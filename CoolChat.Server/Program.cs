using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CoolChat.Server
{
    class ChatClient
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;

        public EndPoint? EndPoint => client?.Client?.RemoteEndPoint;

        public ChatClient(TcpClient client)
        {
            this.client = client;
            this.stream = client.GetStream();
        }

        public Task StartReadAsync()
        {
            return Task.Run(async () =>
            {
                var reader = new StreamReader(stream);
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
            server.Start();
            try
            {
                Console.WriteLine($"Server started on: {server.LocalEndpoint}");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();

                    Console.WriteLine($"Client {client.Client.RemoteEndPoint} was connected");

                    var chatClient = new ChatClient(client);
                    chatClient.MessageReceived += ChatClient_MessageReceived;
                    clients.Add(chatClient);

                    chatClient.StartReadAsync();
                }
            }
            finally
            {
                server.Stop();
                Console.WriteLine("Server is stopped");
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
