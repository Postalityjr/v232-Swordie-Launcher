using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using v232.Launcher.WPF.Models;

namespace v232.Launcher.WPF.Services
{
    public static class Handlers
    {
        /// <summary>
        /// Parses authentication response and returns the token
        /// </summary>
        public static (byte result, string token, byte accountType) GetAuthTokenFromInput(InPacket inPacket)
        {
            inPacket.readInt(); // packet length
            inPacket.readShort(); // header
            byte result = inPacket.readByte();
            string token = "";
            byte accountType = 0;

            if (result == 0) // Success
            {
                token = inPacket.readString();
                accountType = inPacket.readByte();
                Console.WriteLine("Token: " + token + " Account Type: " + accountType);
            }
            else
            {
                Console.WriteLine("Login failed with result code: " + result);
            }

            return (result, token, accountType);
        }

        /// <summary>
        /// Sends authentication request and returns (result, token)
        /// Result codes: 0 = success, 1 = invalid credentials, 2+ = other errors
        /// </summary>
        public static async Task<(byte result, string token)> SendAuthRequest(string username, string password, Client client)
        {
            return await Task.Run(() =>
            {
                try
                {
                    client.Send(OutPackets.AuthRequest(username, password));
                    InPacket inPacket = client.Receive();
                    var authResult = GetAuthTokenFromInput(inPacket);
                    return (authResult.result, authResult.token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return ((byte)4, "");
                }
            });
        }

        /// <summary>
        /// Sends account creation request
        /// Result codes: 0 = success, 1 = username taken, 2 = IP already created, 3 = MAC already created, 4 = unknown error
        /// </summary>
        public static async Task<byte> SendAccountCreateRequest(string username, string password, string email, Client client)
        {
            return await Task.Run(() =>
            {
                try
                {
                    client.Send(OutPackets.CreateAccountRequest(username, password, email));
                    InPacket inPacket = client.Receive();
                    inPacket.readInt(); // packet length
                    inPacket.readShort(); // header
                    byte result = inPacket.readByte();
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return (byte)4;
                }
            });
        }

        private static string Sha256Hex(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
