using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CoolChat.Server
{
    internal class User
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    class ChatServer
    {
        private List<ChatClient> clients = new List<ChatClient>();
        private List<string> history = new List<string>();
        private List<User> users = new List<User>();

        public async Task Start()
        {
            LoadUsers(@"D:\C# Pro\users.txt");
            LoadHistoryFromFile(@"D:\C# Pro\chat_history.txt");

            var udpServerTask = Task.Run(async () =>
            {
                UdpClient udpServer = new UdpClient(7701, AddressFamily.InterNetwork);
                while (true)
                {
                    var result = await udpServer.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    if (message == "SCAN BY COOL CHAT SERVER")
                    {
                        message = $"YES PORT:{7700}";
                        var sendTask = udpServer.SendAsync(Encoding.UTF8.GetBytes(message), result.RemoteEndPoint);
                    }
                }
            });

            TcpListener tcpServer = new TcpListener(IPAddress.Any, 7700);
            tcpServer.Start();

            Console.WriteLine($"Server started on: {tcpServer.LocalEndpoint}");

            while (true)
            {
                TcpClient tcpClient = tcpServer.AcceptTcpClient();
                Console.WriteLine($"Client {tcpClient.Client.RemoteEndPoint} was connected");

                var chatClient = new ChatClient(tcpClient, this);
                clients.Add(chatClient);

                chatClient.Start();

                await chatClient.StartReadAsync();
            }
        }

        public void BroadcastMessage(string message, string username)
        {
            history.Add(message);
            SaveHistoryToFile(message, username); 

            foreach (var client in clients)
            {
                client.SendMessage(message);
            }
        }

        public void PrivateMessage(string sender, string receiver, string message, string senderUsername)
        {
            var receiverClient = clients.Find(c => c.Username == receiver);
            var senderClient = clients.Find(c => c.Username == sender);

            if (receiverClient != null && senderClient != null)
            {
                var privateMessage = $"[Private from {sender}]: {message}";

                history.Add(privateMessage);
                SaveHistoryToFile(privateMessage, senderUsername); 
                receiverClient.SendMessage(privateMessage);
                senderClient.SendMessage(privateMessage);
            }
            else
            {
                senderClient.SendMessage($"User '{receiver}' not found.");
            }
        }

        public void SaveUsersToFile(string filePath)
        {
            try
            {
                var lines = users.Select(u => $"{u.Username},{u.Password}");
                File.WriteAllLines(filePath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving users: " + ex.Message);
            }
        }

        private void LoadUsers(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);

                    foreach (var line in lines)
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 2)
                        {
                            var username = parts[0];
                            var password = parts[1];
                            users.Add(new User { Username = username, Password = password });
                        }
                    }
                }
                else
                {
                    Console.WriteLine("File not found: " + filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading users: " + ex.Message);
            }

            SaveUsersToFile(filePath);
        }

        public bool AuthenticateUser(string username, string password)
        {
            return users.Any(u => u.Username == username && u.Password == password);
        }

        public void SaveHistoryToFile(string message, string username)
        {
            string directoryPath = @"D:\C# Pro\";
            string filePath = Path.Combine(directoryPath, "chat_history.txt");

            try
            {
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();
                }

                File.AppendAllText(filePath, $"{DateTime.Now} - [{username}]: {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving message to history: {ex.Message}");
            }
        }
        
        public void LoadHistoryFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                history.AddRange(File.ReadAllLines(filePath));
            }
        }
    }

    class ChatClient
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private ChatServer server;

        public string Username { get; private set; }

        public EndPoint? EndPoint => client?.Client?.RemoteEndPoint;

        public ChatClient(TcpClient client, ChatServer server)
        {
            this.client = client;
            this.stream = client.GetStream();
            this.reader = new StreamReader(stream);
            this.writer = new StreamWriter(stream);
            this.server = server;
        }

        public void Start()
        {
            writer.WriteLine("Enter your username:");
            writer.Flush();
            Username = reader.ReadLine();

            writer.WriteLine($"Welcome, {Username}!");
            writer.Flush();

            Task.Run(async () =>
            {
                while (true)
                {
                    var message = await reader.ReadLineAsync();
                    if (message == null)
                    {
                        break;
                    }

                    if (message.StartsWith("/private"))
                    {
                        var parts = message.Split(' ');
                        if (parts.Length >= 3)
                        {
                            var receiver = parts[1];
                            var privateMessage = string.Join(' ', parts.Skip(2));
                            server.PrivateMessage(Username, receiver, privateMessage, Username);
                        }
                    }
                    else
                    {
                        server.BroadcastMessage($"[{Username}]: {message}", Username);
                    }
                }
            });
        }

        public Task StartReadAsync()
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    var msg = await reader.ReadLineAsync();
                    Console.WriteLine($"[{EndPoint}] Message: {msg}");
                    MessageReceived?.Invoke(this, msg);
                }
            });
        }

        public event EventHandler<string?> MessageReceived;

        public async Task SendMessage(string? message)
        {
            var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }
    }

    internal class Program
    {
        static List<ChatClient> clients = new List<ChatClient>();

        static async Task Main(string[] args)
        {
            var chatServerInstance = new ChatServer();


            var task = Task.Run(async () =>
            {
                UdpClient udpClient = new UdpClient(7701, AddressFamily.InterNetwork);

                while (true)
                {
                    var result = await udpClient.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    if (message == "SCAN BY COOL CHAT SERVER")
                    {
                        message = $"YES PORT:{7700}";
                        var sendTask = udpClient.SendAsync(Encoding.UTF8.GetBytes(message), result.RemoteEndPoint);
                    }
                }
            });

            var server = new TcpListener(System.Net.IPAddress.Any, 7700);
            string message = "Your message"; 
            string username = "Your username"; 
            server.Start();
            Console.WriteLine($"Server started on: {server.LocalEndpoint}");

            try
            {
                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    Console.WriteLine($"Client {client.Client.RemoteEndPoint} was connected");

                    var chatClient = new ChatClient(client, chatServerInstance);
                    chatClient.MessageReceived += ChatClient_MessageReceived;
                    clients.Add(chatClient);

                    chatClient.StartReadAsync();
                }
            }
            finally
            {
                server.Stop();
                Console.WriteLine("Server is stopped");

                try
                {
                    var historyFilePath = @"D:\C# Pro\chat_history.txt";
                    var usersFilePath = @"D:\C# Pro\users.txt";

                    if (!File.Exists(historyFilePath))
                    {
                        File.Create(historyFilePath).Close();
                    }

                    if (!File.Exists(usersFilePath))
                    {
                        File.Create(usersFilePath).Close();
                    }

                    chatServerInstance.SaveHistoryToFile(message, username);
                    chatServerInstance.SaveUsersToFile(usersFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred while saving files: {ex.Message}");
                }
            }
        }

        private static async void ChatClient_MessageReceived(object? sender, string? message)
        {
            var chatClient = (ChatClient?)sender;
            foreach (var client in clients)
            {
                await client.SendMessage($"{chatClient?.EndPoint}@{message}");
            }
        }
    }
}