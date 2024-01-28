using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace CoolChat.Client
{
    internal class Program
    {

        static async Task Main(string[] args)
        {
            try
            {
                using TcpClient tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(IPAddress.Loopback, 7700);
                Console.WriteLine("Connection established!");

                using var stream = tcpClient.GetStream();

                Console.WriteLine("Enter your username:");
                var username = Console.ReadLine();

                Console.WriteLine("Enter your password:");
                var password = Console.ReadLine();

                await RegisterOrLoginAsync(stream, username, password);

                var readerTask = ReadAsync(stream, username);
                var writerTask = WriteAsync(stream, username);

                await Task.WhenAny(readerTask, writerTask);

                Console.WriteLine("Client was disconnected.");
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        static void HandleException(Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            if (ex is SocketException || ex is IOException)
            {
                Console.WriteLine("Client was disconnected");
            }
        }

        static async Task RegisterOrLoginAsync(NetworkStream stream, string username, string password)
        {
            var writer = new StreamWriter(stream);
            var reader = new StreamReader(stream);

            await writer.WriteLineAsync(username);
            await writer.WriteLineAsync(password);
            await writer.FlushAsync();

            var response = await reader.ReadLineAsync();
            Console.WriteLine(response);
        }

        static Task ReadAsync(NetworkStream stream,string username)
        {
            return Task.Run(async () =>
            {
                var endPoint = stream.Socket.LocalEndPoint;
                var reader = new StreamReader(stream);
                try
                {
                    while (true)
                    {
                        var msg = await reader.ReadLineAsync();
                        if (msg == "exit")
                        {
                           break;
                        }

                        if (!string.IsNullOrEmpty(msg))
                        {
                            var splittedMsg = msg.Split("@");
                            if (splittedMsg.Length == 2)
                            {
                                Console.WriteLine($"[Private from {splittedMsg[0]}]: {splittedMsg[1]}");
                                SaveMessageToHistory($"[Private from {splittedMsg[0]}]: {splittedMsg[1]}", username);
                            }
                            else
                            {
                                Console.WriteLine(msg);
                                SaveMessageToHistory(msg, username);
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("Disconnected from the server.");
                }
            });
        }

        static Task WriteAsync(Stream stream, string username)
        {
            return Task.Run(async () =>
            {
                var writer = new StreamWriter(stream);
                try
                {
                    while (true)
                    {
                        Console.Write("Enter your message (type 'exit' to quit): ");
                        var text = Console.ReadLine();

                        await writer.WriteLineAsync($"{username}: {text}");
                        await writer.FlushAsync();

                        if (text == "exit")
                        {
                            break;
                        }

                        SaveMessageToHistory($"[{username}]: {text}", username);
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("Disconnected from the server.");
                }
            });
        }

        static void SaveMessageToHistory(string message, string username)
        {
            string directoryPath = @"D:\C# Pro\";
            string filePath = Path.Combine(directoryPath, $"{username}_chat_history.txt");

            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }

            File.AppendAllText(filePath, $"{DateTime.Now} - {message}{Environment.NewLine}");
        }
    }
}
