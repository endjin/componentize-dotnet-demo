using Ais.Net.Receiver.Receiver;

using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ais.Net.Receiver.Host.Wasi;

public class WasiSocketNmeaStreamReader : INmeaStreamReader
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(1);
    private const int ReadSize = 4096;
    private const int MaxRetries = 3;
    private (IStreams.InputStream Input, IStreams.OutputStream Output) streams;
    private readonly MemoryStream buffer = new();
    
    public bool DataAvailable { get; private set; }

    public bool Connected { get; private set; }

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
                long originalPosition = buffer.Position;
                buffer.Position = buffer.Length;
                buffer.Write(chunk, 0, chunk.Length);
                buffer.Position = originalPosition;

                // Look for complete line
                byte[] bufferArray = buffer.ToArray();
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
                        buffer.SetLength(0);
                        buffer.Write(remaining, 0, remaining.Length);
                    }
                    else
                    {
                        buffer.SetLength(0);
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

    public ValueTask DisposeAsync()
    {
        Console.WriteLine("DisposeAsync");

        this.streams.Input.Dispose();
        this.streams.Output.Dispose();

        return ValueTask.CompletedTask;
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
        using ByteArrayPoolBuffer arrayPoolBuffer = new(ReadSize);
        try
        {
            RetryState retryState = new();

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await TryReadDataAsync(inputStream, arrayPoolBuffer.Buffer, retryState, cancellationToken);

                if (result is { ErrorMessage: not null } msg)
                {
                    Console.WriteLine(msg.ErrorMessage);
                }

                if (result is { Data: not null } data)
                {
                    yield return data.Data;
                }

                if (result.ShouldBreak)
                {
                    yield break;
                }
            }
        }
        finally
        {
            // Dispose is called automatically due to using statement
        }
    }

    private sealed class RetryState
    {
        public int Count { get; set; }
        public void Reset() => Count = 0;
        public bool IncrementAndCheckLimit() => ++Count > MaxRetries;
    }

    private async Task<ReadResult> TryReadDataAsync(IStreams.InputStream inputStream, byte[] buffer, RetryState retryState, CancellationToken cancellationToken)
    {
        try
        {
            if (!this.Connected)
                return new ReadResult(null, true, "Connection lost, stopping read");

            this.DataAvailable = true;
            byte[] data = inputStream.BlockingRead((ulong)buffer.Length);
            
            return data.Length switch
            {
                0 when retryState.IncrementAndCheckLimit() => 
                    new ReadResult(null, true, "No data received after max retries") { Connected = false },
                
                0 => await HandleWaitForMoreDataAsync(retryState, cancellationToken),
                
                _ => HandleSuccessfulRead(data, retryState)
            };
        }
        catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
        {
            if (retryState.IncrementAndCheckLimit())
            {
                this.DataAvailable = false;
                this.Connected = false;

                return new ReadResult(null, true, "Max retries exceeded while waiting for data");
            }

            this.DataAvailable = true;
            TimeSpan delay = TimeSpan.FromMilliseconds(Math.Min(
                InitialRetryDelay.TotalMilliseconds * (1 << retryState.Count),
                MaxRetryDelay.TotalMilliseconds));
            
            await Task.Delay(delay, cancellationToken);
            return new ReadResult(null, false, $"Waiting for more data... (retry {retryState.Count})");
        }
        catch (WitException e) when (e.Value is IStreams.StreamError)
        {
            this.DataAvailable = false;
            this.Connected = false;
            
            if (this.buffer.Length > 0)
            {
                var remainingData = this.buffer.ToArray();
                this.buffer.SetLength(0);
                return new ReadResult(remainingData, true, $"Stream error: {e.Value}. Processing remaining {remainingData.Length} bytes before disconnecting");
            }
            
            return new ReadResult(null, true, $"Stream error: {e.Value}");
        }
        catch (Exception ex)
        {
            this.DataAvailable = false;
            this.Connected = false;
            return new ReadResult(null, true, $"Unexpected error: {ex.Message}");
        }
    }

    private ReadResult HandleSuccessfulRead(byte[] data, RetryState retryState)
    {
        retryState.Reset();
        byte[] result = [.. data]; // Using collection expression for copying
        return new ReadResult(result, false, null);
    }

    private async Task<ReadResult> HandleWaitForMoreDataAsync(RetryState retryState, CancellationToken cancellationToken)
    {
        await Task.Delay(InitialRetryDelay, cancellationToken);
        return new ReadResult(null, false, "Waiting for more data...");
    }

    private readonly record struct IPv4Bytes(byte A, byte B, byte C, byte D)
    {
        public void Deconstruct(out (byte, byte, byte, byte) tuple) => tuple = (A, B, C, D);
    }

    private static IPv4Bytes GetIpTuple(IPAddress ip) => ip.GetAddressBytes() is [var a, var b, var c, var d]
        ? new IPv4Bytes(a, b, c, d)
        : throw new ArgumentException("Only IPv4 addresses are supported.");

    private readonly record struct ReadResult(byte[]? Data, bool ShouldBreak, string? ErrorMessage)
    {
        public bool Connected { get; init; } = true;
    }
}

// Change the generic ArrayPoolBuffer to a specific ByteArrayPoolBuffer
file sealed class ByteArrayPoolBuffer(int size) : IDisposable
{
    public byte[] Buffer { get; } = ArrayPool<byte>.Shared.Rent(size);

    public void Dispose() => ArrayPool<byte>.Shared.Return(Buffer);
}