using System.Net;
using System.Text;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_0;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_0;
using System.Runtime.CompilerServices;

namespace WasiMainWrapper;

public static class Program
{
    private readonly record struct IPv4Bytes(byte A, byte B, byte C, byte D)
    {
        public void Deconstruct(out (byte, byte, byte, byte) tuple) => tuple = (A, B, C, D);
    }

    private static IPv4Bytes GetIpTuple(IPAddress ip) => ip.GetAddressBytes() is [var a, var b, var c, var d]
            ? new(a, b, c, d)
            : throw new ArgumentException("Only IPv4 addresses are supported.");

    private static async Task<(IStreams.InputStream Input, IStreams.OutputStream Output)> ConnectAsync(INetwork.Network network, INetwork.IpSocketAddress remoteAddress, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("CreateTcpSocket");
        ITcp.TcpSocket tcpSocket = TcpCreateSocketInterop.CreateTcpSocket(INetwork.IpAddressFamily.IPV4);

        Console.WriteLine("Starting connection...");
        tcpSocket.StartConnect(network, remoteAddress);

        Console.WriteLine("Waiting for connection to complete...");
        while (true)
        {
            try
            {
                var streams = tcpSocket.FinishConnect();
                Console.WriteLine("Connection established!");
                return streams;
            }
            catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
            {
                Console.WriteLine("Connection in progress, waiting...");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private static async Task<List<byte>> ReadResponseAsync(IStreams.InputStream inputStream, CancellationToken cancellationToken = default)
    {
        const ulong READ_SIZE = 1024UL;
        List<byte> responseData = [];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                byte[] chunk = inputStream.BlockingRead(READ_SIZE);
                if (chunk is [] or null) break;

                responseData.AddRange(chunk);
                Console.WriteLine($"Received chunk with {chunk.Length} bytes");
            }
            catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
            {
                Console.WriteLine("Waiting for more data...");
                await Task.Delay(100, cancellationToken);
            }
            catch (WitException e) when (e.Value is IStreams.StreamError)
            {
                break;
            }
        }

        return responseData;
    }

    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            Console.WriteLine("Create INetwork.Ipv4SocketAddress");
            IPv4Bytes ipBytes = GetIpTuple(IPAddress.Parse("96.7.128.175"));
            INetwork.Ipv4SocketAddress address = new(80, (ipBytes.A, ipBytes.B, ipBytes.C, ipBytes.D));

            Console.WriteLine("Create remoteAddress");
            INetwork.IpSocketAddress remoteAddress = INetwork.IpSocketAddress.Ipv4(address);

            Console.WriteLine("InstanceNetwork");
            INetwork.Network network = InstanceNetworkInterop.InstanceNetwork();

            (IStreams.InputStream Input, IStreams.OutputStream Output) streams = await ConnectAsync(network, remoteAddress, cts.Token);
            
            Console.WriteLine("Connected to the HTTP server via WASI sockets.");

            string request = 
                "GET / HTTP/1.1\r\n" +
                "Host: example.com\r\n" +
                "User-Agent: WASI-Client/1.0\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            Console.WriteLine("Sending request:");
            Console.WriteLine(request);
            
            streams.Output.BlockingWriteAndFlush(Encoding.ASCII.GetBytes(request));

            List<byte> responseData = await ReadResponseAsync(streams.Input, cts.Token);

            string response = Encoding.ASCII.GetString([.. responseData]);
            
            Console.WriteLine($"Received data: {responseData.Count} bytes");
            Console.WriteLine(response);

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation timed out");
            return 1;
        }
        catch (WitException e)
        {
            Console.WriteLine($"WASI error: {e.Value}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return 1;
        }
    }

    public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}