using Ais.Net.Receiver.Host.Wasi.IO;
using Ais.Net.Receiver.Host.Wasi.Logging;
using Ais.Net.Receiver.Receiver;

﻿using Ais.Net.Receiver.Receiver;

using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ais.Net.Receiver.Host.Wasi;

/// <summary>
/// A WASI-based implementation of INmeaStreamReader that closely mimics TcpClientNmeaStreamReader
/// </summary>
public class WasiSocketNmeaStreamReader(ILogger? logger = null) : INmeaStreamReader
{
    private readonly ILogger logger = logger ?? new ConsoleLogger();
    private WasiTcpClient? tcpClient;
    private WasiNetworkStream? stream;
    private WasiStreamReader? reader;
    
    public bool DataAvailable => stream?.DataAvailable ?? false;
    public bool Connected => tcpClient?.Connected ?? false;
    
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        this.tcpClient = new WasiTcpClient(logger);
        await this.tcpClient.ConnectAsync(host, port, cancellationToken);
        this.stream = tcpClient.GetStream();
        this.reader = new WasiStreamReader(stream!.InputStream, logger, Encoding.ASCII);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (this.reader == null) return null;
        
        string? line = await reader.ReadLineAsync(cancellationToken);

        return line;
    }

    public ValueTask DisposeAsync()
    {
        reader?.Dispose();
        stream?.Dispose();
        tcpClient?.Dispose();

        return ValueTask.CompletedTask;
    }
}