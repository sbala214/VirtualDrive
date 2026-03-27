namespace VirtualDrive.Core;

/// <summary>
/// Implementation of the virtual drive API. 
/// Combines FileSystem, SectorAllocator, and SegmentedMemoryBuffer into a unified, user-friendly interface.
/// Thread-safe for concurrent read operations.
/// </summary>
public class VirtualDriveApi : IVirtualDriveApi
{
    private readonly Dictionary<string, VirtualVolume> _volumes = new();
    private readonly object _volumeLock = new();
    private readonly MemoryBufferConfiguration _bufferConfig;

    /// <summary>
    /// Internal volume representation combining filesystem and capacity management.
    /// </summary>
    private class VirtualVolume
    {
        public string Name { get; set; }
        public long CapacityBytes { get; set; }
        public FileSystem FileSystem { get; set; }

        public VirtualVolume(string name, long capacityBytes, MemoryBufferConfiguration bufferConfig)
        {
            Name = name;
            CapacityBytes = capacityBytes;
            FileSystem = new FileSystem(bufferConfig);
        }
    }

    /// <summary>
    /// Creates API instance with default memory buffer configuration (50 × 2GB).
    /// </summary>
    public VirtualDriveApi() : this(MemoryBufferConfiguration.CreateDefault())
    {
    }

    /// <summary>
    /// Creates API instance with custom memory buffer configuration.
    /// </summary>
    public VirtualDriveApi(MemoryBufferConfiguration bufferConfig)
    {
        if (bufferConfig == null)
            throw new ArgumentNullException(nameof(bufferConfig));
        
        _bufferConfig = bufferConfig;
    }

    public void CreateVolume(string volumeName, long capacityMB)
    {
        if (string.IsNullOrWhiteSpace(volumeName))
            throw new ArgumentNullException(nameof(volumeName));
        if (capacityMB <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacityMB));

