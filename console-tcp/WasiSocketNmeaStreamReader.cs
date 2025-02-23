using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Ais.Net.Receiver.Receiver;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;
using System.Buffers;
using System.IO;

public class WasiSocketNmeaStreamReader : INmeaStreamReader
{
    private (IStreams.InputStream Input, IStreams.OutputStream Output) streams;
    public bool DataAvailable { get; private set; }

    public bool Connected { get; private set; }

    private readonly MemoryStream _buffer = new();

    public ValueTask DisposeAsync()
    {
        Console.WriteLine("DisposeAsync");

        this.streams.Input.Dispose();
        this.streams.Output.Dispose();

        return ValueTask.CompletedTask;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("Create INetwork.Ipv4SocketAddress");

            IPv4Bytes ipBytes = GetIpTuple(IPAddress.Parse(host));
            INetwork.Ipv4SocketAddress address = new((ushort)port, (ipBytes.A, ipBytes.B, ipBytes.C, ipBytes.D));

            Console.WriteLine("Create remoteAddress");

            INetwork.IpSocketAddress remoteAddress = INetwork.IpSocketAddress.Ipv4(address);

            Console.WriteLine("InstanceNetwork");

            INetwork.Network network = InstanceNetworkInterop.InstanceNetwork();
            
            this.streams = await TcpSocketConnectAsync(network, remoteAddress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation timed out");
        }
        catch (WitException e)
        {
            Console.WriteLine($"WASI error: {e.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (byte[] chunk in ReadChunksAsync(this.streams.Input, cancellationToken))
            {
                this.DataAvailable = true;
                
                // Write chunk to buffer
                long originalPosition = _buffer.Position;
                _buffer.Position = _buffer.Length;
                _buffer.Write(chunk, 0, chunk.Length);
                _buffer.Position = originalPosition;

                // Look for complete line
                byte[] bufferArray = _buffer.ToArray();
                int newlineIndex = Array.IndexOf(bufferArray, (byte)'\n');
                
                if (newlineIndex >= 0)
                {
                    // Extract complete line
                    byte[] lineBytes = new byte[newlineIndex + 1];
                    Array.Copy(bufferArray, lineBytes, newlineIndex + 1);
                    string completeLine = Encoding.ASCII.GetString(lineBytes);

                    // Keep remaining data in buffer
                    int remainingLength = bufferArray.Length - (newlineIndex + 1);
                    if (remainingLength > 0)
                    {
                        byte[] remaining = new byte[remainingLength];
                        Array.Copy(bufferArray, newlineIndex + 1, remaining, 0, remainingLength);
                        _buffer.SetLength(0);
                        _buffer.Write(remaining, 0, remaining.Length);
                    }
                    else
                    {
                        _buffer.SetLength(0);
                    }

                    return completeLine.TrimEnd('\r', '\n');
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading line: {ex.Message}");
            this.DataAvailable = false;
            this.Connected = false;
        }

        return null;
    }

    private async Task<(IStreams.InputStream Input, IStreams.OutputStream Output)> TcpSocketConnectAsync(INetwork.Network network, INetwork.IpSocketAddress remoteAddress, CancellationToken cancellationToken = default)
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
                (IStreams.InputStream, IStreams.OutputStream) streams = tcpSocket.FinishConnect();
                this.Connected = true;
                this.DataAvailable = true;
                Console.WriteLine("Connection established!");

                return streams;
            }
            catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
            {
                Console.WriteLine("Connection in progress, waiting...");
                this.Connected = false;
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async IAsyncEnumerable<byte[]> ReadChunksAsync(IStreams.InputStream inputStream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int READ_SIZE = 4096; // Increased buffer size
        byte[] chunk = ArrayPool<byte>.Shared.Rent(READ_SIZE);
        int retryCount = 0;
        const int MAX_RETRIES = 3;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] data;
                try
                {
                    this.DataAvailable = true;
                    data = inputStream.BlockingRead((ulong)READ_SIZE);
                    
                    if (data.Length == 0)
                    {
                        if (++retryCount > MAX_RETRIES)
                        {
                            Console.WriteLine("No data received after max retries");
                            yield break;
                        }
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }
                    
                    retryCount = 0; // Reset retry counter on successful read
                }
                catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
                {
                    if (++retryCount > MAX_RETRIES)
                    {
                        Console.WriteLine("Max retries exceeded while waiting for data");
                        this.DataAvailable = false;
                        yield break;
                    }
                    Console.WriteLine($"Waiting for more data... (retry {retryCount})");
                    this.DataAvailable = true;
                    await Task.Delay(100 * retryCount, cancellationToken); // Exponential backoff
                    continue;
                }
                catch (WitException e) when (e.Value is IStreams.StreamError)
                {
                    Console.WriteLine($"Stream error: {e.Value}");
                    this.DataAvailable = false;
                    this.Connected = false;
                    yield break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                    this.DataAvailable = false;
                    this.Connected = false;
                    yield break;
                }

                var result = new byte[data.Length];
                Array.Copy(data, result, data.Length);
                yield return result;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private readonly record struct IPv4Bytes(byte A, byte B, byte C, byte D)
    {
        public void Deconstruct(out (byte, byte, byte, byte) tuple) => tuple = (A, B, C, D);
    }

    private static IPv4Bytes GetIpTuple(IPAddress ip) => ip.GetAddressBytes() is [var a, var b, var c, var d]
        ? new IPv4Bytes(a, b, c, d)
        : throw new ArgumentException("Only IPv4 addresses are supported.");
}