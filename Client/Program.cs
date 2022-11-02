using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace Client
{
    public class Client
    {
        private TcpClient client;
        private NetworkStream stream;
        string name;

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

            Console.WriteLine("Connected to server");

            // Sending our name
            byte[] buffer = Encoding.Default.GetBytes(name);
            stream.Write(buffer, 0, buffer.Length);

            SendMessageLoop(stream);
        }

        static void SendMessageLoop(NetworkStream stream)
        {
            // Send message loop
            while (true)
            {
                string message = Console.ReadLine();

                byte[] buffer = Encoding.Default.GetBytes(message);

                stream.Write(buffer, 0, buffer.Length);
            }
        }

        static void GetMessage(TcpClient client)
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

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Name: ");
            string name = Console.ReadLine();

            Client client = new Client(IPAddress.Parse("127.0.0.1"), 7676, name);
            client.Handshake();
        }
    }
}
