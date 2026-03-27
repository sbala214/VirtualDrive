using System.Collections.Generic;

namespace VirtualDrive.Core;

/// <summary>
/// Interface for virtual file system operations
/// </summary>
public interface IVirtualFileSystem
{
    /// <summary>
    /// Gets the total bytes used by all files in the filesystem.
    /// </summary>
    /// <returns>Total bytes used by all files</returns>
    long GetTotalUsedBytes();
    
    /// <summary>
    /// Enumerates all file and directory entries in the filesystem.
    /// </summary>
    /// <returns>All file and directory entries</returns>
    IEnumerable<VirtualFileSystemEntry> GetAllEntries();
    
    /// <summary>
    /// Creates a new file at the specified path.
    /// </summary>
    /// <param name="path">Full file path (e.g., "\\folder\\file.txt")</param>
    /// <param name="originalName">Display name preserving original case</param>
    void CreateFile(string path, string originalName);
    
    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <param name="path">Full directory path (e.g., "\\folder\\subfolder")</param>
    /// <param name="name">Directory name preserving original case</param>
    void CreateDirectory(string path, string name);
    
    /// <summary>
    /// Writes data to a file, replacing its contents entirely.
    /// Creates the file if it doesn't exist, otherwise overwrites it.
    /// </summary>
    /// <param name="path">Full file path</param>
    /// <param name="data">Data bytes to write</param>
    void WriteFile(string path, byte[] data);

    /// <summary>
    /// Sets the file size without loading the entire file into memory.
    /// Allocates new sectors when expanding, deallocates when shrinking.
    /// Useful for streaming operations and file resizing.
    /// </summary>
    /// <param name="path">Full file path</param>
    /// <param name="newSize">New file size in bytes</param>
    /// <param name="volumeCapacityBytes">Maximum volume capacity for validation</param>
    void SetFileSize(string path, long newSize, long volumeCapacityBytes = long.MaxValue);
}
