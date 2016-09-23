using System;
using System.Diagnostics;
using System.Threading;
using YGOSharp.OCGWrapper;

namespace YGOSharp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //Debugger.Launch();

            BanlistManager.Init("lflist.conf");
            Api.Init(".", "script", "cards.cdb");

            CoreServer server = new CoreServer();
            server.Start();
            while (server.IsRunning)
            {
                server.Tick();
                Thread.Sleep(1);
            }
            server.suiside();
            Environment.Exit(0);
        }

        public static string from_byte_to_base64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes, 0, bytes.Length);
        }

        public static byte[] from_base64_to_btyes(string str)
        {
            return Convert.FromBase64String(str);
        }
    }
}
