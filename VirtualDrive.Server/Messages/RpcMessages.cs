using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VirtualDrive.Server.Messages;

/// <summary>
/// Base class for all RPC messages with request tracking
/// </summary>
public abstract class RpcMessage
{
    /// <summary>
    /// Unique request correlation ID for tracking and response matching
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Operation type identifier
    /// </summary>
    [JsonPropertyName("operation")]
    public abstract string Operation { get; }
}

/// <summary>
/// Response envelope for all RPC operations
/// </summary>
public class RpcResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public RpcError? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error details in RPC response
/// </summary>
public class RpcError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

// ==================== Volume Operations ====================

/// <summary>
/// Create volume request
/// </summary>
public class CreateVolumeRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("capacityMB")]
    public long CapacityMB { get; set; }

    public override string Operation => "CreateVolume";
}

/// <summary>
/// List volumes response
/// </summary>
public class VolumeInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("capacityBytes")]
    public long CapacityBytes { get; set; }

    [JsonPropertyName("usedBytes")]
    public long UsedBytes { get; set; }

    [JsonPropertyName("availableBytes")]
    public long AvailableBytes => CapacityBytes - UsedBytes;
}

public class ListVolumesRequest : RpcMessage
{
    public override string Operation => "ListVolumes";
}

/// <summary>
/// Delete volume request
/// </summary>
public class DeleteVolumeRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    public override string Operation => "DeleteVolume";
}

// ==================== File Operations ====================

/// <summary>
/// Write file request
/// </summary>
public class WriteFileRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public override string Operation => "WriteFile";
}

/// <summary>
/// Read file request
/// </summary>
public class ReadFileRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    public override string Operation => "ReadFile";
}

/// <summary>
/// Read file at position request (streaming)
/// </summary>
public class ReadFileAtRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    public override string Operation => "ReadFileAt";
}

/// <summary>
/// Write file at position request (streaming)
/// </summary>
public class WriteFileAtRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("data")]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public override string Operation => "WriteFileAt";
}

/// <summary>
/// Delete file request
/// </summary>
public class DeleteFileRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    public override string Operation => "DeleteFile";
}

/// <summary>
/// File exists request
/// </summary>
public class FileExistsRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    public override string Operation => "FileExists";
}

/// <summary>
/// Get file info request
/// </summary>
public class GetFileInfoRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    public override string Operation => "GetFileInfo";
}

/// <summary>
/// File info response
/// </summary>
public class FileInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("createdTime")]
    public DateTime CreatedTime { get; set; }

    [JsonPropertyName("lastModifiedTime")]
    public DateTime LastModifiedTime { get; set; }

    [JsonPropertyName("lastAccessedTime")]
    public DateTime LastAccessedTime { get; set; }
}

// ==================== Directory Operations ====================

/// <summary>
/// Create directory request
/// </summary>
public class CreateDirectoryRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    public override string Operation => "CreateDirectory";
}

/// <summary>
/// List files request
/// </summary>
public class ListFilesRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    public override string Operation => "ListFiles";
}

/// <summary>
/// Delete directory request
/// </summary>
public class DeleteDirectoryRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    public override string Operation => "DeleteDirectory";
}

/// <summary>
/// Directory info response
/// </summary>
public class DirectoryInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = string.Empty;

    [JsonPropertyName("createdTime")]
    public DateTime CreatedTime { get; set; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    [JsonPropertyName("subdirectoryCount")]
    public int SubdirectoryCount { get; set; }
}

/// <summary>
/// Capacity info request
/// </summary>
public class GetCapacityRequest : RpcMessage
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    public override string Operation => "GetCapacity";
}

/// <summary>
/// Capacity info response
/// </summary>
public class CapacityInfoDto
{
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }

    [JsonPropertyName("usedBytes")]
    public long UsedBytes { get; set; }

    [JsonPropertyName("availableBytes")]
    public long AvailableBytes => TotalBytes - UsedBytes;

    [JsonPropertyName("percentUsed")]
    public double PercentUsed => TotalBytes > 0 ? (UsedBytes * 100.0) / TotalBytes : 0;
}
