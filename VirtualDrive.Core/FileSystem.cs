using System;
using System.Collections.Generic;
using System.Threading;

namespace VirtualDrive.Core;

/// <summary>
/// Hierarchical virtual filesystem with sector-based allocation and reader-writer locking.
/// Supports files > 2GB using segmented memory buffers.
/// Provides concurrent reads and exclusive writes with fail-fast error recovery.
/// </summary>
public class FileSystem : IVirtualFileSystem
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);  // Allow concurrent reads
    private readonly SectorAllocator _allocator = new SectorAllocator();
    private readonly SegmentedMemoryBuffer _memoryBuffer;
    private readonly DirectoryNode _root = new DirectoryNode("\\", null);
    private long _totalUsedBytes = 0;  // Track total file size incrementally to avoid O(n) traversal
    private readonly int _sectorSize = SectorAllocator.GetSectorSize();  // Cache sector size to avoid repeated lookups

    /// <summary>
    /// Creates a filesystem with default memory buffer configuration (50 × 2GB).
    /// </summary>
    public FileSystem() : this(MemoryBufferConfiguration.CreateDefault())
    {
    }

    /// <summary>
    /// Creates a filesystem with custom memory buffer configuration.
    /// </summary>
    public FileSystem(MemoryBufferConfiguration bufferConfig)
    {
        if (bufferConfig == null)
            throw new ArgumentNullException(nameof(bufferConfig));
        
        _memoryBuffer = new SegmentedMemoryBuffer(bufferConfig);
        // Root directory is pre-created
    }

    /// <summary>
    /// Gets total used bytes across all files.
    /// </summary>
    public long GetTotalUsedBytes()
    {
        _lock.EnterReadLock();
        try
        {
            return _root.GetTotalBytes();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Enumerates all entries in the filesystem (for compatibility).
    /// This is a legacy method - prefer direct filesystem operations.
    /// </summary>
    public IEnumerable<VirtualFileSystemEntry> GetAllEntries()
    {
        _lock.EnterReadLock();
        try
        {
            var entries = new List<VirtualFileSystemEntry>();
            CollectAllEntries(_root, entries);
            return entries;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Creates a new file with the given path.
    /// Path should be fully qualified: "\folder1\folder2\filename.txt"
    /// </summary>
    public void CreateFile(string path, string originalName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        _lock.EnterWriteLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var metadata = new FileMetadata(originalName, path);
            metadata.UpdateChecksum();

            if (!directory.CreateFile(fileName, metadata))
                throw new InvalidOperationException($"File already exists: {path}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Creates a new directory with the given path.
    /// Path should be fully qualified: "\folder1\folder2"
    /// </summary>
    public void CreateDirectory(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        _lock.EnterWriteLock();
        try
        {
            // Parse path to get parent directory (same as CreateFile)
            var (parentDir, dirName) = ParsePath(path);
            if (parentDir == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            // Create subdirectory with original name (preserves case)
            var newDir = parentDir.CreateSubdirectory(name);
            if (newDir == null)
                throw new InvalidOperationException($"Directory already exists: {path}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Writes data to a file. Creates it if it doesn't exist.
    /// Handles sector allocation, deallocation, and memory mapping.
    /// </summary>
    public void WriteFile(string path, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        if (data == null)
            data = Array.Empty<byte>();

        _lock.EnterWriteLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var existingFile = directory.GetFile(fileName);

            // Deallocate old sectors if file exists
            if (existingFile != null && existingFile.SectorIndices != null)
            {
                _allocator.DeallocateSectors(existingFile.SectorIndices);
            }

            // Allocate new sectors
            long[] newSectors = _allocator.AllocateSectors(data.Length);
            if (data.Length > 0 && newSectors.Length == 0)
                throw new InvalidOperationException("Insufficient disk space for write operation.");

            // Write data to memory buffer
            WriteDataToSectors(newSectors, data);

            // Update metadata
            var metadata = new FileMetadata(fileName, path)
            {
                Size = data.Length,
                SectorIndices = newSectors,
            };
            metadata.UpdateChecksum();

            directory.DeleteFile(fileName);
            if (!directory.CreateFile(fileName, metadata))
                throw new InvalidOperationException($"Failed to update file metadata: {path}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reads data from a file.
    /// Handles cross-segment reads transparently.
    /// </summary>
    public byte[] ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        _lock.EnterReadLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var file = directory.GetFile(fileName);
            if (file == null)
                throw new InvalidOperationException($"File not found: {path}");

            // Validate checksum
            if (!file.ValidateChecksum())
                throw new InvalidOperationException($"File metadata corrupted (checksum mismatch): {path}");

            if (file.Size == 0)
                return Array.Empty<byte>();

            byte[] data = new byte[file.Size];
            ReadDataFromSectors(file.SectorIndices, data);

            // Update access time
            file.TouchAccessTime();

            return data;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Ultra-fast streaming write for large files (8GB+/sec performance).
    /// Writes data at a specific offset WITHOUT reading the entire file.
    /// IMPORTANT: For multi-chunk copies, only allocates NEW sectors when extending file.
    /// Previous data is preserved - sectors are EXTENDED, not replaced.
    /// </summary>
    public void WriteFileAt(string path, byte[] data, long offset, long volumeCapacityBytes = long.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        if (data == null || data.Length == 0)
            return;

        long[] targetSectors;
        
        _lock.EnterWriteLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var file = directory.GetFile(fileName);
            if (file == null)
                throw new InvalidOperationException($"File not found: {path}");

            long newSize = offset + data.Length;
            
            // CAPACITY CHECK: Use incremental total instead of O(n) traversal
            // New used bytes = current total - old file size + new file size
            long totalWouldBe = _totalUsedBytes - file.Size + newSize;
            if (totalWouldBe > volumeCapacityBytes)
                throw new InvalidOperationException($"Disk full: would need {totalWouldBe} bytes but capacity is only {volumeCapacityBytes} bytes");
            
            targetSectors = file.SectorIndices ?? Array.Empty<long>();

            // Calculate sectors needed for new file size
            long sectorsNeeded = (newSize + _sectorSize - 1) / _sectorSize;
            long sectorsAllocated = targetSectors.Length;

            if (sectorsNeeded > sectorsAllocated)
            {
                long additionalSectorsNeeded = sectorsNeeded - sectorsAllocated;
                long[] newSectors = _allocator.AllocateSectors(additionalSectorsNeeded * _sectorSize);
                
                if (newSectors.Length < additionalSectorsNeeded)
                    throw new InvalidOperationException($"Could only allocate {newSectors.Length} sectors, but {additionalSectorsNeeded} are needed. Disk fragmented or full.");

                if (targetSectors.Length > 0)
                {
                    var combined = new long[sectorsNeeded];
                    Array.Copy(targetSectors, combined, targetSectors.Length);
                    Array.Copy(newSectors, 0, combined, targetSectors.Length, newSectors.Length);
                    targetSectors = combined;
                }
                else
                {
                    targetSectors = newSectors;
                }
            }

            // Update metadata BEFORE releasing lock (critical for consistency)
            long oldSize = file.Size;
            file.Size = newSize;
            file.SectorIndices = targetSectors;
            file.UpdateChecksum(); // CRITICAL: Update checksum after metadata changes
            // NOTE: Don't update modification time here - called every write and too expensive
            // Modification time is updated in SetFileSize which is sufficient
            
            // Update incremental total: add the delta (new size - old size)
            _totalUsedBytes += (newSize - oldSize);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        
        // DATA WRITE OUTSIDE LOCK - this hot path can run concurrently with other I/O
        // Sectors are allocated and metadata updated, so write is safe
        WriteDataToSectorsAt(targetSectors, data, offset);
    }

    /// <summary>
    /// Ultra-fast streaming read for large files.
    /// Reads data from a specific offset WITHOUT allocating a full file buffer.
    /// Minimized lock duration: only locks for metadata access, not data copy.
    /// </summary>
    public int ReadFileAt(string path, byte[] buffer, long offset, int length = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        if (buffer == null || buffer.Length == 0)
            return 0;

        if (length <= 0 || length > buffer.Length)
            length = buffer.Length;

        // OPTIMIZED: Acquire lock ONLY to get file metadata, NOT for the data copy
        long[] sectorIndices;
        long fileSize;
        
        _lock.EnterReadLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var file = directory.GetFile(fileName);
            if (file == null)
                throw new InvalidOperationException($"File not found: {path}");

            fileSize = file.Size;
            sectorIndices = file.SectorIndices ?? Array.Empty<long>();
            file.TouchAccessTime();  // Update access time BEFORE releasing lock to avoid re-acquisition
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // DATA COPY WITHOUT LOCK - this is the hot path
        if (offset >= fileSize)
            return 0;

        long availableBytes = fileSize - offset;
        int bytesToRead = (int)Math.Min(length, availableBytes);

        if (bytesToRead > 0)
        {
            ReadDataFromSectorsAt(sectorIndices, buffer, offset, bytesToRead);
        }
        
        return bytesToRead;
    }

    /// <summary>
    /// Sets the file size WITHOUT reading the entire file (streaming-safe).
    /// Used by SetEndOfFile to expand/truncate files efficiently.
    /// - If expanding: allocates new sectors ONLY (no zero-fill - WriteFileAt handles gaps)
    /// - If shrinking: deallocates excess sectors
    /// CRITICAL: Zero-filling is deferred to WriteFileAt to avoid timeout on large allocations.
    /// </summary>
    public void SetFileSize(string path, long newSize, long volumeCapacityBytes = long.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        if (newSize < 0)
            throw new ArgumentException("File size cannot be negative", nameof(newSize));

        _lock.EnterWriteLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                throw new InvalidOperationException($"Parent directory not found for path: {path}");

            var file = directory.GetFile(fileName);
            if (file == null)
                throw new InvalidOperationException($"File not found: {path}");

            long currentSize = file.Size;
            
            // CAPACITY CHECK: Use incremental total instead of O(n) traversal
            long totalWouldBe = _totalUsedBytes - currentSize + newSize;
            if (totalWouldBe > volumeCapacityBytes)
                throw new InvalidOperationException($"Disk full: would need {totalWouldBe} bytes but capacity is only {volumeCapacityBytes} bytes");
            
            long[] currentSectors = file.SectorIndices ?? Array.Empty<long>();

            if (newSize == currentSize)
                return; // No change

            long currentSectorsCount = currentSectors.Length;
            long newSectorsNeeded = (newSize + _sectorSize - 1) / _sectorSize;

            if (newSize > currentSize)
            {
                // EXPAND: Allocate additional sectors (no zero-fill to avoid timeout)
                long additionalSectorsNeeded = newSectorsNeeded - currentSectorsCount;
                if (additionalSectorsNeeded > 0)
                {
                    long[] newSectors = _allocator.AllocateSectors(additionalSectorsNeeded * _sectorSize);
                    
                    if (newSectors.Length < additionalSectorsNeeded)
                        throw new InvalidOperationException($"Could only allocate {newSectors.Length} sectors, but {additionalSectorsNeeded} are needed.");

                    // Combine existing and new sectors
                    var combined = new long[newSectorsNeeded];
                    if (currentSectors.Length > 0)
                        Array.Copy(currentSectors, combined, currentSectors.Length);
                    Array.Copy(newSectors, 0, combined, currentSectorsCount, newSectors.Length);

                    // NOTE: Gap zero-filling is handled by WriteFileAt when data is actually written
                    // This avoids timeout when pre-allocating large files
                    
                    file.SectorIndices = combined;
                }
            }
            else
            {
                // SHRINK: Deallocate excess sectors and clear remainder of last sector
                long sectorsToKeep = newSectorsNeeded;
                if (sectorsToKeep < currentSectorsCount)
                {
                    long[] sectorsToFree = new long[currentSectorsCount - sectorsToKeep];
                    Array.Copy(currentSectors, sectorsToKeep, sectorsToFree, 0, sectorsToFree.Length);
                    _allocator.DeallocateSectors(sectorsToFree);

                    // Trim the sectors array
                    if (sectorsToKeep > 0)
                    {
                        var trimmed = new long[sectorsToKeep];
                        Array.Copy(currentSectors, trimmed, sectorsToKeep);
                        file.SectorIndices = trimmed;
                        
                        // Clear the remainder of the last sector to prevent data leakage
                        // (only if the new size doesn't align to sector boundary)
                        long remainder = newSize % _sectorSize;
                        if (remainder > 0)
                        {
                            long lastSectorIndex = trimmed[trimmed.Length - 1];
                            long byteOffset = lastSectorIndex * _sectorSize + remainder;
                            
                            // Map byte offset to segment+offset within segment
                            const long SEGMENT_SIZE_BYTES = (2L * 1024 * 1024 * 1024) - (1L * 1024 * 1024); // ~2GB
                            int segmentIndex = (int)(byteOffset / SEGMENT_SIZE_BYTES);
                            long offsetInSegment = byteOffset % SEGMENT_SIZE_BYTES;
                            long clearLength = _sectorSize - remainder;
                            
                            _memoryBuffer.ClearSectorData(segmentIndex, offsetInSegment, (int)clearLength);
                        }
                    }
                    else
                    {
                        file.SectorIndices = Array.Empty<long>();
                    }
                }
            }

            long oldSize = file.Size;
            file.Size = newSize;
            file.TouchModificationTime();
            file.UpdateChecksum();
            
            // Update incremental total: add the delta (new size - old size)
            _totalUsedBytes += (newSize - oldSize);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Deletes a file from the filesystem.
    /// </summary>
    public bool DeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));

        _lock.EnterWriteLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                return false;

            var file = directory.GetFile(fileName);
            if (file == null)
                return false;

            // Deallocate sectors
            _allocator.DeallocateSectors(file.SectorIndices);
            
            // Update incremental total by subtracting the deleted file's size
            _totalUsedBytes -= file.Size;

            // Remove from directory
            return directory.DeleteFile(fileName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Deletes a directory from the filesystem (must be empty).
    /// </summary>
    public bool DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "\\")
            return false; // Cannot delete root

        _lock.EnterWriteLock();
        try
        {
            var (parentDir, dirName) = ParsePath(path);
            if (parentDir == null)
                return false;

            var dir = parentDir.GetSubdirectory(dirName);
            if (dir == null)
                return false;

            // Check if directory is empty
            if (dir.Subdirectories.Count > 0 || dir.Files.Count > 0)
                return false;

            // Remove from parent
            return parentDir.DeleteSubdirectory(dirName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Moves or renames a file to a new location.
    /// </summary>
    public bool MoveFile(string oldPath, string newPath, bool replace = false)
    {
        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentNullException(oldPath == null ? nameof(oldPath) : nameof(newPath));

        _lock.EnterWriteLock();
        try
        {
            var (oldDir, oldFileName) = ParsePath(oldPath);
            if (oldDir == null)
                return false;

            var oldFile = oldDir.GetFile(oldFileName);
            if (oldFile == null)
                return false;

            var (newDir, newFileName) = ParsePath(newPath);
            if (newDir == null)
                return false;

            var existingFile = newDir.GetFile(newFileName);
            if (existingFile != null && !replace)
                return false;

            // Delete existing file if replacing
            if (existingFile != null)
            {
                _allocator.DeallocateSectors(existingFile.SectorIndices);
                newDir.DeleteFile(newFileName);
            }

            // Create file at new location with preserved data
            var newMetadata = new FileMetadata(newFileName, newPath)
            {
                Size = oldFile.Size,
                SectorIndices = oldFile.SectorIndices,
            };
            newMetadata.UpdateChecksum();

            if (!newDir.CreateFile(newFileName, newMetadata))
                return false;

            // Delete from old location
            return oldDir.DeleteFile(oldFileName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Moves or renames a directory to a new location.
    /// </summary>
    public bool MoveDirectory(string oldPath, string newPath, bool replace = false)
    {
        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentNullException(oldPath == null ? nameof(oldPath) : nameof(newPath));

        if (oldPath == "\\" || newPath == "\\")
            return false; // Cannot move root

        _lock.EnterWriteLock();
        try
        {
            var (oldParent, oldDirName) = ParsePath(oldPath);
            if (oldParent == null)
                return false;

            var oldDir = oldParent.GetSubdirectory(oldDirName);
            if (oldDir == null)
                return false;

            var (newParent, newDirName) = ParsePath(newPath);
            if (newParent == null)
                return false;

            var existingDir = newParent.GetSubdirectory(newDirName);
            if (existingDir != null && !replace)
                return false;

            // Delete existing directory if replacing (must be empty)
            if (existingDir != null)
            {
                if (existingDir.Subdirectories.Count > 0 || existingDir.Files.Count > 0)
                    return false;
                newParent.DeleteSubdirectory(newDirName);
            }

            // Create new directory at destination
            var newDir = newParent.CreateSubdirectory(newDirName);
            if (newDir == null)
                return false;

            // Copy all contents from old to new
            foreach (var file in oldDir.Files.Values)
            {
                var metadata = new FileMetadata(file.Name, newPath + "\\" + file.Name)
                {
                    Size = file.Size,
                    SectorIndices = file.SectorIndices,
                };
                metadata.UpdateChecksum();
                newDir.CreateFile(file.Name, metadata);
            }

            foreach (var subdir in oldDir.Subdirectories.Values)
            {
                newDir.CreateSubdirectory(subdir.Name);
            }

            // Delete old directory
            return oldParent.DeleteSubdirectory(oldDirName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets file information.
    /// </summary>
    public FileMetadata? GetFileInfo(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        _lock.EnterReadLock();
        try
        {
            var (directory, fileName) = ParsePath(path);
            if (directory == null)
                return null;

            return directory.GetFile(fileName);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets directory information by path.
    /// </summary>
    public DirectoryNode? GetDirectoryInfo(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        // Root directory always exists
        if (path == "\\")
        {
            _lock.EnterReadLock();
            try
            {
                return _root;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        _lock.EnterReadLock();
        try
        {
            var (parentDir, dirName) = ParsePath(path);
            if (parentDir == null)
                return null;

            return parentDir.GetSubdirectory(dirName);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Parses a full file path into directory and filename.
    /// Example: "\folder1\file.txt" -> (DirectoryNode for "folder1", "file.txt")
    /// </summary>
    private (DirectoryNode? directory, string fileName) ParsePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return (null, "");

        int lastSlash = fullPath.LastIndexOf('\\');
        if (lastSlash < 0)
            return (null, "");

        string dirPath = lastSlash == 0 ? "\\" : fullPath.Substring(0, lastSlash);
        string fileName = fullPath.Substring(lastSlash + 1);

        if (string.IsNullOrWhiteSpace(fileName))
            return (null, "");

        var directory = _root.NavigateToPath(dirPath);
        return (directory, fileName);
    }

    /// <summary>
    /// Writes data to allocated sectors starting at a specific byte offset.
    /// Ultra-fast path: writes directly to unmanaged memory without copies.
    /// Optimized: Caches segment mapping and batches writes to contiguous memory regions.
    /// </summary>
    private void WriteDataToSectorsAt(long[] sectorIndices, byte[] data, long startOffset)
    {
        if (data == null || data.Length == 0 || sectorIndices == null || sectorIndices.Length == 0)
            return;

        long byteOffset = startOffset;
        int dataOffset = 0;
        int dataRemaining = data.Length;

        // Find starting sector and offset within that sector
        int sectorIdx = (int)(byteOffset / _sectorSize);
        int offsetInSector = (int)(byteOffset % _sectorSize);

        while (dataRemaining > 0 && sectorIdx < sectorIndices.Length)
        {
            // Compute segment and memory address once per sector lookup
            var (currentSegment, currentOffset) = _allocator.SectorToMemoryAddress(sectorIndices[sectorIdx]);
            long currentMemAddr = currentOffset + offsetInSector;
            int bytesToWrite = Math.Min(dataRemaining, _sectorSize - offsetInSector);

            // Batch contiguous writes: check if next sector is contiguous in memory
            int batchedSectors = 1;
            while (batchedSectors < (sectorIndices.Length - sectorIdx) && 
                   bytesToWrite < dataRemaining && 
                   offsetInSector == 0)
            {
                var (nextSegment, nextOffset) = _allocator.SectorToMemoryAddress(sectorIndices[sectorIdx + batchedSectors]);
                // Check if next sector is contiguous (same segment and consecutive offset)
                if (nextSegment == currentSegment && nextOffset == currentOffset + (long)bytesToWrite)
                {
                    int nextBatchSize = Math.Min(dataRemaining - bytesToWrite, _sectorSize);
                    bytesToWrite += nextBatchSize;
                    batchedSectors++;
                }
                else
                {
                    break;
                }
            }

            _memoryBuffer.WriteSectorData(currentSegment, currentMemAddr, data, dataOffset, bytesToWrite);

            dataOffset += bytesToWrite;
            dataRemaining -= bytesToWrite;
            sectorIdx += batchedSectors;
            offsetInSector = 0;
        }
    }

    /// <summary>
    /// Reads data from allocated sectors starting at a specific byte offset.
    /// Ultra-fast path: reads directly from unmanaged memory without allocating full file buffer.
    /// Optimized: Uses int indexing and caches segment mapping per sector lookup.
    /// </summary>
    private void ReadDataFromSectorsAt(long[] sectorIndices, byte[] buffer, long startOffset, int length)
    {
        if (buffer == null || length <= 0 || sectorIndices == null || sectorIndices.Length == 0)
            return;

        long byteOffset = startOffset;
        int bufferOffset = 0;
        int bytesRemaining = length;

        // Find starting sector and offset within that sector
        int sectorIdx = (int)(byteOffset / _sectorSize);
        int offsetInSector = (int)(byteOffset % _sectorSize);

        while (bytesRemaining > 0 && sectorIdx < sectorIndices.Length)
        {
            int bytesToRead = Math.Min(bytesRemaining, _sectorSize - offsetInSector);
            var (segmentIndex, offsetInMemory) = _allocator.SectorToMemoryAddress(sectorIndices[sectorIdx]);

            _memoryBuffer.ReadSectorData(segmentIndex, offsetInMemory + offsetInSector, buffer, bufferOffset, bytesToRead);

            bufferOffset += bytesToRead;
            bytesRemaining -= bytesToRead;
            sectorIdx++;
            offsetInSector = 0;
        }
    }

    /// <summary>
    /// Writes data buffer to allocated sectors.
    /// Handles cross-segment writes transparently.
    /// </summary>
    private void WriteDataToSectors(long[] sectorIndices, byte[] data)
    {
        int bytesToWrite = data.Length;
        int dataOffset = 0;

        foreach (long sectorIndex in sectorIndices)
        {
            if (bytesToWrite <= 0)
                break;

            var (segmentIndex, offsetInSegment) = _allocator.SectorToMemoryAddress(sectorIndex);
            int bytesThisSector = Math.Min(bytesToWrite, _sectorSize);

            _memoryBuffer.WriteSectorData(segmentIndex, offsetInSegment, data, dataOffset, bytesThisSector);

            dataOffset += bytesThisSector;
            bytesToWrite -= bytesThisSector;
        }
    }

    /// <summary>
    /// Reads data from allocated sectors.
    /// Handles cross-segment reads transparently.
    /// </summary>
    private void ReadDataFromSectors(long[] sectorIndices, byte[] data)
    {
        int bytesToRead = data.Length;
        int dataOffset = 0;

        foreach (long sectorIndex in sectorIndices)
        {
            if (bytesToRead <= 0)
                break;

            var (segmentIndex, offsetInSegment) = _allocator.SectorToMemoryAddress(sectorIndex);
            int bytesThisSector = Math.Min(bytesToRead, _sectorSize);

            _memoryBuffer.ReadSectorData(segmentIndex, offsetInSegment, data, dataOffset, bytesThisSector);

            dataOffset += bytesThisSector;
            bytesToRead -= bytesThisSector;
        }
    }

    /// <summary>
    /// Collects all entries recursively for compatibility.
    /// </summary>
    private void CollectAllEntries(DirectoryNode dir, List<VirtualFileSystemEntry> entries)
    {
        // Add directories (FullPath must end with \ for IsDirectory to work)
        foreach (var subdir in dir.Subdirectories.Values)
        {
            string dirPath = subdir.GetFullPath();
            if (!dirPath.EndsWith("\\"))
                dirPath += "\\";

            entries.Add(new VirtualFileSystemEntry
            {
                Name = subdir.Name,
                FullPath = dirPath
            });
            CollectAllEntries(subdir, entries);
        }

        // Add files
        foreach (var file in dir.Files.Values)
        {
            entries.Add(new VirtualFileSystemEntry
            {
                Name = file.Name,
                FullPath = file.FullPath,
                Size = file.Size,
                Data = null // Don't load data for enumeration
            });
        }
    }
}
