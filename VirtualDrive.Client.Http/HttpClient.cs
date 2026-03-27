using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualDrive.Client.Abstractions;
using VirtualDrive.Server.Messages;

namespace VirtualDrive.Client.Http;

/// <summary>
/// Client for HTTP/REST transport
/// Network-accessible client for local, intranet, and internet access
/// </summary>
public class HttpClient : ITransportClient
{
    private readonly HttpClientHandler _handler;
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly string _baseUrl;

    public HttpClient(string serverUrl = "http://localhost:5051")
    {
        _baseUrl = serverUrl.TrimEnd('/');
        _handler = new HttpClientHandler();
        _httpClient = new System.Net.Http.HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        Console.WriteLine($"[HTTP Client] Initialized for {_baseUrl}");
    }

    /// <summary>
    /// Connect to the HTTP server (validates connectivity)
    /// </summary>
    public async Task ConnectAsync(int timeoutMs = 5000)
    {
        _httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
        
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Server returned {response.StatusCode}");
            
            Console.WriteLine($"[HTTP Client] Connected to {_baseUrl}");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to connect to HTTP server at {_baseUrl}: {ex.Message}", ex);
        }
        finally
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }
    }

    /// <summary>
    /// Check server health
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ==================== Volume Operations ====================

    public async Task CreateVolumeAsync(string volumeName, long capacityMB)
    {
        var request = new CreateVolumeRequest { VolumeName = volumeName, CapacityMB = capacityMB };
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/volumes/create", request);
        ThrowIfError(response);
    }

    public async Task DeleteVolumeAsync(string volumeName)
    {
        var response = await DeleteAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/volumes/{volumeName}");
        ThrowIfError(response);
    }

    public async Task<List<VolumeInfoDto>> ListVolumesAsync()
    {
        var response = await GetAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/volumes");
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
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/write", request);
        ThrowIfError(response);
    }

    public async Task<byte[]> ReadFileAsync(string volumeName, string filePath)
    {
        var request = new ReadFileRequest { VolumeName = volumeName, FilePath = filePath };
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/read", request);
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
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/read-at", request);
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
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/write-at", request);
        ThrowIfError(response);
    }

    public async Task DeleteFileAsync(string volumeName, string filePath)
    {
        var request = new DeleteFileRequest { VolumeName = volumeName, FilePath = filePath };
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files", request);
        ThrowIfError(response);
    }

    public async Task<bool> FileExistsAsync(string volumeName, string filePath)
    {
        var query = $"volumeName={volumeName}&filePath={filePath}";
        var response = await GetAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/exists?{query}");
        ThrowIfError(response);

        if (response.Data is JsonElement element && element.TryGetProperty("exists", out var existsElement))
        {
            return existsElement.GetBoolean();
        }

        return false;
    }

    public async Task<FileInfoDto?> GetFileInfoAsync(string volumeName, string filePath)
    {
        var query = $"volumeName={volumeName}&filePath={filePath}";
        var response = await GetAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/files/info?{query}");
        ThrowIfError(response);

        if (response.Data is JsonElement element)
        {
            var fileInfo = JsonSerializer.Deserialize<FileInfoDto>(element.GetRawText());
            return fileInfo;
        }

        return null;
    }

    // ==================== Directory Operations ====================

    public async Task CreateDirectoryAsync(string volumeName, string dirPath)
    {
        var request = new CreateDirectoryRequest { VolumeName = volumeName, DirectoryPath = dirPath };
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/directories/create", request);
        ThrowIfError(response);
    }

    public async Task DeleteDirectoryAsync(string volumeName, string dirPath)
    {
        var query = $"volumeName={volumeName}&dirPath={dirPath}";
        var response = await DeleteAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/directories?{query}");
        ThrowIfError(response);
    }

    public async Task<List<FileInfoDto>> ListFilesAsync(string volumeName, string dirPath)
    {
        var request = new ListFilesRequest { VolumeName = volumeName, DirectoryPath = dirPath };
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/directories/list", request);
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
        var response = await PostAsync<RpcResponse>($"{_baseUrl}/api/virtualdrive/capacity", request);
        ThrowIfError(response);

        if (response.Data is JsonElement element)
        {
            var capacityInfo = JsonSerializer.Deserialize<CapacityInfoDto>(element.GetRawText());
            return capacityInfo;
        }

        return null;
    }

    // ==================== Helper Methods ====================

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(content);
        return result ?? throw new InvalidOperationException($"Failed to deserialize response from {url}");
    }

    private async Task<T> PostAsync<T>(string url, object? data = null)
    {
        var content = data != null ? new StringContent(JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json") : null;
        var response = await _httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(responseContent);
        return result ?? throw new InvalidOperationException($"Failed to deserialize response from {url}");
    }

    private async Task<T> DeleteAsync<T>(string url)
    {
        var response = await _httpClient.DeleteAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(content);
        return result ?? throw new InvalidOperationException($"Failed to deserialize response from {url}");
    }

    private void ThrowIfError(RpcResponse? response)
    {
        if (response?.Success == false && response.Error != null)
        {
            throw new InvalidOperationException(
                $"[{response.Error.Code}] {response.Error.Message}\n{response.Error.Details}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}
