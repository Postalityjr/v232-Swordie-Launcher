using System;
using System.Text;

namespace v232.Launcher.WPF.Models
{
    public static class Configs
    {
        // Change this to use Local Login, skipping Updates and replacing IP
        public static bool LocalLogin = true;

        // Base64 encoded IPs - decode these to get actual IP
        // To encode: Convert.ToBase64String(Encoding.UTF8.GetBytes("127.0.0.1"))
        // To decode: Encoding.UTF8.GetString(Convert.FromBase64String(encoded))

        public static string LocalIP = "MjYuMy4xODguNjE="; // Base64: 127.0.0.1
        public static string ServerIP = "MjYuMy4xODguNjE="; // Base64: 192.168.50.10

        public static string WebServerToken = "djIxNF9VcGRhdGVyOk1hcGxldjIxNFVwZGF0ZXI3MTYhQA==";

        public static int APIServerPort = 8483;
        public static int WebServerPort = 80;

        /// <summary>
        /// Gets the decoded server IP based on LocalLogin setting
        /// </summary>
        public static string GetServerIP()
        {
            try
            {
                string encoded = LocalLogin ? LocalIP : ServerIP;
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch
            {
                return "127.0.0.1"; // Fallback
            }
        }
    }
}