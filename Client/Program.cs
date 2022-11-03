using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Client
{
    public class Client
    {
        private TcpClient client;
        private NetworkStream stream;
        private string name;

        public Client(IPAddress ip, int port, string name)
        {
            client = new TcpClient();
            client.Connect(ip, port);
            this.name = name;
        }

        public void Handshake()
        {
            stream = client.GetStream();

            Thread thread = new Thread(o => GetMessage((TcpClient)o));
            thread.Start(client);

            // Sending our name
            byte[] buffer = Encoding.Default.GetBytes(name);
            stream.Write(buffer, 0, buffer.Length);

            SendMessageLoop(stream, name);
        }

        static void SendMessageLoop(NetworkStream stream, string name)
        {
            // Send message loop
            while (true)
            {

                string message = Console.ReadLine();
                Console.Write("All: ");

                Console.SetCursorPosition(0, Console.CursorTop - 1);
                ClearCurrentConsoleLine();

                byte[] buffer = Encoding.Default.GetBytes(message);

                stream.Write(buffer, 0, buffer.Length);

                Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + name + ": " + message);
            }
        }

        static void GetMessage(TcpClient client)
        {

            while (true)
            {
                NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[512];

                stream.Read(buffer, 0, buffer.Length);

                string receivedMessage = Encoding.Default.GetString(buffer);

                // Buffer size is larger than the actual message
                // The rest is filled with '\0' (' '), we trim it
                receivedMessage = receivedMessage.TrimEnd('\0');

                Console.WriteLine(receivedMessage);
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Name: ");
            string name = Console.ReadLine();
            Console.Clear();

            Client client = new Client(IPAddress.Parse("127.0.0.1"), 7676, name);
            client.Handshake();
        }
    }
}
