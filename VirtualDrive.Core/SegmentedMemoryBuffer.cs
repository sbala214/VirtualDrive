using System;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable CS4710 // Don't warn about unsafe code

namespace VirtualDrive.Core;

/// <summary>
/// Ultra-high-performance unmanaged memory buffer with configurable capacity.
/// Uses segmented allocation via unmanaged memory (Marshal.AllocHGlobal).
/// Enables direct memory access for 8GB+/sec I/O without GC overhead or copies.
/// </summary>
public unsafe class SegmentedMemoryBuffer : IDisposable
{
    private readonly int _segmentCount;
    private readonly long _segmentSizeBytes;
    private readonly IntPtr[] _segments;  // Unmanaged memory pointers
    private readonly object _allocationLock = new object();  // Only for segment allocation, not hot path
    private bool _disposed;

    /// <summary>
    /// Creates a buffer with default configuration (50 × 2GB = ~98GB).
    /// </summary>
    public SegmentedMemoryBuffer() : this(MemoryBufferConfiguration.CreateDefault())
    {
    }

    /// <summary>
    /// Creates a buffer with custom configuration.
    /// </summary>
    public SegmentedMemoryBuffer(MemoryBufferConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (config.SegmentCount <= 0)
            throw new ArgumentException("SegmentCount must be greater than 0", nameof(config));
        if (config.SegmentSizeBytes <= 0)
            throw new ArgumentException("SegmentSizeBytes must be greater than 0", nameof(config));

        _segmentCount = config.SegmentCount;
        _segmentSizeBytes = config.SegmentSizeBytes;

        _segments = new IntPtr[_segmentCount];
        for (int i = 0; i < _segmentCount; i++)
        {
            _segments[i] = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Total capacity available.
    /// </summary>
    public long Capacity => _segmentCount * _segmentSizeBytes;

    /// <summary>
    /// Allocates a segment on-demand via unmanaged memory (assumes caller holds appropriate lock).
    /// Use only when already holding a write lock on this segment.
    /// </summary>
    private void EnsureSegmentAllocatedUnsafe(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= _segmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        if (_segments[segmentIndex] == IntPtr.Zero)
        {
            _segments[segmentIndex] = Marshal.AllocHGlobal((int)_segmentSizeBytes);
        }
    }

    /// <summary>
    /// Allocates a segment on-demand via unmanaged memory (thread-safe).
    /// </summary>
    private void EnsureSegmentAllocated(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= _segmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        // Only lock during allocation. Most of the time this is skipped (segment already allocated).
        if (_segments[segmentIndex] == IntPtr.Zero)
        {
            lock (_allocationLock)
            {
                // Double-check after acquiring lock
                if (_segments[segmentIndex] == IntPtr.Zero)
                {
                    _segments[segmentIndex] = Marshal.AllocHGlobal((int)_segmentSizeBytes);
                }
            }
        }
    }

    /// <summary>
    /// Gets direct pointer to segment memory for ultra-high-speed writes (no copy required).
    /// </summary>
    public IntPtr GetSegmentPointer(int segmentIndex, int offsetInSegment)
    {
        if (segmentIndex < 0 || segmentIndex >= _segmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));

        EnsureSegmentAllocated(segmentIndex);
        return new IntPtr(_segments[segmentIndex].ToInt64() + offsetInSegment);
    }

    /// <summary>
    /// Writes data to unmanaged memory using direct memory copy (ultra-fast, no managed allocation).
    /// Does not allocate any managed objects on the GC heap.
    /// </summary>
    /// <param name="destination">Destination memory address</param>
    /// <param name="source">Source byte array</param>
    /// <param name="length">Number of bytes to copy</param>
    public unsafe void WriteDirectUnsafe(IntPtr destination, byte[] source, int length)
    {
        if (source == null || length <= 0)
            return;

        fixed (byte* srcPtr = source)
        {
            Buffer.MemoryCopy(srcPtr, (void*)destination, long.MaxValue, length);
        }
    }

    /// <summary>
    /// Reads data from unmanaged memory using direct memory copy (ultra-fast).
    /// Does not allocate managed arrays beyond the destination parameter.
    /// </summary>
    /// <param name="source">Source memory address</param>
    /// <param name="destination">Destination byte array to read into</param>
    /// <param name="length">Number of bytes to copy</param>
    public unsafe void ReadDirectUnsafe(IntPtr source, byte[] destination, int length)
    {
        if (destination == null || length <= 0)
            return;

        fixed (byte* dstPtr = destination)
        {
            Buffer.MemoryCopy((void*)source, dstPtr, long.MaxValue, length);
        }
    }

    /// <summary>
    /// Writes data to a specific sector using segment-based addressing.
    /// Data may span from dataOffset in source array to specified segment location.
    /// </summary>
    /// <param name="segmentIndex">Segment number</param>
    /// <param name="offsetInSegment">Byte offset within the segment</param>
    /// <param name="data">Source data array</param>
    /// <param name="dataOffset">Starting offset in source array</param>
    /// <param name="length">Number of bytes to write</param>
    public unsafe void WriteSectorData(int segmentIndex, long offsetInSegment, byte[] data, int dataOffset, int length)
    {
        if (data == null || length <= 0)
            return;

        EnsureSegmentAllocated(segmentIndex);
        IntPtr destPtr = new IntPtr(_segments[segmentIndex].ToInt64() + offsetInSegment);
        
        fixed (byte* srcPtr = data)
        {
            Buffer.MemoryCopy(srcPtr + dataOffset, (void*)destPtr, long.MaxValue, length);
        }
    }

    /// <summary>
    /// Reads data from a specific sector using segment-based addressing.
    /// </summary>
    /// <param name="segmentIndex">Segment number</param>
    /// <param name="offsetInSegment">Byte offset within the segment</param>
    /// <param name="data">Destination data array</param>
    /// <param name="dataOffset">Starting offset in destination array</param>
    /// <param name="length">Number of bytes to read</param>
    public unsafe void ReadSectorData(int segmentIndex, long offsetInSegment, byte[] data, int dataOffset, int length)
    {
        if (data == null || length <= 0)
            return;

        if (_segments[segmentIndex] == IntPtr.Zero)
            throw new InvalidOperationException("Segment not allocated.");

        IntPtr srcPtr = new IntPtr(_segments[segmentIndex].ToInt64() + offsetInSegment);
        
        fixed (byte* dstPtr = data)
        {
            Buffer.MemoryCopy((void*)srcPtr, dstPtr + dataOffset, long.MaxValue, length);
        }
    }

    /// <summary>
    /// Zeros out memory in a sector range using efficient memory clearing.
    /// Use when truncating files to prevent exposing old data from previous allocations.
    /// Should not be used if the entire sector will be immediately overwritten.
    /// </summary>
    /// <param name="segmentIndex">Segment number</param>
    /// <param name="offsetInSegment">Byte offset within the segment</param>
    /// <param name="length">Number of bytes to zero</param>
    public unsafe void ClearSectorData(int segmentIndex, long offsetInSegment, int length)
    {
        if (length <= 0)
            return;

        EnsureSegmentAllocated(segmentIndex);
        IntPtr ptr = new IntPtr(_segments[segmentIndex].ToInt64() + offsetInSegment);
        
        // Use ZeroMemory for fast zeroing
        ZeroMemory((void*)ptr, (ulong)length);
    }

    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern unsafe void ZeroMemory(void* ptr, ulong count);

    /// <summary>
    /// Gets the total bytes allocated across all segments that have been allocated so far.
    /// Does not include segments that haven't been allocated yet (on-demand allocation).
    /// </summary>
    /// <returns>Total allocated bytes</returns>
    public long GetAllocatedBytes()
    {
        long total = 0;
        for (int i = 0; i < _segmentCount; i++)
        {
            // No lock needed - reading an IntPtr is atomic and safe
            // Even if we see a null while another thread allocates, that's fine
            if (_segments[i] != IntPtr.Zero)
                total += (int)_segmentSizeBytes;
        }
        return total;
    }

    /// <summary>
    /// Cleans up unmanaged memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_allocationLock)
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                if (_segments[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_segments[i]);
                    _segments[i] = IntPtr.Zero;
                }
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~SegmentedMemoryBuffer()
    {
        Dispose();
    }
}
