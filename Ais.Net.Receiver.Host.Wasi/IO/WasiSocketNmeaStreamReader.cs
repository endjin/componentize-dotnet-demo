using Ais.Net.Receiver.Host.Wasi.Logging;
using Ais.Net.Receiver.Receiver;

namespace Ais.Net.Receiver.Host.Wasi.IO;

/// <summary>
/// WASI-compatible implementation of INmeaStreamReader that uses WASI socket primitives
/// </summary>
public class WasiSocketNmeaStreamReader : INmeaStreamReader
{
    private readonly ILogger logger;
    private WasiTcpClient? tcpClient;
    private WasiNetworkStream? stream;
    private WasiStreamReader? reader;

    public WasiSocketNmeaStreamReader(ILogger? logger = null)
    {
        this.logger = logger ?? new ConsoleLogger();
    }

    /// <summary>
    /// Gets a value indicating whether data is available to read
    /// </summary>
    public bool DataAvailable => stream?.DataAvailable ?? false;

    /// <summary>
    /// Gets a value indicating whether the reader is connected
    /// </summary>
    public bool Connected => tcpClient?.Connected ?? false;

    /// <summary>
    /// Connects to the specified host and port
    /// </summary>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        this.logger.Info($"Connecting to AIS source: {host}:{port}");
        
        this.tcpClient = new WasiTcpClient(logger);
        await this.tcpClient.ConnectAsync(host, port, cancellationToken);
        
        this.stream = this.tcpClient.GetStream();
        if (this.stream == null)
        {
            throw new InvalidOperationException("Failed to get network stream");
        }
        
        this.reader = new WasiStreamReader(stream.InputStream, logger);
        
        this.logger.Info("Connected successfully to AIS source");
    }

    /// <summary>
    /// Reads a line of text from the stream
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (this.reader is null)
        {
            return null;
        }

        try 
        {
            return await this.reader.ReadLineAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.Error($"Error reading from NMEA stream: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Disposes of resources used by this reader
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (this.reader != null)
        {
            this.reader.Dispose();
            this.reader = null;
        }

        if (stream != null)
        {
            this.stream.Dispose();
            this.stream = null;
        }

        if (this.tcpClient != null)
        {
            this.tcpClient.Dispose();
            this.tcpClient = null;
        }

        return ValueTask.CompletedTask;
    }
}
