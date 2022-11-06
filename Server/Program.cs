using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.IO;
using System.Text.Json;

namespace Server
{
    class Server
    {
        static readonly object Lock = new object();
        static readonly Dictionary<int, Client> Clients = new Dictionary<int, Client>();
        static string Header;
        static int usersOnline = 0;
        TcpListener ServerSocket;

        public Server(int port)
        {
            ServerSocket = new TcpListener(IPAddress.Any, port);
            ServerSocket.Start();

            Console.ForegroundColor = ConsoleColor.DarkYellow;

            LoadHeader();

            Console.WriteLine("Server started on port: " + port);

            Thread serverConsoleThread = new Thread(ServerConsole);
            serverConsoleThread.Start();
        }

        public void ListenForClients()
        {
            int clientIndex = -1;

            while (clientIndex++ != -2)
            {
                Client newClient = new Client(ServerSocket.AcceptTcpClient());
                lock (Lock) Clients.Add(clientIndex, newClient);

                Thread t = new Thread(ClientHandler);
                t.Start(clientIndex);
            }
        }

        public static void ServerConsole()
        {
            Response serverResponse = new Response();

            while (true)
            {
                string input = Console.ReadLine();

                if (input.Contains("/kick "))
                {
                    serverResponse.ToName = input.Substring(6);
                    serverResponse.Action = 3;

                    NetworkStream stream = GetClientStreamByName(serverResponse.ToName);

                    if (stream == null)
                    {
                        Console.WriteLine("Client doesn't exists: " + serverResponse.ToName);
                        continue;
                    }

                    SendText(stream, JsonSerializer.Serialize(serverResponse));
                }

                FormatConsole(input);

                serverResponse.Text = input;

                Console.WriteLine(serverResponse);
                BroadcastResponse(-1, JsonSerializer.Serialize(serverResponse));
            }
        }

        private static NetworkStream GetClientStreamByName(string name)
        {
            foreach (Client c in Clients.Values)
            {
                if (c.GetName() == name)
                {
                    return c.GetClient().GetStream();
                }
            }

            return null;
        }

        public static void ClientHandler(object o)
        {
            int clientIndex = (int)o;
            Client listClient;
            TcpClient client;

            listClient = Clients[clientIndex];
            lock (Lock) client = listClient.GetClient();

            NetworkStream stream = client.GetStream();

            listClient.SetId(clientIndex);

            usersOnline++;

            Handshake handshake = new Handshake
            {
                Id = clientIndex,
                UsersOnline = usersOnline,
                Text = "SERVER: Hi! You are connected!",
                Header = Header,
            };

            SendText(stream, JsonSerializer.Serialize(handshake));

            string clientHandshake = GetText(clientIndex);

            Handshake hs = JsonSerializer.Deserialize<Handshake>(clientHandshake);

            listClient.SetName(hs.Name);

            // Broadcast that a new user is connected
            ServerResponse(1, clientIndex);

            while (true)
            {
                string response = GetText(clientIndex);

                if (response == null)
                {
                    ServerResponse(2, clientIndex);
                    RemoveClient(clientIndex);
                    return;
                }

                HandleResponse(response);
            }
        }

        private static void ServerResponse(int code, int targetClientId)
        {
            Client client = Clients[targetClientId];

            Response response = new Response();

            switch (code)
            {
                case 1:
                    response.Text = "User connected: " + client.GetName();
                    response.Action = 1;
                    break;
                case 2:
                    response.Text = "User left: " + client.GetName();
                    response.Action = 2;
                    BroadcastResponse(targetClientId, JsonSerializer.Serialize(response));
                    Console.WriteLine(response);
                    return;
                case 3:
                    string users = "Users:";

                    foreach(Client c in Clients.Values)
                    {
                        users += " " + c.GetName();
                    }

                    response.Text = users;

                    SendText(client.GetClient().GetStream(), JsonSerializer.Serialize(response));
                    return;
            }

            BroadcastResponse(client.GetId(), JsonSerializer.Serialize(response));
        }

