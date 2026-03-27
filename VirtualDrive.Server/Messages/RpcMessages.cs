using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProtoBuf;

namespace VirtualDrive.Server.Messages;

/// <summary>
/// Base class for all RPC messages with request tracking
/// </summary>
[ProtoContract]
public abstract class RpcMessage
{
    /// <summary>
    /// Unique request correlation ID for tracking and response matching
    /// </summary>
    [ProtoMember(1)]
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
[ProtoContract]
public class RpcResponse
{
    /// <summary>Gets or sets the correlation request ID.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Gets or sets the operation type identifier.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation response data payload.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>Gets or sets error details if the operation failed.</summary>
    [ProtoMember(5)]
    [JsonPropertyName("error")]
    public RpcError? Error { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the response was created.</summary>
    [ProtoMember(6)]
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error details in RPC response
/// </summary>
[ProtoContract]
public class RpcError
{
    /// <summary>Gets or sets the error code.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the error message.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets optional detailed error information.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

// ==================== Volume Operations ====================

/// <summary>
/// Create volume request
/// </summary>
[ProtoContract]
public class CreateVolumeRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the volume capacity in megabytes.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("capacityMB")]
    public long CapacityMB { get; set; }

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "CreateVolume";
}

/// <summary>
/// List volumes response
/// </summary>
[ProtoContract]
public class VolumeInfoDto
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the total capacity in bytes.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("capacityBytes")]
    public long CapacityBytes { get; set; }

    /// <summary>Gets or sets the used space in bytes.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("usedBytes")]
    public long UsedBytes { get; set; }

    /// <summary>Gets the available space in bytes (calculated).</summary>
    [JsonPropertyName("availableBytes")]
    public long AvailableBytes => CapacityBytes - UsedBytes;
}

/// <summary>
/// List volumes request
/// </summary>
[ProtoContract]
public class ListVolumesRequest : RpcMessage
{
    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "ListVolumes";
}

/// <summary>
/// Delete volume request
/// </summary>
[ProtoContract]
public class DeleteVolumeRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "DeleteVolume";
}

// ==================== File Operations ====================

/// <summary>
/// Write file request
/// </summary>
[ProtoContract]
public class WriteFileRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the file data to write.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("data")]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "WriteFile";
}

/// <summary>
/// Read file request
/// </summary>
[ProtoContract]
public class ReadFileRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "ReadFile";
}

/// <summary>
/// Read file at position request (streaming)
/// </summary>
[ProtoContract]
public class ReadFileAtRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the file offset to start reading from.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    /// <summary>Gets or sets the number of bytes to read.</summary>
    [ProtoMember(5)]
    [JsonPropertyName("length")]
    public int Length { get; set; }

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "ReadFileAt";
}

/// <summary>
/// Write file at position request (streaming)
/// </summary>
[ProtoContract]
public class WriteFileAtRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the file offset to start writing at.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    /// <summary>Gets or sets the file data to write.</summary>
    [ProtoMember(5)]
    [JsonPropertyName("data")]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "WriteFileAt";
}

/// <summary>
/// Delete file request
/// </summary>
[ProtoContract]
public class DeleteFileRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "DeleteFile";
}

/// <summary>
/// File exists request
/// </summary>
[ProtoContract]
public class FileExistsRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "FileExists";
}

/// <summary>
/// Get file info request
/// </summary>
[ProtoContract]
public class GetFileInfoRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "GetFileInfo";
}

/// <summary>
/// File info response
/// </summary>
[ProtoContract]
public class FileInfoDto
{
    /// <summary>Gets or sets the file name.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the full file path.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the file size in bytes.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Gets or sets the file creation time in UTC.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("createdTime")]
    public DateTime CreatedTime { get; set; }

    /// <summary>Gets or sets the file last modified time in UTC.</summary>
    [ProtoMember(5)]
    [JsonPropertyName("lastModifiedTime")]
    public DateTime LastModifiedTime { get; set; }

    /// <summary>Gets or sets the file last accessed time in UTC.</summary>
    [ProtoMember(6)]
    [JsonPropertyName("lastAccessedTime")]
    public DateTime LastAccessedTime { get; set; }
}

// ==================== Directory Operations ====================

/// <summary>
/// Create directory request
/// </summary>
[ProtoContract]
public class CreateDirectoryRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "CreateDirectory";
}

/// <summary>
/// List files request
/// </summary>
[ProtoContract]
public class ListFilesRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "ListFiles";
}

/// <summary>
/// Delete directory request
/// </summary>
[ProtoContract]
public class DeleteDirectoryRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory path.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("dirPath")]
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "DeleteDirectory";
}

/// <summary>
/// Directory info response
/// </summary>
[ProtoContract]
public class DirectoryInfoDto
{
    /// <summary>Gets or sets the directory name.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the full directory path.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory creation time in UTC.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("createdTime")]
    public DateTime CreatedTime { get; set; }

    /// <summary>Gets or sets the number of files in the directory.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    /// <summary>Gets or sets the number of subdirectories in the directory.</summary>
    [ProtoMember(5)]
    [JsonPropertyName("subdirectoryCount")]
    public int SubdirectoryCount { get; set; }
}

/// <summary>
/// Capacity info request
/// </summary>
[ProtoContract]
public class GetCapacityRequest : RpcMessage
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets the operation type identifier.</summary>
    public override string Operation => "GetCapacity";
}

/// <summary>
/// Capacity info response
/// </summary>
[ProtoContract]
public class CapacityInfoDto
{
    /// <summary>Gets or sets the volume name.</summary>
    [ProtoMember(1)]
    [JsonPropertyName("volumeName")]
    public string VolumeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the total capacity in bytes.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; set; }

    /// <summary>Gets or sets the used space in bytes.</summary>
    [ProtoMember(3)]
    [JsonPropertyName("usedBytes")]
    public long UsedBytes { get; set; }

    /// <summary>Gets the available space in bytes (calculated).</summary>
    [JsonPropertyName("availableBytes")]
    public long AvailableBytes => TotalBytes - UsedBytes;

    /// <summary>Gets the percentage of used space (calculated).</summary>
    [JsonPropertyName("percentUsed")]
    public double PercentUsed => TotalBytes > 0 ? (UsedBytes * 100.0) / TotalBytes : 0;
}
