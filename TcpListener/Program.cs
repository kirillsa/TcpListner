using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

class TelnetServer
{
    private static TcpListener? _listener;
    private static ConcurrentDictionary<EndPoint, int> _clientSessionValues = new ConcurrentDictionary<EndPoint, int>();
    private static CancellationTokenSource _cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int port))
        {
            Console.WriteLine("Usage: dotnet run <port>");
            return;
        }

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Console.WriteLine($"Server listening on port {port}...");

        while (!_cts.Token.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_cts.Token);
            _ = HandleClientAsync(client);
        }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        if (client is null || client.Client is null || client.Client.RemoteEndPoint is null)
        {
            return;
        }

        Console.WriteLine($"Client {client.Client.RemoteEndPoint} connected.");
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        await writer.WriteLineAsync("Welcome to the Telnet server. Enter messages or commands 'list' 'exit'.");

        while (!_cts.Token.IsCancellationRequested)
        {
            var message = await reader.ReadLineAsync(_cts.Token);
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (message.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (message.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                await HandleListCommandAsync(writer);
                continue;
            }

            if (!int.TryParse(message, out var intMessage))
            {
                //Console.WriteLine($"Client: '{client.Client.RemoteEndPoint}' sent incorrect message value: {message}");
                await writer.WriteLineAsync("Incorrect message value received. Enter the number or the command.");
                continue;
            }

            var newClientValue = DoWork(client.Client.RemoteEndPoint, intMessage);

            Console.WriteLine($"Client: '{client.Client.RemoteEndPoint}' sent message: {message}.");
            await writer.WriteLineAsync($"Client session value is: {newClientValue}");
        }

        _clientSessionValues!.Remove(client.Client.RemoteEndPoint, out var removeResult);
        Console.WriteLine($"Client: '{client.Client.RemoteEndPoint}' disconnected.");
        client.Close();
    }

    static async Task HandleListCommandAsync(StreamWriter streamWriter)
    {
        foreach (var item in _clientSessionValues)
        {
            await streamWriter.WriteLineAsync($"Client: {item.Key} has the value: {item.Value}.");
        }
    }

    static int DoWork(EndPoint client, int inputMessage) =>
        _clientSessionValues.AddOrUpdate(client, inputMessage, (key, value) => value + inputMessage);
}