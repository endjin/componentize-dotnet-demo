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
    public bool DataAvailable
    {
        get
        {
            logger?.Debug($"WasiSocketNmeaStreamReader:DataAvailable:{this.stream?.DataAvailable}");
            return this.stream?.DataAvailable ?? false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the reader is connected
    /// </summary>
    public bool Connected
    {
        get
        {
            logger?.Debug($"WasiSocketNmeaStreamReader:Connected:{this.tcpClient?.Connected}");
            return this.tcpClient?.Connected ?? false;
        }
    }

    /// <summary>
    /// Connects to the specified host and port
    /// </summary>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        this.logger.Debug($"Connecting to AIS source: {host}:{port}");

        try
        {
            this.tcpClient = new WasiTcpClient(logger);
            await this.tcpClient.ConnectAsync(host, port, cancellationToken);
            this.stream = this.tcpClient.GetStream();
            if (this.stream == null)
            {
                throw new InvalidOperationException("Failed to get network stream");
            }
            this.reader = new WasiStreamReader(this.stream.InputStream, logger);
            this.logger.Info("Connected successfully to AIS source");
        }
        catch (Exception ex)
        {
            this.logger.Error($"Connection failed: {ex.Message}");
            // If connection fails, clean up resources
            await this.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Reads a line of text from the stream
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        return this.reader is not null
            ? await this.reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// Disposes of resources used by this reader
    /// </summary>
    public ValueTask DisposeAsync()
    {
        reader?.Dispose();
        stream?.Dispose();
        tcpClient?.Dispose();

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}