using System;

namespace VirtualDrive.Core;

/// <summary>
/// Represents a file or directory entry in the virtual filesystem.
/// Includes metadata and optional data payload.
/// </summary>
public class VirtualFileSystemEntry
{
    /// <summary>Entry name (file or directory)</summary>
    public string Name { get; set; } = "";
    
    /// <summary>Full path from root (e.g., "\\folder\\file.txt")</summary>
    public string FullPath { get; set; } = "";
    
    /// <summary>True if this entry is a directory, false if file</summary>
    public bool IsDirectory => FullPath.EndsWith("\\");
    
    /// <summary>File size in bytes; 0 for directories</summary>
    public long Size { get; set; }
    
    /// <summary>Timestamp when the entry was created</summary>
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>Timestamp when the entry was last written</summary>
    public DateTime LastWriteTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>Optional file contents (used rarely, typically null)</summary>
    public byte[]? Data { get; set; }
}
