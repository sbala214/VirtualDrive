using System;
using System.Collections.Generic;

namespace VirtualDrive.Core;

/// <summary>
/// Manages sector-based allocation for the virtual filesystem.
/// Uses a bitmap to track 4KB sectors: 100GB = 26,214,400 sectors = 3.125MB bitmap
/// </summary>
public class SectorAllocator
{
    private const int SECTOR_SIZE = 8192; // 8KB sectors
    private const long MAX_CAPACITY_BYTES = 100L * 1024 * 1024 * 1024; // 100GB
    private const long TOTAL_SECTORS = MAX_CAPACITY_BYTES / SECTOR_SIZE;

    private readonly bool[] _sectorBitmap;
    private readonly object _allocationLock = new object();
    private long _nextFreeSector = 0;  // Pointer to speed up searches (avoid O(n) scans from 0)

    public SectorAllocator()
    {
        _sectorBitmap = new bool[TOTAL_SECTORS];
        UsedSectors = 0;
    }

    public long UsedSectors { get; private set; }

    public long UsedBytes => UsedSectors * SECTOR_SIZE;
    public long AvailableBytes => (TOTAL_SECTORS - UsedSectors) * SECTOR_SIZE;
    public long Capacity => MAX_CAPACITY_BYTES;

    /// <summary>
    /// Allocates contiguous sectors for a file of given size.
    /// Optimized: Uses _nextFreeSector pointer to avoid O(n) scans from 0.
    /// Returns array of sector indices, or empty array if allocation fails.
    /// </summary>
    public long[] AllocateSectors(long sizeBytes)
    {
        if (sizeBytes <= 0)
            return Array.Empty<long>();

        long sectorsNeeded = (sizeBytes + SECTOR_SIZE - 1) / SECTOR_SIZE;

        lock (_allocationLock)
        {
            if (UsedSectors + sectorsNeeded > TOTAL_SECTORS)
                return Array.Empty<long>(); // Out of space

            var allocatedSectors = new List<long>();

            // OPTIMIZED: Try contiguous allocation starting from _nextFreeSector (not 0)
            long consecutiveCount = 0;
            long startSector = -1;
            long scanStart = _nextFreeSector;
            long i = scanStart;

            // Scan forward from _nextFreeSector to find contiguous space
            while (i < TOTAL_SECTORS && consecutiveCount < sectorsNeeded)
            {
                if (!_sectorBitmap[i])
                {
                    if (consecutiveCount == 0)
                        startSector = i;
                    consecutiveCount++;
                    i++;
                }
                else
                {
                    consecutiveCount = 0;
                    i++;
                }
            }

            // If found enough contiguous, allocate them
            if (consecutiveCount >= sectorsNeeded)
            {
                for (long j = 0; j < sectorsNeeded; j++)
                {
                    _sectorBitmap[startSector + j] = true;
                    allocatedSectors.Add(startSector + j);
                }
                UsedSectors += sectorsNeeded;
                _nextFreeSector = startSector + sectorsNeeded;  // Update pointer past allocation
                return allocatedSectors.ToArray();
            }

            // OPTIMIZED: If not enough contiguous, allocate fragmented starting from _nextFreeSector
            for (long j = scanStart; j < TOTAL_SECTORS && allocatedSectors.Count < sectorsNeeded; j++)
            {
                if (!_sectorBitmap[j])
                {
                    allocatedSectors.Add(j);
                    _sectorBitmap[j] = true;
                }
            }

            // If still not enough, wrap around and scan from 0
            if (allocatedSectors.Count < sectorsNeeded)
            {
                for (long j = 0; j < scanStart && allocatedSectors.Count < sectorsNeeded; j++)
                {
                    if (!_sectorBitmap[j])
                    {
                        allocatedSectors.Add(j);
                        _sectorBitmap[j] = true;
                    }
                }
            }

            if (allocatedSectors.Count < sectorsNeeded)
            {
                // Allocation failed, rollback
                foreach (var sector in allocatedSectors)
                    _sectorBitmap[sector] = false;
                return Array.Empty<long>();
            }

            UsedSectors += allocatedSectors.Count;
            // Update _nextFreeSector to first allocation (for next search)
            if (allocatedSectors.Count > 0)
                _nextFreeSector = allocatedSectors[allocatedSectors.Count - 1] + 1;
            if (_nextFreeSector >= TOTAL_SECTORS)
                _nextFreeSector = 0;  // Wrap around
            return allocatedSectors.ToArray();
        }
    }

    /// <summary>
    /// Deallocates sectors, freeing them for reuse by future allocations.
    /// Marks sectors as free in the bitmap and decrements the used sector count.
    /// </summary>
    /// <param name="sectorIndices">Array of sector indices to deallocate</param>
    public void DeallocateSectors(long[] sectorIndices)
    {
        if (sectorIndices == null || sectorIndices.Length == 0)
            return;

        lock (_allocationLock)
        {
            foreach (var sector in sectorIndices)
            {
                if (sector >= 0 && sector < TOTAL_SECTORS && _sectorBitmap[sector])
                {
                    _sectorBitmap[sector] = false;
                    UsedSectors--;
                }
            }
        }
    }

    /// <summary>
    /// Converts a sector index to memory segment address (segment index, offset).
    /// Used internally to locate file data in the segmented memory buffer.
    /// </summary>
    /// <param name="sectorIndex">Sector number to convert</param>
    /// <returns>Tuple of (segment index, offset in segment)</returns>
    public (int segmentIndex, long offsetInSegment) SectorToMemoryAddress(long sectorIndex)
    {
        const long SEGMENT_SIZE_BYTES = (2L * 1024 * 1024 * 1024) - (1L * 1024 * 1024); // MUST match SegmentedMemoryBuffer!
        const long SEGMENTS_IN_BYTES = SEGMENT_SIZE_BYTES / SECTOR_SIZE;
        const int SECTOR_SHIFT = 13; // SECTOR_SIZE = 8192 = 2^13
        int segment = (int)(sectorIndex / SEGMENTS_IN_BYTES);
        long offset = ((sectorIndex % SEGMENTS_IN_BYTES) << SECTOR_SHIFT); // Use shift instead of multiply
        return (segment, offset);
    }

    /// <summary>
    /// Converts a memory segment address back to sector index.
    /// Inverse operation of SectorToMemoryAddress for verification or debugging.
    /// </summary>
    /// <param name="segmentIndex">Memory segment index</param>
    /// <param name="offsetInSegment">Offset within the segment</param>
    /// <returns>Corresponding sector index</returns>
    public long MemoryAddressToSector(int segmentIndex, long offsetInSegment)
    {
        const long SEGMENT_SIZE_BYTES = (2L * 1024 * 1024 * 1024) - (1L * 1024 * 1024); // MUST match SegmentedMemoryBuffer!
        const long SEGMENTS_IN_BYTES = SEGMENT_SIZE_BYTES / SECTOR_SIZE;
        return segmentIndex * SEGMENTS_IN_BYTES + (offsetInSegment / SECTOR_SIZE);
    }

    /// <summary>
    /// Gets the size of a single sector in bytes.
    /// Currently 8192 bytes (8KB).
    /// </summary>
    /// <returns>Sector size in bytes</returns>
    public static int GetSectorSize() => SECTOR_SIZE;
}
