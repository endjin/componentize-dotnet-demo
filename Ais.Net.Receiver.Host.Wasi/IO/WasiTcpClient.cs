using System.Net;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;
using Ais.Net.Receiver.Host.Wasi.Logging;

namespace Ais.Net.Receiver.Host.Wasi.IO;

/// <summary>
/// A TcpClient-like class that works with WASI socket primitives
/// </summary>
public class WasiTcpClient : IDisposable
{
    private readonly ILogger logger;
    private (IStreams.InputStream Input, IStreams.OutputStream Output)? streams;

    /// <summary>
    /// A TcpClient-like class that works with WASI socket primitives
    /// </summary>
    public WasiTcpClient(ILogger logger)
    {
        this.logger = logger;
    }

    public bool Connected => this.streams.HasValue;
    
    public WasiNetworkStream? GetStream() => 
        this.streams.HasValue ? new WasiNetworkStream(streams.Value.Input) : null;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        this.logger.Info($"Connecting to {host}:{port}");
        
        try
        {
            // Parse IP address
            IPAddress ipAddress = IPAddress.Parse(host);
            byte[] addressBytes = ipAddress.GetAddressBytes();
            
            if (addressBytes.Length != 4)
            {
                throw new ArgumentException("Only IPv4 addresses are supported");
            }
            
            // Create socket address
            INetwork.Ipv4SocketAddress address = new((ushort)port, (addressBytes[0], addressBytes[1], addressBytes[2], addressBytes[3]));
            INetwork.IpSocketAddress remoteAddress = INetwork.IpSocketAddress.Ipv4(address);
            
            // Get network instance
            INetwork.Network network = InstanceNetworkInterop.InstanceNetwork();
            
            // Create socket
            ITcp.TcpSocket tcpSocket = TcpCreateSocketInterop.CreateTcpSocket(INetwork.IpAddressFamily.IPV4);
            
            // Connect
            tcpSocket.StartConnect(network, remoteAddress);
            
            logger.Debug("Waiting for connection to complete...");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    streams = tcpSocket.FinishConnect();
                    logger.Info("Connection established");
                    return;
                }
                catch (WitException e) when (e.Value.ToString()!.Contains("WOULD_BLOCK"))
                {
                    logger.Error("Connection in progress, waiting...");
                    await Task.Delay(100, cancellationToken);
                }
            }
            
            throw new OperationCanceledException("Connection attempt was canceled");
        }
        catch (Exception ex)
        {
            logger.Critical($"Connection error: {ex.Message}", ex);
            throw;
        }
    }

    public void Dispose()
    {
        if (streams.HasValue)
        {
            streams.Value.Input?.Dispose();
            streams.Value.Output?.Dispose();
            streams = null;
        }
        GC.SuppressFinalize(this);
    }
}
