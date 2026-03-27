using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDrive.Client.Abstractions;
using VirtualDrive.Server.Messages;

namespace VirtualDrive.Client.NamedPipes;

/// <summary>
/// Client for Named Pipes transport
/// Low-latency local and intranet communication with VirtualDrive server
/// </summary>
public class NamedPipesClient : ITransportClient
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipeStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly object _lockObject = new();

    public NamedPipesClient(string pipeName = "VirtualDrive.Server")
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Connect to the Named Pipes server
    /// </summary>
    public async Task ConnectAsync(int timeoutMs = 5000)
    {
        try
        {
            _pipeStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            await _pipeStream.ConnectAsync(timeoutMs);

            _reader = new StreamReader(_pipeStream);
            _writer = new StreamWriter(_pipeStream) { AutoFlush = false };

            Console.WriteLine($"[NamedPipes Client] Connected to '{_pipeName}'");
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException($"Failed to connect to Named Pipe '{_pipeName}' within {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to Named Pipe '{_pipeName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Send request and receive response
    /// </summary>
    private async Task<RpcResponse> SendRequestAsync(RpcMessage message)
    {
        if (_writer == null || _reader == null)
            throw new InvalidOperationException("Not connected to server");

        lock (_lockObject)
        {
            var json = JsonSerializer.Serialize(message);
            _writer.WriteLine(json);
            _writer.Flush();
        }

        var responseJson = await _reader.ReadLineAsync();
        if (string.IsNullOrEmpty(responseJson))
            throw new InvalidOperationException("Server closed connection");

        var response = JsonSerializer.Deserialize<RpcResponse>(responseJson);
        if (response == null)
            throw new InvalidOperationException("Failed to deserialize response");

        return response;
    }

    // ==================== Volume Operations ====================

    public async Task CreateVolumeAsync(string volumeName, long capacityMB)
    {
        var request = new CreateVolumeRequest { VolumeName = volumeName, CapacityMB = capacityMB };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task DeleteVolumeAsync(string volumeName)
    {
        var request = new DeleteVolumeRequest { VolumeName = volumeName };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task<List<VolumeInfoDto>> ListVolumesAsync()
    {
        var request = new ListVolumesRequest();
        var response = await SendRequestAsync(request);
        ThrowIfError(response);

        if (response.Data is JsonElement element)
        {
            var volumes = JsonSerializer.Deserialize<List<VolumeInfoDto>>(element.GetRawText());
            return volumes ?? new();
        }

        return new();
    }

    // ==================== File Operations ====================

    public async Task WriteFileAsync(string volumeName, string filePath, byte[] data)
    {
        var request = new WriteFileRequest { VolumeName = volumeName, FilePath = filePath, Data = data };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task<byte[]> ReadFileAsync(string volumeName, string filePath)
    {
        var request = new ReadFileRequest { VolumeName = volumeName, FilePath = filePath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);

        if (response.Data is JsonElement element && element.TryGetProperty("data", out var dataElement))
        {
            var bytesStr = dataElement.GetRawText();
            return JsonSerializer.Deserialize<byte[]>(bytesStr) ?? Array.Empty<byte>();
        }

        return Array.Empty<byte>();
    }

    public async Task<byte[]> ReadFileAtAsync(string volumeName, string filePath, long offset, int length)
    {
        var request = new ReadFileAtRequest { VolumeName = volumeName, FilePath = filePath, Offset = offset, Length = length };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);

        if (response.Data is JsonElement element && element.TryGetProperty("data", out var dataElement))
        {
            var bytesStr = dataElement.GetRawText();
            return JsonSerializer.Deserialize<byte[]>(bytesStr) ?? Array.Empty<byte>();
        }

        return Array.Empty<byte>();
    }

    public async Task WriteFileAtAsync(string volumeName, string filePath, byte[] data, long offset)
    {
        var request = new WriteFileAtRequest { VolumeName = volumeName, FilePath = filePath, Data = data, Offset = offset };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task DeleteFileAsync(string volumeName, string filePath)
    {
        var request = new DeleteFileRequest { VolumeName = volumeName, FilePath = filePath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task<bool> FileExistsAsync(string volumeName, string filePath)
    {
        var response = new RpcResponse(); // Placeholder
        ThrowIfError(response);
        return false; // TODO: Implement
    }

    public async Task<FileInfoDto?> GetFileInfoAsync(string volumeName, string filePath)
    {
        return null; // TODO: Implement
    }

    // ==================== Directory Operations ====================

    public async Task CreateDirectoryAsync(string volumeName, string dirPath)
    {
        var request = new CreateDirectoryRequest { VolumeName = volumeName, DirectoryPath = dirPath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    public async Task DeleteDirectoryAsync(string volumeName, string dirPath)
    {
        var response = new RpcResponse(); // Placeholder
        ThrowIfError(response);
    }

    public async Task<List<FileInfoDto>> ListFilesAsync(string volumeName, string dirPath)
    {
        var request = new ListFilesRequest { VolumeName = volumeName, DirectoryPath = dirPath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);

        if (response.Data is JsonElement element)
        {
            var files = JsonSerializer.Deserialize<List<FileInfoDto>>(element.GetRawText());
            return files ?? new();
        }

        return new();
    }

    public async Task<CapacityInfoDto?> GetCapacityAsync(string volumeName)
    {
        var request = new GetCapacityRequest { VolumeName = volumeName };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);

        if (response.Data is JsonElement element)
        {
            var capacityInfo = JsonSerializer.Deserialize<CapacityInfoDto>(element.GetRawText());
            return capacityInfo;
        }

        return null;
    }

    // ==================== Helper Methods ====================

    private void ThrowIfError(RpcResponse response)
    {
        if (!response.Success && response.Error != null)
        {
            throw new InvalidOperationException(
                $"[{response.Error.Code}] {response.Error.Message}\n{response.Error.Details}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipeStream?.Dispose();
    }
}
