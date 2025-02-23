using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_0;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_0;
using System.Collections.Generic;

// wasmtime -S tcp=y,allow-ip-name-lookup=y,inherit-network=y,network-error-code=y .\dist\wasi-sockets.wasm 

public static class WasiMainWrapper
{
    // Helper to convert IPAddress to a tuple of four bytes.
    private static (byte, byte, byte, byte) GetIpTuple(IPAddress ip)
    {
        byte[] bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
            throw new ArgumentException("Only IPv4 addresses are supported.");
        return (bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    public static Task<int> MainAsync(string[] args)
    {
        try
        {
            // For example.com use a matching IP address (93.184.216.34)
            Console.WriteLine("Create INetwork.Ipv4SocketAddress");
            //INetwork.Ipv4SocketAddress address = new(5631, GetIpTuple(IPAddress.Parse("153.44.253.27")));
            INetwork.Ipv4SocketAddress address = new(80, GetIpTuple(IPAddress.Parse("96.7.128.175")));

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
                    Thread.Sleep(100); // Avoid tight loop
                }
            }

            Console.WriteLine("Connected to the server via WASI sockets.");

            // Formulate a complete HTTP/1.1 request with matching Host header.
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
            List<byte> responseData = new List<byte>();
            bool reading = true;

            while (reading)
            {
                try
                {
                    var chunk = streams.inputStream!.BlockingRead(READ_SIZE);
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
                    Thread.Sleep(100); // Delay before retrying
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
        catch(WitException e)
        {
            Console.WriteLine($"WASI error: {e.Value}");
            return Task.FromResult(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return Task.FromResult(1);
        }

        return Task.FromResult(0);
    }

    public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, 
            Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
