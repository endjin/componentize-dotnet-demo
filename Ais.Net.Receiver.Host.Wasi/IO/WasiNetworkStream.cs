using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using Ais.Net.Receiver.Host.Wasi.Logging;
using Ais.Net.Receiver.Host.Wasi.Exceptions;

namespace Ais.Net.Receiver.Host.Wasi.IO;

/// <summary>
/// A NetworkStream-like class that works with WASI stream primitives
/// </summary>
public class WasiNetworkStream : IDisposable
{
    private readonly IStreams.InputStream inputStream;
    private bool isDisposed;

    /// <summary>
    /// A NetworkStream-like class that works with WASI stream primitives
    /// </summary>
    public WasiNetworkStream(IStreams.InputStream inputStream)
    {
        this.inputStream = inputStream;
    }

    public bool DataAvailable
    {
        get
        {
            if (this.isDisposed) return false;
            
            try
            {
                // Try a non-blocking read of 0 bytes
                byte[] data = this.inputStream.Read(0);
                return true; // If we get here, data is available
            }
            catch (WitException)
            {
                return false; // If we get an exception, no data is available
            }
        }
    }

    public IStreams.InputStream InputStream => inputStream;
    
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.inputStream.Dispose();
            this.isDisposed = true;
        }
    }
}