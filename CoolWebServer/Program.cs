using System.Net.Sockets;
using System.Text;

namespace CoolWebServer
{
    class Request
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Protocol { get; set; }

        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public static async Task<Request> Parse(StreamReader reader)
        {
            var line = await reader.ReadLineAsync();
            var segments = line.Split(" ");
            var request = new Request() { Method = segments[0], Path = segments[1], Protocol = segments[2] };

            line = await reader.ReadLineAsync();
            while(!string.IsNullOrEmpty(line))
            {
                segments = line.Split(":");
                request.Headers[segments[0]] = segments[1];

                line = await reader.ReadLineAsync();
            }

            return request;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Method} {Path} {Protocol}");

            foreach( var kvp in Headers)
            {
                sb.AppendLine($"{kvp.Key} : {kvp.Value}");
            }
            return sb.ToString();
        }
    }

    class Response
    {

        public string Protocol { get; set; }
        public int StatusCode { get; set; }
        public string StatusDesc { get; set; }

        public string Body { get; set; }

        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();


        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Protocol} {StatusCode} {StatusDesc}");

            foreach (var kvp in Headers)
            {
                sb.AppendLine($"{kvp.Key} : {kvp.Value}");
            }

            sb.AppendLine();
            sb.AppendLine(Body);

            return sb.ToString();
        }
    }


    internal class Program
    {
        static async Task Main(string[] args)
        {
            TcpListener server = new TcpListener(System.Net.IPAddress.Any, 8081);
            server.Start();

            Console.WriteLine($"Server started on: {server.Server.LocalEndPoint}");
            try
            {
                while (true)
                {
                    using var client = await server.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream);
                    using var writer = new StreamWriter(stream);

                    var request = await Request.Parse(reader);
                    Console.WriteLine($"client: {client.Client.RemoteEndPoint}");
                    Console.WriteLine(request.ToString());
                    Console.WriteLine("--------------------------------------------");

                    var response = await GetResponse(request, writer);
                    Console.WriteLine("Response send to client:");
                    Console.WriteLine(response.ToString());
                    Console.WriteLine("============================================");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                server.Stop();
            }
        }

        private static string? GetName(Request request)
        {
            var splitted = request.Path.Split('?');
            splitted = splitted[1].Split('&');

            for (int i = 0; i < splitted.Length; i++)
            {
                var pair = splitted[i];
                var splittedPair = pair.Split('=');
                if (splittedPair[0].ToLower() == "name")
                {
                    return splittedPair[1];
                }
            }

            return null;
        }

        private static async Task<Response> GetResponse(Request request, StreamWriter writer)
        {
            var name = GetName(request);
            var body = name == null
                ? "<html> <body><h1>Hello, World!</h1></body></html>"
                : $"<html> <body><h1>Hello, {name}!</h1></body></html>";

            var response = new Response()
            {
                Protocol = "HTTP/1.1",
                StatusCode = 200,
                StatusDesc = "OK",
                Headers = {
                    { "Content-Length", body.Length.ToString() },
                    { "Content-Type", "text/html" },
                    { "Connection", "Closed" },
                    { "Server", "MyCoolWebServer/1.0.0 (Win32)" }
                },
                Body = body
            };

            await writer.WriteLineAsync(response.ToString());
            await writer.FlushAsync();
            return response;
        }
    }
}
