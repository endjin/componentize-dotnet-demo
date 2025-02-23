using System.Net;
using System.Text;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_0;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_0;
using System.Runtime.CompilerServices;

// wasmtime -S tcp=y,allow-ip-name-lookup=y,inherit-network=y,network-error-code=y .\dist\wasi-sockets.wasm 

namespace WasiMainWrapper;

public static class Program
{
    // A lightweight struct to hold IPv4 bytes.
    private readonly record struct IPv4Bytes(byte A, byte B, byte C, byte D);

    private static IPv4Bytes GetIpTuple(IPAddress ip)
    {
        byte[] bytes = ip.GetAddressBytes();

        if (bytes.Length != 4)
        {
            throw new ArgumentException("Only IPv4 addresses are supported.");
        }

        return new IPv4Bytes(bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            Console.WriteLine("Create INetwork.Ipv4SocketAddress");

            // Deconstruct the new record struct when creating the IPv4 address
            var (a, b, c, d) = GetIpTuple(IPAddress.Parse("96.7.128.175"));
            INetwork.Ipv4SocketAddress address = new(80, (a, b, c, d));

            Console.WriteLine("Create remoteAddress");
            INetwork.IpSocketAddress remoteAddress = INetwork.IpSocketAddress.Ipv4(address);

            Console.WriteLine("InstanceNetwork");
            INetwork.Network network = InstanceNetworkInterop.InstanceNetwork();

            Console.WriteLine("CreateTcpSocket");
            ITcp.TcpSocket tcpSocket = TcpCreateSocketInterop.CreateTcpSocket(INetwork.IpAddressFamily.IPV4);

            Console.WriteLine("Starting connection...");
            tcpSocket.StartConnect(network, remoteAddress);

            Console.WriteLine("Waiting for connection to complete...");
            bool connected = false;
            (IStreams.InputStream inputStream, IStreams.OutputStream outputStream) streams = default;

            while (!connected)
            {
                try
                {
                    streams = tcpSocket.FinishConnect();
                    connected = true;
                    Console.WriteLine("Connection established!");
                }
                catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
                {
                    Console.WriteLine("Connection in progress, waiting...");
                    await Task.Delay(100); // Avoid tight loop
                }
            }

            Console.WriteLine("Connected to the server via WASI sockets.");

            // Use a raw string literal to simplify a multi-line string.
            string request = 
                "GET / HTTP/1.1\r\n" +
                "Host: example.com\r\n" +
                "User-Agent: WASI-Client/1.0\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] requestBytes = Encoding.ASCII.GetBytes(request);

            Console.WriteLine("Sending request:");
            Console.WriteLine(request);
            streams.outputStream!.BlockingWriteAndFlush(requestBytes);

            // Read response with retry logic.
            const ulong READ_SIZE = 1024UL;
            List<byte> responseData = [];

            bool reading = true;
            while (reading)
            {
                try
                {
                    byte[] chunk = streams.inputStream!.BlockingRead(READ_SIZE);
                    if (chunk.Length == 0)
                    {
                        // End of stream: server closed connection.
                        reading = false;
                    }
                    else
                    {
                        responseData.AddRange(chunk);
                        Console.WriteLine($"Received chunk with {chunk.Length} bytes");
                    }
                }
                catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
                {
                    Console.WriteLine("Waiting for more data...");
                    await Task.Delay(100); // Delay before retrying
                }
                catch (WitException e) when (e.Value is IStreams.StreamError)
                {
                    break;
                }
            }

            // Convert accumulated bytes to a string.
            string response = Encoding.ASCII.GetString(responseData.ToArray());
            Console.WriteLine($"Received data: {responseData.Count} bytes");
            Console.WriteLine(response);
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

        return 0;
    }

    public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}