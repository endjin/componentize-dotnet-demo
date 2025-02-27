using System.Text;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using Ais.Net.Receiver.Host.Wasi.Logging;
using Ais.Net.Receiver.Host.Wasi.Exceptions;

namespace Ais.Net.Receiver.Host.Wasi.IO;

/// <summary>
/// A TextReader-like class that reads characters from a WASI stream.
/// Similar to StreamReader, but works with WASI stream primitives.
/// </summary>
public class WasiStreamReader : IDisposable
{
    private readonly IStreams.InputStream inputStream;
    private readonly ILogger logger;
    private readonly MemoryStream buffer = new();
    private readonly Encoding encoding;
    private readonly int bufferSize;
    private bool isDisposed;

    /// <summary>
    /// A TextReader-like class that reads characters from a WASI stream.
    /// Similar to StreamReader, but works with WASI stream primitives.
    /// </summary>
    public WasiStreamReader(IStreams.InputStream inputStream, ILogger logger, Encoding? encoding = null, int bufferSize = 1024)
    {
        this.inputStream = inputStream;
        this.logger = logger;
        this.encoding = encoding ?? Encoding.UTF8;
        this.bufferSize = bufferSize;
    }

    /// <summary>
    /// Reads a line of text from the underlying stream.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        if (this.isDisposed)
        {
            throw new ObjectDisposedException(nameof(WasiStreamReader));
        }
        
        // Loop until we find a complete line or reach EOF
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we already have a complete line in the buffer
            byte[] bufferArray = buffer.ToArray();
            int newlineIndex = Array.IndexOf(bufferArray, (byte)'\n');
            
            if (newlineIndex >= 0)
            {
                // Extract the line
                string line = this.ExtractLineFromBuffer(bufferArray, newlineIndex);
                return line;
            }
            
            // Need to read more data
            byte[]? chunk = await this.ReadChunkAsync(cancellationToken);
            
            // If we couldn't read any more data, we're done
            if (chunk is null or { Length: 0 })
            {
                // Return any remaining data in buffer as the final line
                if (buffer.Length > 0)
                {
                    string finalLine = encoding.GetString(buffer.ToArray()).TrimEnd('\r', '\n');
                    buffer.SetLength(0);
                    return finalLine;
                }
                
                return null;
            }
            
            // Append the data to our buffer
            buffer.Position = buffer.Length;
            buffer.Write(chunk, 0, chunk.Length);
            buffer.Position = 0;
        }
        
        return null;
    }
    
    private string ExtractLineFromBuffer(byte[] bufferArray, int newlineIndex)
    {
        // Extract the line (including the newline character)
        byte[] lineBytes = new byte[newlineIndex + 1];
        Array.Copy(bufferArray, lineBytes, lineBytes.Length);
        
        // Remove this data from the buffer
        if (newlineIndex + 1 < bufferArray.Length)
        {
            byte[] remaining = new byte[bufferArray.Length - (newlineIndex + 1)];
            Array.Copy(bufferArray, newlineIndex + 1, remaining, 0, remaining.Length);
            buffer.SetLength(0);
            buffer.Write(remaining, 0, remaining.Length);
        }
        else
        {
            buffer.SetLength(0);
        }
        
        // Convert to string and trim trailing newline chars
        return encoding.GetString(lineBytes).TrimEnd('\r', '\n');
    }
    
    private async Task<byte[]?> ReadChunkAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try non-blocking read first
            try
            {
                byte[] data = inputStream.Read((ulong)bufferSize);
                if (data.Length > 0)
                {
                    return data;
                }
            }
            catch (WitException e) when (e.Value?.ToString()?.Contains("WOULD_BLOCK") == true)
            {
                this.logger.Error(e.Message);
            }
            
            // Try a short blocking read with timeout
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromMilliseconds(50));
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            
            try
            {
                byte[] data = inputStream.BlockingRead((ulong)bufferSize);
                if (data.Length > 0)
                {
                    return data;
                }
            }
            catch (OperationCanceledException e)
            {
                // Timeout is expected
                this.logger.Error(e.Message);
            }
            catch (WitException e) when (e.Value?.ToString()?.Contains("WOULD_BLOCK") == true)
            {
                this.logger.Error(e.Message);
            }
            
            // If we got here, we need to wait a bit and let the caller retry
            await Task.Delay(50, cancellationToken);
            return []; // Using collection expression for empty array
        }
        catch (WitException e)
        {
            this.logger.Error($"Error reading from stream: {e.Value}");
            return null;
        }
        catch (Exception e)
        {
            this.logger.Error($"Unexpected error reading from stream: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.buffer.Dispose();
            this.isDisposed = true;
        }
    }
}