        public static void LoadHeader()
        {
            try
            {
                Header += File.ReadAllText(@"header.txt");
                Console.WriteLine(Header);
            }
            catch (Exception)
            {
                return;
            }
        }

        public static void RemoveClient(int clientIndex)
        {
            TcpClient client;

            lock (Lock) client = Clients[clientIndex].GetClient();

            lock (Lock) Clients.Remove(clientIndex);
            client.Client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        public static string GetText(int clientIndex)
        {
            NetworkStream stream = Clients[clientIndex].GetClient().GetStream();

            byte[] buffer = new byte[512];

            try
            {
                stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                return null;
            }

            // Buffer size is larger than the actual message
            // The rest is filled with '\0' (' '), we trim it
            return Encoding.Default.GetString(buffer).TrimEnd('\0');
        }

        private static void HandleResponse(string receivedResponse)
        {
            Response response = JsonSerializer.Deserialize<Response>(receivedResponse);

            Console.WriteLine(response);

            if (response.Text == "/list")
            {
                ServerResponse(2, (int)response.SenderId);
                return;
            }

            if (response.ToName == null)
            {
                BroadcastResponse((int)response.SenderId, receivedResponse);
            } 
            else
            {
                // Whisper
                foreach (Client c in Clients.Values)
                {
                    if (c.GetName() == response.ToName)
                    {
                        SendText(c.GetClient().GetStream(), receivedResponse);
                        return;
                    }
                }
            }
        }
        public static void BroadcastResponse(int currentClientId, string response)
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

                    SendText(stream, response);
                }
            }
        }


        public static void SendText(NetworkStream stream, string response)
        {
            byte[] buffer = Encoding.Default.GetBytes(response);

            stream.Write(buffer, 0, buffer.Length);
        }

        private static void FormatConsole(string message)
        {
            // How many rows the blablabla takes
            int repeat = 1;

            if (message.Length > Console.WindowWidth)
            {
                repeat = 2;
            }
            else if (message.Length > Console.WindowWidth * 2)
            {
                repeat = 3;
            }

            int currentLineCursor = Console.CursorTop - 1;
            while (repeat-- != 0)
            {
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, currentLineCursor);
                currentLineCursor--;
            }
        }
    }

    class Client
    {
        string name;
        int id;
        int whisperId = -1;
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

        internal void SetId(int clientIndex)
        {
            id = clientIndex;
        }

        public int GetId()
        {
            return id;
        }

        internal void SetWhisperId(int clientIndex)
        {
            whisperId = clientIndex;
        }

        public int GetWhisperId()
        {
            return whisperId;
        }
    }

    class Handshake
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public int UsersOnline { get; set; }
        public string Header { get; set; }
        public string Timestamp = DateTime.Now.ToString("[HH:mm:ss]");
    }

    class Response
    {
        // Default server
        public int? SenderId { get; set; }
        public string SenderName { get; set; } = "SERVER";
        public string ToName { get; set; }
        public string Text { get; set; }
        // Default server dark yellow
        public int? Color { get; set; }
        // 1 - user joined 2 - user left 3 - kick 4 - name error
        public int? Action { get; set; }
        public string Timestamp = DateTime.Now.ToString("[HH:mm:ss]");

        public override string ToString()
        {
            switch (Color)
            {
                case 0:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case 1:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case 2:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
            }

            if (ToName == null)
            {
                return Timestamp + " " + SenderName + ": " + Text;
            }
            else return Timestamp + " W FROM " + SenderName + ": " + Text;
        }

        public string ToStringWhisper()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            return Timestamp + " W TO " + ToName + ": " + Text;
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Port: ");
            int port = Int32.Parse(Console.ReadLine());
            Console.Clear();

            Server chatServer = new Server(port);
            chatServer.ListenForClients();
        }
    }
}
