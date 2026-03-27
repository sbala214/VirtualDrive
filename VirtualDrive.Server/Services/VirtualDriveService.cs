using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualDrive.Core;
using VirtualDrive.Server.Messages;

namespace VirtualDrive.Server.Services;

/// <summary>
/// Manages VirtualDrive API operations for remote clients
/// Handles volume and file operations, translates core exceptions to RPC responses
/// </summary>
public class VirtualDriveService
{
    private readonly IVirtualDriveApi _api;
    private readonly Dictionary<string, IVirtualDriveApi> _volumeApis;
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Initializes a new instance of the VirtualDriveService class.
    /// </summary>
    /// <param name="config">Memory buffer configuration. Uses default if null.</param>
    public VirtualDriveService(MemoryBufferConfiguration? config = null)
    {
        _api = config != null ? new VirtualDriveApi(config) : new VirtualDriveApi();
        _volumeApis = new Dictionary<string, IVirtualDriveApi>();
    }

    /// <summary>
    /// Create a new virtual volume
    /// </summary>
    public async Task<RpcResponse> CreateVolume(CreateVolumeRequest request)
    {
        try
        {
            _api.CreateVolume(request.VolumeName, request.CapacityMB);
            return Success(request.RequestId, "CreateVolume", new { message = "Volume created successfully" });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "CreateVolume", ex);
        }
    }

    /// <summary>
    /// Delete a virtual volume
    /// </summary>
    public async Task<RpcResponse> DeleteVolume(string requestId, string volumeName)
    {
        try
        {
            _api.DeleteVolume(volumeName);
            return Success(requestId, "DeleteVolume", new { message = "Volume deleted successfully" });
        }
        catch (Exception ex)
        {
            return Error(requestId, "DeleteVolume", ex);
        }
    }

    /// <summary>
    /// List all volumes
    /// </summary>
    public async Task<RpcResponse> ListVolumes(ListVolumesRequest request)
    {
        try
        {
            var volumes = _api.ListVolumes()
                .Select(v => new VolumeInfoDto
                {
                    Name = v.Name,
                    CapacityBytes = v.CapacityBytes,
                    UsedBytes = v.UsedBytes
                })
                .ToList();

            return Success(request.RequestId, "ListVolumes", volumes);
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "ListVolumes", ex);
        }
    }

    /// <summary>
    /// Write file to volume
    /// </summary>
    public async Task<RpcResponse> WriteFile(WriteFileRequest request)
    {
        try
        {
            _api.WriteFile(request.VolumeName, request.FilePath, request.Data);
            return Success(request.RequestId, "WriteFile", new { bytesWritten = request.Data.Length });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "WriteFile", ex);
        }
    }

    /// <summary>
    /// Read file from volume
    /// </summary>
    public async Task<RpcResponse> ReadFile(ReadFileRequest request)
    {
        try
        {
            var data = _api.ReadFile(request.VolumeName, request.FilePath);
            return Success(request.RequestId, "ReadFile", new { data, bytesRead = data.Length });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "ReadFile", ex);
        }
    }

    /// <summary>
    /// Read file at specific offset (streaming)
    /// </summary>
    public async Task<RpcResponse> ReadFileAt(ReadFileAtRequest request)
    {
        try
        {
            var buffer = new byte[request.Length];
            int bytesRead = _api.ReadFileAt(request.VolumeName, request.FilePath, buffer, request.Offset);
            
            // Trim buffer to actual bytes read
            if (bytesRead < buffer.Length)
            {
                Array.Resize(ref buffer, bytesRead);
            }

            return Success(request.RequestId, "ReadFileAt", new { data = buffer, bytesRead });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "ReadFileAt", ex);
        }
    }

    /// <summary>
    /// Write file at specific offset (streaming)
    /// </summary>
    public async Task<RpcResponse> WriteFileAt(WriteFileAtRequest request)
    {
        try
        {
            _api.WriteFileAt(request.VolumeName, request.FilePath, request.Data, request.Offset);
            return Success(request.RequestId, "WriteFileAt", new { bytesWritten = request.Data.Length });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "WriteFileAt", ex);
        }
    }

    /// <summary>
    /// Delete file from volume
    /// </summary>
    public async Task<RpcResponse> DeleteFile(DeleteFileRequest request)
    {
        try
        {
            _api.DeleteFile(request.VolumeName, request.FilePath);
            return Success(request.RequestId, "DeleteFile", new { message = "File deleted successfully" });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "DeleteFile", ex);
        }
    }

    /// <summary>
    /// Check if file exists
    /// </summary>
    public async Task<RpcResponse> FileExists(string requestId, string volumeName, string filePath)
    {
        try
        {
            bool exists = _api.FileExists(volumeName, filePath);
            return Success(requestId, "FileExists", new { exists });
        }
        catch (Exception ex)
        {
            return Error(requestId, "FileExists", ex);
        }
    }

    /// <summary>
    /// Get file information
    /// </summary>
    public async Task<RpcResponse> GetFileInfo(string requestId, string volumeName, string filePath)
    {
        try
        {
            var info = _api.GetFileInfo(volumeName, filePath);
            var dto = new FileInfoDto
            {
                Name = info.Name,
                FullPath = info.FullPath,
                Size = info.Size,
                CreatedTime = info.CreatedTime,
                LastModifiedTime = info.LastModifiedTime,
                LastAccessedTime = info.LastAccessedTime
            };
            return Success(requestId, "GetFileInfo", dto);
        }
        catch (Exception ex)
        {
            return Error(requestId, "GetFileInfo", ex);
        }
    }

    /// <summary>
    /// Create directory
    /// </summary>
    public async Task<RpcResponse> CreateDirectory(CreateDirectoryRequest request)
    {
        try
        {
            _api.CreateDirectory(request.VolumeName, request.DirectoryPath);
            return Success(request.RequestId, "CreateDirectory", new { message = "Directory created successfully" });
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "CreateDirectory", ex);
        }
    }

    /// <summary>
    /// Delete directory
    /// </summary>
    public async Task<RpcResponse> DeleteDirectory(string requestId, string volumeName, string dirPath)
    {
        try
        {
            _api.DeleteDirectory(volumeName, dirPath);
            return Success(requestId, "DeleteDirectory", new { message = "Directory deleted successfully" });
        }
        catch (Exception ex)
        {
            return Error(requestId, "DeleteDirectory", ex);
        }
    }

    /// <summary>
    /// List files in directory
    /// </summary>
    public async Task<RpcResponse> ListFiles(ListFilesRequest request)
    {
        try
        {
            var files = _api.ListFiles(request.VolumeName, request.DirectoryPath)
                .Select(f => new FileInfoDto
                {
                    Name = f.Name,
                    FullPath = f.FullPath,
                    Size = f.Size,
                    CreatedTime = f.CreatedTime,
                    LastModifiedTime = f.LastModifiedTime,
                    LastAccessedTime = f.LastAccessedTime
                })
                .ToList();

            return Success(request.RequestId, "ListFiles", files);
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "ListFiles", ex);
        }
    }

    /// <summary>
    /// Get capacity information
    /// </summary>
    public async Task<RpcResponse> GetCapacity(GetCapacityRequest request)
    {
        try
        {
            long totalBytes = _api.GetCapacityBytes(request.VolumeName);
            long usedBytes = _api.GetUsedBytes(request.VolumeName);

            var dto = new CapacityInfoDto
            {
                VolumeName = request.VolumeName,
                TotalBytes = totalBytes,
                UsedBytes = usedBytes
            };

            return Success(request.RequestId, "GetCapacity", dto);
        }
        catch (Exception ex)
        {
            return Error(request.RequestId, "GetCapacity", ex);
        }
    }

    // ==================== Helper Methods ====================

    private RpcResponse Success(string requestId, string operation, object? data = null)
    {
        return new RpcResponse
        {
            RequestId = requestId,
            Success = true,
            Operation = operation,
            Data = data,
            Timestamp = DateTime.UtcNow
        };
    }

    private RpcResponse Error(string requestId, string operation, Exception ex)
    {
        return new RpcResponse
        {
            RequestId = requestId,
            Success = false,
            Operation = operation,
            Error = new RpcError
            {
                Code = ex.GetType().Name,
                Message = ex.Message,
                Details = ex.StackTrace
            },
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Releases all resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}
