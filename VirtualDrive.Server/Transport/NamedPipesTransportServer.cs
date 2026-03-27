using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VirtualDrive.Server.Messages;
using VirtualDrive.Server.Services;

namespace VirtualDrive.Server.Transport;

/// <summary>
/// Named Pipes transport server for local/intranet interprocess communication
/// Supports Windows named pipes and Unix domain sockets
/// </summary>
public class NamedPipesTransportServer : IAsyncDisposable
{
    private readonly VirtualDriveService _service;
    private readonly TransportConfiguration _config;
    private readonly CancellationTokenSource _cts;
    private Task? _acceptTask;

    /// <summary>
    /// Initializes a new instance of the NamedPipesTransportServer class.
    /// </summary>
    /// <param name="service">The VirtualDrive service instance.</param>
    /// <param name="config">Transport configuration settings.</param>
    public NamedPipesTransportServer(VirtualDriveService service, TransportConfiguration config)
    {
        _service = service;
        _config = config;
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Start listening for client connections
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[NamedPipes] Starting server on pipe '{_config.NamedPipeName}'...");
        
        _acceptTask = AcceptClientConnectionsAsync(cancellationToken);
        await Task.Yield(); // Allow the task to start
        
        Console.WriteLine($"[NamedPipes] Server listening on '{_config.NamedPipeName}'");
    }

    /// <summary>
    /// Accept and handle incoming client connections
    /// </summary>
    private async Task AcceptClientConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(
                        _config.NamedPipeName,
                        PipeDirection.InOut,
                        _config.MaxConnections,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous))
                    {
                        Console.WriteLine($"[NamedPipes] Waiting for client connection...");
                        
                        await server.WaitForConnectionAsync(cancellationToken);
                        
                        Console.WriteLine($"[NamedPipes] Client connected!");
                        
                        // Handle client in background, continue accepting
                        _ = HandleClientAsync(server, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[NamedPipes] Error accepting connection: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Retry after delay
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[NamedPipes] Server shutdown");
        }
    }

    /// <summary>
    /// Handle individual client connection
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            using (var reader = new StreamReader(server))
            using (var writer = new StreamWriter(server) { AutoFlush = false })
            {
                while (!cancellationToken.IsCancellationRequested && server.IsConnected)
                {
                    try
                    {
                        // Read request JSON
                        var requestJson = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(requestJson))
                            break;

                        Console.WriteLine($"[NamedPipes] Received request: {requestJson.Substring(0, Math.Min(100, requestJson.Length))}...");

                        var response = await ProcessRequestAsync(requestJson);

                        // Write response JSON
                        var responseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson);
                        await writer.FlushAsync();

                        Console.WriteLine($"[NamedPipes] Sent response");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[NamedPipes] Error processing request: {ex.Message}");
                        var errorResponse = new RpcResponse
                        {
                            Success = false,
                            Operation = "Unknown",
                            Error = new RpcError
                            {
                                Code = "ProcessingError",
                                Message = ex.Message
                            }
                        };
                        var errorJson = JsonSerializer.Serialize(errorResponse);
                        await writer.WriteLineAsync(errorJson);
                        await writer.FlushAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NamedPipes] Client handler error: {ex.Message}");
        }
    }

    /// <summary>
    /// Process incoming RPC request
    /// </summary>
    private async Task<RpcResponse> ProcessRequestAsync(string requestJson)
    {
        try
        {
            using (var doc = JsonDocument.Parse(requestJson))
            {
                var root = doc.RootElement;
                string operation = root.GetProperty("operation").GetString() ?? "Unknown";
                string requestId = root.GetProperty("requestId").GetString() ?? Guid.NewGuid().ToString();

                return operation switch
                {
                    "CreateVolume" => await _service.CreateVolume(
                        JsonSerializer.Deserialize<CreateVolumeRequest>(requestJson) ?? new()),
                    
                    "DeleteVolume" => await _service.DeleteVolume(
                        requestId,
                        root.GetProperty("volumeName").GetString() ?? ""),
                    
                    "ListVolumes" => await _service.ListVolumes(
                        JsonSerializer.Deserialize<ListVolumesRequest>(requestJson) ?? new()),
                    
                    "WriteFile" => await _service.WriteFile(
                        JsonSerializer.Deserialize<WriteFileRequest>(requestJson) ?? new()),
                    
                    "ReadFile" => await _service.ReadFile(
                        JsonSerializer.Deserialize<ReadFileRequest>(requestJson) ?? new()),
                    
                    "ReadFileAt" => await _service.ReadFileAt(
                        JsonSerializer.Deserialize<ReadFileAtRequest>(requestJson) ?? new()),
                    
                    "WriteFileAt" => await _service.WriteFileAt(
                        JsonSerializer.Deserialize<WriteFileAtRequest>(requestJson) ?? new()),
                    
                    "DeleteFile" => await _service.DeleteFile(
                        JsonSerializer.Deserialize<DeleteFileRequest>(requestJson) ?? new()),
                    
                    "FileExists" => await _service.FileExists(
                        requestId,
                        root.GetProperty("volumeName").GetString() ?? "",
                        root.GetProperty("filePath").GetString() ?? ""),
                    
                    "GetFileInfo" => await _service.GetFileInfo(
                        requestId,
                        root.GetProperty("volumeName").GetString() ?? "",
                        root.GetProperty("filePath").GetString() ?? ""),
                    
                    "CreateDirectory" => await _service.CreateDirectory(
                        JsonSerializer.Deserialize<CreateDirectoryRequest>(requestJson) ?? new()),
                    
                    "DeleteDirectory" => await _service.DeleteDirectory(
                        requestId,
                        root.GetProperty("volumeName").GetString() ?? "",
                        root.GetProperty("dirPath").GetString() ?? ""),
                    
                    "ListFiles" => await _service.ListFiles(
                        JsonSerializer.Deserialize<ListFilesRequest>(requestJson) ?? new()),
                    
                    "GetCapacity" => await _service.GetCapacity(
                        JsonSerializer.Deserialize<GetCapacityRequest>(requestJson) ?? new()),
                    
                    _ => new RpcResponse
                    {
                        RequestId = requestId,
                        Success = false,
                        Operation = operation,
                        Error = new RpcError
                        {
                            Code = "UnknownOperation",
                            Message = $"Unknown operation: {operation}"
                        }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            return new RpcResponse
            {
                Success = false,
                Operation = "Unknown",
                Error = new RpcError
                {
                    Code = "ParsingError",
                    Message = $"Failed to parse request: {ex.Message}"
                }
            };
        }
    }

    /// <summary>
    /// Stop the server
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("[NamedPipes] Stopping server...");
        _cts.Cancel();
        
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Stops the server and releases allocated resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts.Dispose();
    }
}
