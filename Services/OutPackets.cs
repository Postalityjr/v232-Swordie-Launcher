using v232.Launcher.WPF.Models;

namespace v232.Launcher.WPF.Services
{
    public static class OutPackets
    {
        private static int AuthRequestPacket = 100;
        private static int CreateAccountPacket = 101;
        private static int FileChecksumPacket = 10002;
        private static int HeartbeatRequestPacket = 11000;

        public static OutPacket AuthRequest(string username, string pwd)
        {
            OutPacket outPacket = new OutPacket((short)AuthRequestPacket);
            outPacket.WriteString(username);
            outPacket.WriteString(pwd);
            return outPacket;
        }

        public static OutPacket CreateAccountRequest(string username, string pwd, string email)
        {
            OutPacket outPacket = new OutPacket((short)CreateAccountPacket);
            outPacket.WriteString(username);
            outPacket.WriteString(pwd);
            outPacket.WriteString(email);
            return outPacket;
        }

        public static OutPacket FileChecksum(string filename, string checksum)
        {
            OutPacket outPacket = new OutPacket((short)FileChecksumPacket);
            outPacket.WriteString(filename);
            outPacket.WriteString(checksum);
            return outPacket;
        }

        public static OutPacket HeartbeatRequest(string heartbeat)
        {
            OutPacket outPacket = new OutPacket((short)HeartbeatRequestPacket);
            outPacket.WriteString(heartbeat);
            return outPacket;
        }
    }
}
