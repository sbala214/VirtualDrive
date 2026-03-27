using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualDrive.Server.Messages;

namespace VirtualDrive.Client.Abstractions;

/// <summary>
/// Abstract interface for VirtualDrive transport clients
/// Defines the contract for Named Pipes, HTTP, and other client implementations
/// </summary>
public interface ITransportClient : IAsyncDisposable
{
    // ==================== Connection ====================

    /// <summary>
    /// Connect to the VirtualDrive server
    /// </summary>
    Task ConnectAsync(int timeoutMs = 5000);

    // ==================== Volume Operations ====================

    /// <summary>
    /// Create a new virtual volume
    /// </summary>
    Task CreateVolumeAsync(string volumeName, long capacityMB);

    /// <summary>
    /// Delete a virtual volume
    /// </summary>
    Task DeleteVolumeAsync(string volumeName);

    /// <summary>
    /// List all virtual volumes
    /// </summary>
    Task<List<VolumeInfoDto>> ListVolumesAsync();

    // ==================== File Operations ====================

    /// <summary>
    /// Write entire file content
    /// </summary>
    Task WriteFileAsync(string volumeName, string filePath, byte[] data);

    /// <summary>
    /// Read entire file content
    /// </summary>
    Task<byte[]> ReadFileAsync(string volumeName, string filePath);

    /// <summary>
    /// Write file content at specific offset
    /// </summary>
    Task WriteFileAtAsync(string volumeName, string filePath, byte[] data, long offset);

    /// <summary>
    /// Read file content from specific offset
    /// </summary>
    Task<byte[]> ReadFileAtAsync(string volumeName, string filePath, long offset, int length);

    /// <summary>
    /// Delete a file
    /// </summary>
    Task DeleteFileAsync(string volumeName, string filePath);

    /// <summary>
    /// Check if file exists
    /// </summary>
    Task<bool> FileExistsAsync(string volumeName, string filePath);

    /// <summary>
    /// Get file information
    /// </summary>
    Task<FileInfoDto?> GetFileInfoAsync(string volumeName, string filePath);

    // ==================== Directory Operations ====================

    /// <summary>
    /// Create a directory
    /// </summary>
    Task CreateDirectoryAsync(string volumeName, string dirPath);

    /// <summary>
    /// Delete a directory
    /// </summary>
    Task DeleteDirectoryAsync(string volumeName, string dirPath);

    /// <summary>
    /// List files in directory
    /// </summary>
    Task<List<FileInfoDto>> ListFilesAsync(string volumeName, string dirPath);

    /// <summary>
    /// Get volume capacity information
    /// </summary>
    Task<CapacityInfoDto?> GetCapacityAsync(string volumeName);
}
