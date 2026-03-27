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

    /// <summary>
    /// Initializes a new instance of the NamedPipesClient class.
    /// </summary>
    /// <param name="pipeName">The name of the named pipe to connect to (default: VirtualDrive.Server).</param>
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

    /// <summary>
    /// Creates a new virtual volume.
    /// </summary>
    /// <param name="volumeName">The name of the volume to create.</param>
    /// <param name="capacityMB">The capacity of the volume in megabytes.</param>
    public async Task CreateVolumeAsync(string volumeName, long capacityMB)
    {
        var request = new CreateVolumeRequest { VolumeName = volumeName, CapacityMB = capacityMB };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Deletes an existing virtual volume.
    /// </summary>
    /// <param name="volumeName">The name of the volume to delete.</param>
    public async Task DeleteVolumeAsync(string volumeName)
    {
        var request = new DeleteVolumeRequest { VolumeName = volumeName };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Lists all existing virtual volumes.
    /// </summary>
    /// <returns>A list of volume information objects.</returns>
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

    /// <summary>
    /// Writes data to a file.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <param name="data">The data to write to the file.</param>
    public async Task WriteFileAsync(string volumeName, string filePath, byte[] data)
    {
        var request = new WriteFileRequest { VolumeName = volumeName, FilePath = filePath, Data = data };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Reads the entire contents of a file.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <returns>The file contents as a byte array.</returns>
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

    /// <summary>
    /// Reads a portion of a file starting at a specified offset.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <param name="offset">The byte offset to start reading from.</param>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>The requested portion of the file as a byte array.</returns>
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

    /// <summary>
    /// Writes data to a file starting at a specified offset.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="offset">The byte offset to start writing at.</param>
    public async Task WriteFileAtAsync(string volumeName, string filePath, byte[] data, long offset)
    {
        var request = new WriteFileAtRequest { VolumeName = volumeName, FilePath = filePath, Data = data, Offset = offset };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    public async Task DeleteFileAsync(string volumeName, string filePath)
    {
        var request = new DeleteFileRequest { VolumeName = volumeName, FilePath = filePath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Checks whether a file exists.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <returns>True if the file exists; otherwise false.</returns>
    public async Task<bool> FileExistsAsync(string volumeName, string filePath)
    {
        var response = new RpcResponse(); // Placeholder
        ThrowIfError(response);
        return false; // TODO: Implement
    }

    /// <summary>
    /// Retrieves metadata about a file.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the file.</param>
    /// <param name="filePath">The path to the file within the volume.</param>
    /// <returns>File information if found; otherwise null.</returns>
    public async Task<FileInfoDto?> GetFileInfoAsync(string volumeName, string filePath)
    {
        return null; // TODO: Implement
    }

    // ==================== Directory Operations ====================

    /// <summary>
    /// Creates a new directory.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the directory.</param>
    /// <param name="dirPath">The path to the directory to create.</param>
    public async Task CreateDirectoryAsync(string volumeName, string dirPath)
    {
        var request = new CreateDirectoryRequest { VolumeName = volumeName, DirectoryPath = dirPath };
        var response = await SendRequestAsync(request);
        ThrowIfError(response);
    }

    /// <summary>
    /// Deletes a directory.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the directory.</param>
    /// <param name="dirPath">The path to the directory to delete.</param>
    public async Task DeleteDirectoryAsync(string volumeName, string dirPath)
    {
        var response = new RpcResponse(); // Placeholder
        ThrowIfError(response);
    }

    /// <summary>
    /// Lists the contents of a directory.
    /// </summary>
    /// <param name="volumeName">The name of the volume containing the directory.</param>
    /// <param name="dirPath">The path to the directory to list.</param>
    /// <returns>A list of files in the directory.</returns>
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

    /// <summary>
    /// Retrieves capacity and usage information for a volume.
    /// </summary>
    /// <param name="volumeName">The name of the volume.</param>
    /// <returns>Capacity information if found; otherwise null.</returns>
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

    /// <summary>
    /// Releases all resources used by the client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipeStream?.Dispose();
    }
}