        lock (_volumeLock)
        {
            if (_volumes.ContainsKey(volumeName))
                throw new InvalidOperationException($"Volume '{volumeName}' already exists");

            long capacityBytes = capacityMB * 1024 * 1024;
            var volume = new VirtualVolume(volumeName, capacityBytes, _bufferConfig);
            _volumes[volumeName] = volume;
        }
    }

    public void DeleteVolume(string volumeName)
    {
        if (string.IsNullOrWhiteSpace(volumeName))
            throw new ArgumentNullException(nameof(volumeName));

        lock (_volumeLock)
        {
            if (!_volumes.Remove(volumeName))
                throw new InvalidOperationException($"Volume '{volumeName}' not found");
        }
    }

    public IEnumerable<VolumeInfo> ListVolumes()
    {
        lock (_volumeLock)
        {
            return _volumes.Values.Select(v => new VolumeInfo
            {
                Name = v.Name,
                CapacityBytes = v.CapacityBytes,
                UsedBytes = v.FileSystem.GetTotalUsedBytes()
            }).ToList();
        }
    }

    /// <summary>
    /// Retrieves information about a specific volume.
    /// Returns capacity, used bytes, and available free space.
    /// </summary>
    /// <param name="volumeName">Name of the volume to query</param>
    /// <returns>Volume information including capacity and usage</returns>
    public VolumeInfo GetVolumeInfo(string volumeName)
    {
        var volume = GetVolume(volumeName);
        return new VolumeInfo
        {
            Name = volume.Name,
            CapacityBytes = volume.CapacityBytes,
            UsedBytes = volume.FileSystem.GetTotalUsedBytes()
        };
    }

    public void WriteFile(string volumeName, string filePath, byte[] data)
    {
        var volume = GetVolume(volumeName);
        if (data == null)
            data = Array.Empty<byte>();

        // Create parent directories if needed
        EnsureDirectoriesExist(volume.FileSystem, filePath);

        // Create or overwrite file
        var (_, fileName) = ParsePath(filePath);
        volume.FileSystem.CreateFile(filePath, fileName);
        
        // Write data
        volume.FileSystem.WriteFileAt(filePath, data, 0, volume.CapacityBytes);
    }

    public byte[] ReadFile(string volumeName, string filePath)
    {
        var volume = GetVolume(volumeName);
        return volume.FileSystem.ReadFile(filePath);
    }

    public void WriteFileAt(string volumeName, string filePath, byte[] data, long offset)
    {
        var volume = GetVolume(volumeName);
        if (data == null || data.Length == 0)
            return;

        // Ensure file exists (create with empty content if needed)
        if (!FileExists(volumeName, filePath))
        {
            var (_, fileName) = ParsePath(filePath);
            EnsureDirectoriesExist(volume.FileSystem, filePath);
            volume.FileSystem.CreateFile(filePath, fileName);
        }

        volume.FileSystem.WriteFileAt(filePath, data, offset, volume.CapacityBytes);
    }

    public int ReadFileAt(string volumeName, string filePath, byte[] buffer, long offset)
    {
        var volume = GetVolume(volumeName);
        if (buffer == null || buffer.Length == 0)
            return 0;

        return volume.FileSystem.ReadFileAt(filePath, buffer, offset, buffer.Length);
    }

    public void DeleteFile(string volumeName, string filePath)
    {
        var volume = GetVolume(volumeName);
        bool deleted = volume.FileSystem.DeleteFile(filePath);
        if (!deleted)
            throw new InvalidOperationException($"File not found or could not be deleted: {filePath}");
    }

    public bool FileExists(string volumeName, string filePath)
    {
        var volume = GetVolume(volumeName);
        var fileInfo = volume.FileSystem.GetFileInfo(filePath);
        return fileInfo != null;
    }

    public void CreateDirectory(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        EnsureDirectoryPathExists(volume.FileSystem, dirPath, createTarget: true);
    }

    public void DeleteDirectory(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        volume.FileSystem.DeleteDirectory(dirPath);
    }

    public IEnumerable<FileInfo> ListFiles(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        var dirInfo = volume.FileSystem.GetDirectoryInfo(dirPath);
        
        if (dirInfo == null)
            throw new InvalidOperationException($"Directory not found: {dirPath}");
        
        return dirInfo.Files.Values
            .OfType<FileMetadata>()
            .Select(f => new FileInfo
            {
                Name = f.Name ?? string.Empty,
                FullPath = f.FullPath ?? string.Empty,
                Size = f.Size,
                CreatedTime = f.CreatedTime,
                LastModifiedTime = f.LastModifiedTime,
                LastAccessedTime = f.LastAccessedTime
            }).ToList();
    }

    public IEnumerable<DirectoryInfo> ListDirectories(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        var dirInfo = volume.FileSystem.GetDirectoryInfo(dirPath);
        
        if (dirInfo == null)
            throw new InvalidOperationException($"Directory not found: {dirPath}");
        
        return dirInfo.Subdirectories.Values
            .OfType<DirectoryNode>()
            .Select(d => new DirectoryInfo
            {
                Name = d.OriginalName ?? string.Empty,
                FullPath = d.GetFullPath() ?? string.Empty,
                CreatedTime = DateTime.UtcNow, // DirectoryNode doesn't track creation time
                FileCount = d.Files.Count,
                SubdirectoryCount = d.Subdirectories.Count
            }).ToList();
    }

    public bool DirectoryExists(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        var dirInfo = volume.FileSystem.GetDirectoryInfo(dirPath);
        return dirInfo != null;
    }

    public FileInfo GetFileInfo(string volumeName, string filePath)
    {
        var volume = GetVolume(volumeName);
        var meta = volume.FileSystem.GetFileInfo(filePath);
        
        if (meta == null)
            throw new InvalidOperationException($"File not found: {filePath}");
        
        return new FileInfo
        {
            Name = meta.Name ?? string.Empty,
            FullPath = meta.FullPath ?? string.Empty,
            Size = meta.Size,
            CreatedTime = meta.CreatedTime,
            LastModifiedTime = meta.LastModifiedTime,
            LastAccessedTime = meta.LastAccessedTime
        };
    }

    public DirectoryInfo GetDirectoryInfo(string volumeName, string dirPath)
    {
        var volume = GetVolume(volumeName);
        var dirNode = volume.FileSystem.GetDirectoryInfo(dirPath);
        
        if (dirNode == null)
            throw new InvalidOperationException($"Directory not found: {dirPath}");
        
        return new DirectoryInfo
        {
            Name = dirNode.OriginalName ?? string.Empty,
            FullPath = dirNode.GetFullPath() ?? string.Empty,
            CreatedTime = DateTime.UtcNow,
            FileCount = dirNode.Files.Count,
            SubdirectoryCount = dirNode.Subdirectories.Count
        };
    }

    public long GetUsedBytes(string volumeName)
    {
        var volume = GetVolume(volumeName);
        return volume.FileSystem.GetTotalUsedBytes();
    }

    public long GetAvailableBytes(string volumeName)
    {
        var volume = GetVolume(volumeName);
        return volume.CapacityBytes - volume.FileSystem.GetTotalUsedBytes();
    }

    public long GetCapacityBytes(string volumeName)
    {
        var volume = GetVolume(volumeName);
        return volume.CapacityBytes;
    }

    // ============================================================================
    // PRIVATE HELPERS
    // ============================================================================

    /// <summary>
    /// Retrieves a volume by name. Throws if not found.
    /// </summary>
    /// <param name="volumeName">Name of the volume to retrieve</param>
    /// <returns>Virtual volume object</returns>
    /// <exception cref="InvalidOperationException">If volume does not exist</exception>
    private VirtualVolume GetVolume(string volumeName)
    {
        lock (_volumeLock)
        {
            if (!_volumes.TryGetValue(volumeName, out var volume))
                throw new InvalidOperationException($"Volume '{volumeName}' not found");
            return volume;
        }
    }

    /// <summary>
    /// Ensures all directories in a path exist. If createTarget is true, creates the target directory too.
    /// </summary>
    private void EnsureDirectoryPathExists(FileSystem fs, string dirPath, bool createTarget)
    {
        var parts = dirPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var currentPath = "";
        int loopEnd = createTarget ? parts.Length : parts.Length - 1;

        for (int i = 0; i < loopEnd; i++)
        {
            currentPath += "\\" + parts[i];
            var dirInfo = fs.GetDirectoryInfo(currentPath);
            if (dirInfo == null)
            {
                // Directory doesn't exist, create it
                fs.CreateDirectory(currentPath, parts[i]);
            }
        }
    }

    /// <summary>
    /// Ensures parent directories exist for a file path.
    /// </summary>
    private void EnsureDirectoriesExist(FileSystem fs, string filePath)
    {
        var parts = filePath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return; // Just a file in root

        var currentPath = "";
        for (int i = 0; i < parts.Length - 1; i++) // Skip last part (filename)
        {
            currentPath += "\\" + parts[i];
            var dirInfo = fs.GetDirectoryInfo(currentPath);
            if (dirInfo == null)
            {
                // Directory doesn't exist, create it
                fs.CreateDirectory(currentPath, parts[i]);
            }
        }
    }

    /// <summary>
    /// Parses a file path into parent directory path and file name components.
    /// Example: "\\folder\\subfolder\\file.txt" → ("\\folder\\subfolder", "file.txt")
    /// </summary>
    /// <param name="fullPath">Full file path to parse</param>
    /// <returns>Tuple of (parent directory path, file name)</returns>
    private static (string parentPath, string name) ParsePath(string fullPath)
    {
        if (fullPath == "\\")
            return ("\\", "");

        int lastSlash = fullPath.LastIndexOf('\\');
        if (lastSlash < 0)
            return ("\\", fullPath);

        string parentPath = lastSlash == 0 ? "\\" : fullPath.Substring(0, lastSlash);
        string name = fullPath.Substring(lastSlash + 1);

        return (parentPath, name);
    }
}
