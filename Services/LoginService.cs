using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using v232.Launcher.WPF.Models;

// Marshal is in System.Runtime.InteropServices

namespace v232.Launcher.WPF.Services
{
    public class LoginService
    {
        private readonly static string sDllPath = "Localhost.dll";
        private readonly static uint CREATE_SUSPENDED = 0x00000004;

        public Client CClient { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
        public string Token { get; set; }
        public bool Auth { get; set; }

        public LoginService()
        {
        }

        public LoginService(string user, string pass)
        {
            User = user;
            Pass = pass;
            Token = null;
            Auth = false;
            CClient = null;
        }

        #region Win32 API

        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
                        bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment,
                        string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, IntPtr dwStackSize, IntPtr lpStartAddress,
            IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        #endregion

        private static int Inject(uint processID, string dllPath)
        {
            // Check if DLL exists first
            if (!File.Exists(dllPath))
            {
                MessageBox.Show($"DLL not found: {dllPath}", "Inject Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            if (processID == 0)
                return 1;

            IntPtr pLoadLibraryAddress = GetProcAddress(GetModuleHandle("Kernel32.dll"), "LoadLibraryA");
            if (pLoadLibraryAddress == (IntPtr)0)
                return 2;

            IntPtr processHandle = OpenProcess((0x2 | 0x8 | 0x10 | 0x20 | 0x400), 1, (uint)processID);
            if (processHandle == (IntPtr)0)
                return 3;

            // Allocate length + 1 for null terminator
            IntPtr lpAddress = VirtualAllocEx(processHandle, (IntPtr)null, (IntPtr)(dllPath.Length + 1), (0x1000 | 0x2000), 0X40);
            if (lpAddress == (IntPtr)0)
                return 4;

            byte[] bytes = Encoding.ASCII.GetBytes(dllPath + "\0"); // Add null terminator
            if (WriteProcessMemory(processHandle, lpAddress, bytes, (uint)bytes.Length, 0) == 0)
                return 5;

            IntPtr threadId;
            IntPtr hThread = CreateRemoteThread(processHandle, IntPtr.Zero, IntPtr.Zero, pLoadLibraryAddress, lpAddress, 0, out threadId);
            if (hThread == (IntPtr)0)
            {
                int err = Marshal.GetLastWin32Error();
                MessageBox.Show($"CreateRemoteThread failed.\nError: {err}\nDLL: {dllPath}", "Inject Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseHandle(processHandle);
                return 6;
            }

            // Wait for DLL to load (max 10 seconds)
            WaitForSingleObject(hThread, 10000);

            CloseHandle(hThread);
            CloseHandle(processHandle);
            return 0;
        }

        private async Task<byte> GetFileChecksum(string filename, string checksum)
        {
            this.CClient.Send(OutPackets.FileChecksum(filename, checksum));
            InPacket inPacket = this.CClient.Receive();
            inPacket.readInt();
            int num = (int)inPacket.readShort();
            return inPacket.readByte();
        }

        public async Task<bool> Authenticate()
        {
            try
            {
                if (CClient == null || !CClient.IsConnected())
                {
                    CClient = new Client();
                    if (!CClient.Connect())
                    {
                        return false;
                    }
                }

                var result = await Handlers.SendAuthRequest(User, Pass, CClient);

                if (result.result == 0)
                {
                    Token = result.token;
                    Auth = true;
                    return true;
                }

                Auth = false;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Auth = false;
                return false;
            }
        }

        public bool LaunchMaple()
        {
            if (!Configs.LocalLogin)
            {
                bool passChecksum = true;
                int bufferSize = 10 * 1024 * 1024; // 10 MB

                string[] wz_files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.wz", SearchOption.TopDirectoryOnly);
                if (wz_files.Length > 26)
                {
                    MessageBox.Show("Too many .wz files detected. Please remove any wz files that didn't come with the client install.", "WZ Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                else if (wz_files.Length < 26)
                {
                    MessageBox.Show("Too few .wz files detected. Please reinstall the client or replace the files needed.", "WZ Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                string[] wz_ignore = { "Effect", "Sound", "Morph", "Reactor", "String", "TamingMob", "Base" };

                Parallel.ForEach(wz_files, async wz_file =>
                {
                    try
                    {
                        if (wz_ignore.Any(s => wz_file.Contains(s)))
                            return;

                        string[] file_parts = wz_file.Split('\\');
                        string file_name = file_parts[file_parts.Length - 1];
                        file_name = file_name.Replace(".wz", "");

                        byte[] fileHash;
                        using (var md5 = MD5.Create())
                        using (var fileStream = new FileStream(wz_file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                        using (var bufferedStream = new BufferedStream(fileStream, bufferSize))
                        {
                            fileHash = md5.ComputeHash(bufferedStream);
                        }

                        string hashString = BitConverter.ToString(fileHash).Replace("-", "").ToLowerInvariant();
                        byte checkFileChecksum = await GetFileChecksum(file_name, hashString);

                        if (checkFileChecksum.Equals(1))
                        {
                            MessageBox.Show("One or more .wz files failed the integrity check. Please re-download the correct files.", "File Integrity Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            passChecksum = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {wz_file}: {ex.Message}");
                    }
                    finally
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                });

                if (!passChecksum)
                    return false;
            }

            try
            {
                STARTUPINFO si = new STARTUPINFO();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                bool bCreateProc = CreateProcess("MapleStory.exe", $" WebStart {this.Token}", IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, null, ref si, out pi);

                if (bCreateProc)
                {
                    // Use full path for DLL injection
                    string fullDllPath = Path.Combine(Directory.GetCurrentDirectory(), sDllPath);
                    int bInject = Inject(pi.dwProcessId, fullDllPath);
                    if (bInject == 0)
                    {
                        ResumeThread(pi.hThread);

                        CloseHandle(pi.hThread);
                        CloseHandle(pi.hProcess);

                        return true;
                    }
                    else
                    {
                        MessageBox.Show("Error code: " + bInject.ToString(), "Injection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    // CreateProcess failed - show error
                    int error = Marshal.GetLastWin32Error();
                    MessageBox.Show($"CreateProcess failed!\n\nError code: {error}\n\nMake sure:\n1. Launcher is in MapleStory folder\n2. MapleStory.exe exists\n3. Run as Administrator", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start the game.\n\nError: {ex.Message}\n\nMake sure the file is in your game folder and that this program is ran as admin.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }
    }
}
