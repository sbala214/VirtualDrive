using System;
using System.Security.Cryptography;

namespace VirtualDrive.Core;

/// <summary>
/// Metadata for a file stored in the virtual filesystem.
/// Includes sector allocation, size, timestamps, and checksum for corruption detection.
/// </summary>
public class FileMetadata
{
    /// <summary>File name without path</summary>
    public string Name { get; set; }
    
    /// <summary>Full file path from volume root</summary>
    public string FullPath { get; set; }
    
    /// <summary>File size in bytes</summary>
    public long Size { get; set; }
    
    /// <summary>Array of sector indices where file data is stored</summary>
    public long[] SectorIndices { get; set; }
    
    /// <summary>Timestamp when file was created</summary>
    public DateTime CreatedTime { get; set; }
    
    /// <summary>Timestamp of last modification</summary>
    public DateTime LastModifiedTime { get; set; }
    
    /// <summary>Timestamp of last access</summary>
    public DateTime LastAccessedTime { get; set; }
    
    /// <summary>CRC32 checksum for integrity verification</summary>
    public uint Checksum { get; set; }

    /// <summary>
    /// Creates file metadata with default values.
    /// All timestamps initialized to current UTC time, checksum set to 0.
    /// </summary>
    public FileMetadata()
    {
        Name = "";
        FullPath = "";
        Size = 0;
        SectorIndices = Array.Empty<long>();
        CreatedTime = DateTime.UtcNow;
        LastModifiedTime = DateTime.UtcNow;
        LastAccessedTime = DateTime.UtcNow;
        Checksum = 0;
    }

    /// <summary>
    /// Creates file metadata with specified name and path.
    /// All timestamps initialized to current UTC time.
    /// </summary>
    /// <param name="name">File name</param>
    /// <param name="fullPath">Full file path</param>
    public FileMetadata(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
        Size = 0;
        SectorIndices = Array.Empty<long>();
        CreatedTime = DateTime.UtcNow;
        LastModifiedTime = DateTime.UtcNow;
        LastAccessedTime = DateTime.UtcNow;
        Checksum = 0;
    }

    /// <summary>
    /// Calculates CRC32 checksum based on current metadata state.
    /// Used to detect corruption of file metadata.
    /// </summary>
    /// <returns>CRC32 checksum value</returns>
    public uint CalculateChecksum()
    {
        return Crc32(Name + FullPath + Size + SectorIndices.Length);
    }

    /// <summary>
    /// Validates that the stored checksum matches the current metadata state.
    /// Returns false if metadata has been corrupted or modified improperly.
    /// </summary>
    /// <returns>True if checksum is valid, false otherwise</returns>
    public bool ValidateChecksum()
    {
        return Checksum == CalculateChecksum();
    }

    /// <summary>
    /// Recalculates and stores the checksum based on current metadata.
    /// Call after modifying metadata to keep checksum valid.
    /// </summary>
    public void UpdateChecksum()
    {
        Checksum = CalculateChecksum();
    }

    /// <summary>
    /// Computes CRC32 checksum for the given input string.
    /// Uses standard CRC-32 polynomial. Not cryptographically secure, for integrity only.
    /// </summary>
    /// <param name="input">String to checksum</param>
    /// <returns>CRC32 checksum value</returns>
    private static uint Crc32(string input)
    {
        const uint polynomial = 0xedb88320;
        uint crc = 0xffffffff;

        foreach (char c in input)
        {
            byte byte_val = (byte)c;
            crc ^= byte_val;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ polynomial;
                else
                    crc >>= 1;
            }
        }

        return crc ^ 0xffffffff;
    }

    /// <summary>
    /// Gets total allocated bytes for this file (sector count * sector size).
    /// </summary>
    public long GetAllocatedBytes()
    {
        return (SectorIndices?.Length ?? 0) * SectorAllocator.GetSectorSize();
    }

    /// <summary>
    /// Updates access time to current UTC.
    /// </summary>
    public void TouchAccessTime()
    {
        LastAccessedTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates modification time to current UTC.
    /// </summary>
    public void TouchModificationTime()
    {
        LastModifiedTime = DateTime.UtcNow;
    }
}
