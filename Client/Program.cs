using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Text.Json;

namespace Client
{
    public class Client
    {
        private TcpClient client;
        private NetworkStream stream;

        private int id;
        private string name;
        private static int usersOnline = 1;
        private string recipient = "ALL";


        public Client(IPAddress ip, int port, string name)
        {
            client = new TcpClient();

            bool ok = false;
            while (!ok)
            {
                try
                {
                    client.Connect(ip, port);
                    ok = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Couldn't connect to server");
                    Console.WriteLine("Press enter to retry...");
                    Console.ReadLine();
                }
            }

            Console.Clear();

            this.name = name;
        }

        public void SetWindowTitle()
        {
            Console.Title = "TCPChatClient - Name: " + name + " Users online: " + usersOnline + " Recipient(s): " + recipient;
        }

        public void Handshake()
        {
            stream = client.GetStream();

            HandleHandshake();

            Thread thread = new Thread(o => GetResponseLoop((TcpClient)o));
            thread.Start(client);

            SendResponseLoop(stream, name);
        }

        private void HandleHandshake()
        {
            string receivedHandshake = GetText(client);

            Handshake handshake = JsonSerializer.Deserialize<Handshake>(receivedHandshake);

            // CW and setting variable by handshake
            id = handshake.Id;
            usersOnline = handshake.UsersOnline;

            // Now we have data for the title
            SetWindowTitle();

            Console.WriteLine(handshake.Header);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(handshake.Timestamp + " " + handshake.Text);

            // Sending the handshake back with our name
            handshake.Name = name;

            byte[] buffer = Encoding.Default.GetBytes(JsonSerializer.Serialize(handshake));
            stream.Write(buffer, 0, buffer.Length);

            Console.ForegroundColor = ConsoleColor.Green;
        }

        void SendResponseLoop(NetworkStream stream, string name)
        {
            Thread.Sleep(10);

            // Send message loop
            while (true)
            {
                string message = Console.ReadLine();

                Response response = new Response();

                FormatConsole(message);

                if (message == null)
                {
                    ServerShutdown();
                }

                if (message.Contains("/w "))
                {
                    Whisper(message);
                    continue;
                }

                response = new Response
                {
                    SenderId = id,
                    SenderName = name,
                    Text = message,
                    Color = 1
                };
                
                Console.WriteLine(response);

                SendText(JsonSerializer.Serialize(response));
            }
        }

        private void Whisper(string message)
        {
            // Name of who receives the whisper
            recipient = message.Substring(3);

            // Just updating the Recipient(s) part
            SetWindowTitle();

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss]") + " SERVER: Whispering to " + recipient);
            Console.ForegroundColor = ConsoleColor.Magenta;

            // Whisper mode enabled until /all
            while ((message = Console.ReadLine()) != "/all")
            {
                Response response = new Response
                {
                    SenderId = id,
                    SenderName = name,
                    ToName = recipient,
                    Text = message,
                    Color = 2
                };

                FormatConsole(message);

                // [time] W TO [name]: text
                Console.WriteLine(response.ToStringWhisper());

                // Actually sending the whisper
                SendText(JsonSerializer.Serialize(response));
            }

            // Whisper mode ended, set back the window title
            recipient = "ALL";
            SetWindowTitle();

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            FormatConsole(message);
            Console.WriteLine(DateTime.Now.ToString("[HH:mm:ss]") + " SERVER: Talking to everyone");
            Console.ForegroundColor = ConsoleColor.Green;
        }

        private void SendText(string message)
        {
            byte[] buffer = Encoding.Default.GetBytes(message);

            try
            {
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                ServerShutdown();
            }
        }

        // When the user types stuff: blablabla
        // After pressing enter: [time] [name]: blablabla
        // The blablabla obviously can stretch across multiple rows
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

        private void GetResponseLoop(TcpClient client)
        {
            while (true)
            {
                string response = GetText(client);

                HandleResponse(response);
            }
        }

        public static string GetText(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

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
            string receivedResponse = Encoding.Default.GetString(buffer).TrimEnd('\0');

            return receivedResponse;
        }

        private void HandleResponse(string receivedResponse)
        {
            if (receivedResponse == null)
            {
                ServerShutdown();
            }

            Response response = JsonSerializer.Deserialize<Response>(receivedResponse);

            Console.WriteLine(response);

            switch (response.Action)
            {
                case 1:
                    usersOnline++;
                    SetWindowTitle();
                    break;
                case 2:
                    usersOnline--;
                    SetWindowTitle();
                    break;
                case 3:
                    Kick();
                    break;
                case 4:
                    NameError();
                    break;
                default:
                    return;
            }
        }

        private void ServerShutdown()
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Server has been shut down");
            Console.ReadKey();
            Exit();
        }

        private void Kick()
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("You have been kicked out from the server!");
            Console.ReadKey();
            Console.BackgroundColor = ConsoleColor.Black;

            Exit();
        }

        private void NameError()
        {
            throw new NotImplementedException();
        }

        private void Exit()
        {
            Environment.Exit(0);
        }
    }

    class Response
    {
        // Default server
        public int? SenderId { get; set; }
        public string SenderName { get; set; }
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
            } else return Timestamp + " W FROM " + SenderName + ": " + Text;
        }

        public string ToStringWhisper()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            return Timestamp + " W TO " + ToName + ": " + Text;
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

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Name: ");
            string name = Console.ReadLine();

            bool ok = false;

            IPAddress ip = null;
            while (!ok)
            {
                Console.Write("Server IP address: ");
                string input = Console.ReadLine();

                if (input == "localhost")
                {
                    ip = IPAddress.Parse("127.0.0.1");
                    ok = true;
                    continue;
                }

                try
                {
                    ip = IPAddress.Parse(input);
                    ok = true;
                }
                catch (Exception)
                {
                    Console.Clear();
                }
            }

            ok = false;

            int port = 0;
            while (!ok)
            {
                Console.Write("Server port: ");
                try
                {
                    port = int.Parse(Console.ReadLine());
                    ok = true;
                }
                catch (Exception)
                {
                    Console.Clear();
                }
            }

            Console.Clear();

            Client client = new Client(ip, port, name);
            client.Handshake();
        }
    }
}
