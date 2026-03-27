using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using VirtualDrive.Server.Messages;
using VirtualDrive.Server.Services;

namespace VirtualDrive.Server.Transport;

/// <summary>
/// HTTP/gRPC transport server for network interprocess communication
/// Supports local, intranet, and internet access
/// </summary>
public class GrpcHttpTransportServer : IAsyncDisposable
{
    private readonly VirtualDriveService _service;
    private readonly TransportConfiguration _config;
    private WebApplication? _app;
    private Task? _runTask;

    /// <summary>
    /// Initializes a new instance of the GrpcHttpTransportServer class.
    /// </summary>
    /// <param name="service">The VirtualDrive service instance.</param>
    /// <param name="config">Transport configuration settings.</param>
    public GrpcHttpTransportServer(VirtualDriveService service, TransportConfiguration config)
    {
        _service = service;
        _config = config;
    }

    /// <summary>
    /// Start the HTTP/gRPC server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[GrpcHttp] Starting server on {_config.HttpEndpoint}...");

        var builder = WebApplication.CreateBuilder();

        // Configure services
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(_service);

        // Configure endpoints
        builder.WebHost.UseUrls(_config.HttpEndpoint.Split(';'));

        _app = builder.Build();

        // Map gRPC and HTTP endpoints
        MapEndpoints(_app);

        // Start server without blocking
        _runTask = Task.Run(async () => await _app.RunAsync(), cancellationToken);
        await Task.Delay(100, cancellationToken); // Give server time to start

        Console.WriteLine($"[GrpcHttp] Server listening on {_config.HttpEndpoint}");
    }

    /// <summary>
    /// Map HTTP REST endpoints for VirtualDrive operations
    /// </summary>
    private void MapEndpoints(WebApplication app)
    {
        const string basePath = "/api/virtualdrive";

        // Volume operations
        app.MapPost($"{basePath}/volumes/create", CreateVolume);
        app.MapDelete($"{basePath}/volumes/{{volumeName}}", DeleteVolume);
        app.MapGet($"{basePath}/volumes", ListVolumes);

        // File operations
        app.MapPost($"{basePath}/files/write", WriteFile);
        app.MapPost($"{basePath}/files/read", ReadFile);
        app.MapPost($"{basePath}/files/read-at", ReadFileAt);
        app.MapPost($"{basePath}/files/write-at", WriteFileAt);
        app.MapDelete($"{basePath}/files", DeleteFile);
        app.MapGet($"{basePath}/files/exists", FileExists);
        app.MapGet($"{basePath}/files/info", GetFileInfo);

        // Directory operations
        app.MapPost($"{basePath}/directories/create", CreateDirectory);
        app.MapDelete($"{basePath}/directories", DeleteDirectory);
        app.MapGet($"{basePath}/directories/list", ListFiles);

        // Capacity operations
        app.MapGet($"{basePath}/capacity", GetCapacity);

        // Health check
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
    }

    // ==================== Endpoint Handlers ====================

    private async Task CreateVolume(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<CreateVolumeRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.CreateVolume(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task DeleteVolume(HttpContext context, string volumeName, VirtualDriveService service)
    {
        var requestId = context.Request.Query["requestId"].ToString() ?? Guid.NewGuid().ToString();
        var response = await service.DeleteVolume(requestId, volumeName);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task ListVolumes(HttpContext context, VirtualDriveService service)
    {
        var requestId = context.Request.Query["requestId"].ToString() ?? Guid.NewGuid().ToString();
        var response = await service.ListVolumes(new ListVolumesRequest { RequestId = requestId });
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task WriteFile(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<WriteFileRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.WriteFile(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task ReadFile(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<ReadFileRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.ReadFile(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task ReadFileAt(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<ReadFileAtRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.ReadFileAt(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task WriteFileAt(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<WriteFileAtRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.WriteFileAt(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task DeleteFile(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<DeleteFileRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.DeleteFile(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task FileExists(HttpContext context, VirtualDriveService service)
    {
        var volumeName = context.Request.Query["volumeName"].ToString();
        var filePath = context.Request.Query["filePath"].ToString();

        if (string.IsNullOrEmpty(volumeName) || string.IsNullOrEmpty(filePath))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var requestId = context.Request.Query["requestId"].ToString() ?? Guid.NewGuid().ToString();
        var response = await service.FileExists(requestId, volumeName, filePath);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task GetFileInfo(HttpContext context, VirtualDriveService service)
    {
        var volumeName = context.Request.Query["volumeName"].ToString();
        var filePath = context.Request.Query["filePath"].ToString();

        if (string.IsNullOrEmpty(volumeName) || string.IsNullOrEmpty(filePath))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var requestId = context.Request.Query["requestId"].ToString() ?? Guid.NewGuid().ToString();
        var response = await service.GetFileInfo(requestId, volumeName, filePath);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task CreateDirectory(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<CreateDirectoryRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.CreateDirectory(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task DeleteDirectory(HttpContext context, VirtualDriveService service)
    {
        var volumeName = context.Request.Query["volumeName"].ToString();
        var dirPath = context.Request.Query["dirPath"].ToString();

        if (string.IsNullOrEmpty(volumeName) || string.IsNullOrEmpty(dirPath))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var requestId = context.Request.Query["requestId"].ToString() ?? Guid.NewGuid().ToString();
        var response = await service.DeleteDirectory(requestId, volumeName, dirPath);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task ListFiles(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<ListFilesRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.ListFiles(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private async Task GetCapacity(HttpContext context, VirtualDriveService service)
    {
        var request = await context.Request.ReadFromJsonAsync<GetCapacityRequest>();
        if (request == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var response = await service.GetCapacity(request);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Stop the server
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("[GrpcHttp] Stopping server...");
        if (_app != null)
        {
            await _app.StopAsync();
        }
    }

    /// <summary>
    /// Stops the server and releases allocated resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_app != null)
        {
            await ((IAsyncDisposable)_app).DisposeAsync();
        }
    }
}
