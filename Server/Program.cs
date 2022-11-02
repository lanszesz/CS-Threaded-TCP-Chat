using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;

namespace Server
{
    class Server
    {
        static readonly object Lock = new object();
        static readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        TcpListener ServerSocket;

        public Server(int port)
        {
            ServerSocket = new TcpListener(IPAddress.Any, port);
            ServerSocket.Start();
            Console.ForegroundColor = ConsoleColor.Magenta;
        }

        public void ListenForClients()
        {
            int id = -1;

            while (id++ != -2)
            {
                Client newClient = new Client(ServerSocket.AcceptTcpClient());
                lock (Lock) Clients.Add(id, newClient);

                Thread t = new Thread(ClientHandler);
                t.Start(id);
            }
        }

        public static void ClientHandler(object o)
        {
            int id = (int)o;
            TcpClient client;

            lock (Lock) client = Clients[id].GetClient();

            Clients[id].SetName(GetMessage(client.GetStream()));
            Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + Clients[id].GetName() + " has connected!");
            SendMessage(client.GetStream(), DateTime.Now.ToString("[HH:mm:ss] ") +  "SERVER: Hi. You are connected!");
            BroadcastServerMessage(Clients[id].GetName() + " has connected!", id);

            while (true)
            {
                string message = GetMessage(client.GetStream());

                if (message == "")
                {
                    break;
                }

                Broadcast(message, id);
                Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + Clients[id].GetName() + ": " + message);
            }

            lock (Lock) Clients.Remove(id);
            client.Client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        public static void Broadcast(string message, int currentClientId)
        {
            lock (Lock)
            {
                for (int i = 0; i < Clients.Count; i++)
                {
                    // Don't broadcast the message back to the sender
                    if (i == currentClientId)
                    {
                        continue;
                    }

                    TcpClient client = Clients[i].GetClient();
                    NetworkStream stream = client.GetStream();
                    string formattedMessage = DateTime.Now.ToString("[HH:mm:ss] ") + Clients[currentClientId].GetName() + ": " + message;
                    SendMessage(stream, formattedMessage);
                }
            }
        }

        public static void BroadcastServerMessage(string message, int currentClientId)
        {
            lock (Lock)
            {
                for (int i = 0; i < Clients.Count; i++)
                {
                    // Don't broadcast the message back to the sender
                    if (i == currentClientId)
                    {
                        continue;
                    }

                    TcpClient client = Clients[i].GetClient();
                    NetworkStream stream = client.GetStream();
                    string formattedMessage = DateTime.Now.ToString("[HH:mm:ss] ") + "SERVER: " + message;
                    SendMessage(stream, formattedMessage);
                }
            }
        }

        public static string GetMessage(NetworkStream stream)
        {
            byte[] buffer = new byte[512];

            stream.Read(buffer, 0, buffer.Length);

            string receivedMessage = Encoding.Default.GetString(buffer);

            // Buffer size is larger than the actual message
            // The rest is filled with '\0' (' '), we trim it
            receivedMessage = receivedMessage.TrimEnd('\0');

            if (receivedMessage == "/list")
            {
                string userNames = "Users: ";
                foreach(Client client in Clients.Values)
                {
                    userNames += client.GetName() + ' ';
                }
                BroadcastServerMessage(userNames, 0);
            }

            return receivedMessage;
        }

        public static void SendMessage(NetworkStream stream, string message)
        {
            byte[] buffer = Encoding.Default.GetBytes(message);

            stream.Write(buffer, 0, buffer.Length);
        }
    }

    class Client
    {
        string name;
        TcpClient tcpClient;

        public Client(TcpClient client)
        {
            tcpClient = client;
        }

        public Client(TcpClient client, string name)
        {
            tcpClient = client;
            this.name = name;
        }

        public string GetName()
        {
            return name;
        }

        public void SetName(string name)
        {
            this.name = name;
        }

        public TcpClient GetClient()
        {
            return tcpClient;
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Server chatServer = new Server(7676);
            chatServer.ListenForClients();
        }
    }
}
