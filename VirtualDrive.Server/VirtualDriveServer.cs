using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualDrive.Core;
using VirtualDrive.Server.Services;

namespace VirtualDrive.Server.Transport;

/// <summary>
/// Unified VirtualDrive server host that manages transport layers
/// Supports simultaneous operation of multiple transport protocols
/// </summary>
public class VirtualDriveServer : IAsyncDisposable
{
    private readonly VirtualDriveService _service;
    private readonly TransportConfiguration _config;
    private NamedPipesTransportServer? _namedPipesServer;
    private GrpcHttpTransportServer? _grpcHttpServer;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Initializes a new instance of the VirtualDriveServer class.
    /// </summary>
    /// <param name="config">Transport configuration. Uses default if null.</param>
    /// <param name="bufferConfig">Memory buffer configuration. Uses default if null.</param>
    public VirtualDriveServer(TransportConfiguration config, MemoryBufferConfiguration? bufferConfig = null)
    {
        _config = config ?? TransportConfigurationFactory.CreateDefault();
        _service = new VirtualDriveService(bufferConfig ?? MemoryBufferConfiguration.CreateDefault());
    }

    /// <summary>
    /// Create server with default configuration
    /// </summary>
    public static VirtualDriveServer CreateDefault(MemoryBufferConfiguration? bufferConfig = null)
    {
        return new VirtualDriveServer(TransportConfigurationFactory.CreateDefault(), bufferConfig);
    }

    /// <summary>
    /// Create server for local Named Pipes access
    /// </summary>
    public static VirtualDriveServer CreateLocal(
        string pipeName = "VirtualDrive.Server",
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return new VirtualDriveServer(
            TransportConfigurationFactory.CreateLocalPipes(pipeName),
            bufferConfig ?? MemoryBufferConfiguration.CreateDefault());
    }

    /// <summary>
    /// Create server for HTTP/gRPC access
    /// </summary>
    public static VirtualDriveServer CreateNetwork(
        string httpEndpoint = "http://0.0.0.0:5051",
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return new VirtualDriveServer(
            TransportConfigurationFactory.CreateGrpcHttp(httpEndpoint),
            bufferConfig ?? MemoryBufferConfiguration.CreateDefault());
    }

    /// <summary>
    /// Create server for high-throughput scenarios
    /// </summary>
    public static VirtualDriveServer CreateHighThroughput(MemoryBufferConfiguration? bufferConfig = null)
    {
        return new VirtualDriveServer(
            TransportConfigurationFactory.CreateHighThroughput(),
            bufferConfig ?? MemoryBufferConfiguration.CreateDefault());
    }

    /// <summary>
    /// Start the VirtualDrive server with configured transport(s)
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n╔══════════════════════════════════════╗");
        Console.WriteLine($"║   VirtualDrive.Core Server v1.0.0    ║");
        Console.WriteLine($"║   Protocol: {_config.Protocol,-24} ║");
        Console.WriteLine($"╚══════════════════════════════════════╝\n");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Start configured transport server(s)
            if (_config.Protocol == TransportProtocol.NamedPipes)
            {
                _namedPipesServer = new NamedPipesTransportServer(_service, _config);
                await _namedPipesServer.StartAsync(_cts.Token);
            }
            else if (_config.Protocol == TransportProtocol.GrpcHttp2)
            {
                _grpcHttpServer = new GrpcHttpTransportServer(_service, _config);
                await _grpcHttpServer.StartAsync(_cts.Token);
            }

            Console.WriteLine("\n[Server] Ready to accept connections. Press Ctrl+C to stop.\n");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Server] Failed to start: {ex.Message}\n");
            throw;
        }
    }

    /// <summary>
    /// Stop the server and clean up resources
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("\n[Server] Shutting down...");

        _cts?.Cancel();

        if (_namedPipesServer != null)
        {
            await _namedPipesServer.StopAsync();
        }

        if (_grpcHttpServer != null)
        {
            await _grpcHttpServer.StopAsync();
        }

        Console.WriteLine("[Server] Shutdown complete.");
    }

    /// <summary>
    /// Run the server until cancellation
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellationToken is cancelled
        }
        finally
        {
            await StopAsync();
        }
    }

    /// <summary>
    /// Get the underlying VirtualDriveService instance
    /// </summary>
    public VirtualDriveService Service => _service;

    /// <summary>
    /// Get the transport configuration
    /// </summary>
    public TransportConfiguration Configuration => _config;

    /// <summary>
    /// Stops the server and releases allocated resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _service.Dispose();
        if (_namedPipesServer != null)
        {
            await _namedPipesServer.DisposeAsync();
        }
        if (_grpcHttpServer != null)
        {
            await _grpcHttpServer.DisposeAsync();
        }
        _cts?.Dispose();
    }
}
