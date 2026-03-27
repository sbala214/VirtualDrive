namespace VirtualDrive.Core;

/// <summary>
/// Configuration for SegmentedMemoryBuffer behavior.
/// Allows customization of segment count and size for different use cases.
/// </summary>
public class MemoryBufferConfiguration
{
    /// <summary>
    /// Number of segments to allocate (default: 50).
    /// Each segment can hold 2GB (or configured segment size).
    /// Total capacity = SegmentCount × SegmentSizeBytes
    /// </summary>
    public int SegmentCount { get; set; } = 50;

    /// <summary>
    /// Size of each segment in bytes (default: ~2GB).
    /// Must fit within IntPtr range for Marshal.AllocHGlobal.
    /// Typical value: (2 * 1024 * 1024 * 1024) - (1 * 1024 * 1024) = ~2GB
    /// </summary>
    public long SegmentSizeBytes { get; set; } = (2L * 1024 * 1024 * 1024) - (1L * 1024 * 1024);

    /// <summary>
    /// Gets the total maximum capacity in bytes.
    /// </summary>
    public long MaxCapacityBytes => SegmentCount * SegmentSizeBytes;

    /// <summary>
    /// Creates a default configuration (50 × 2GB = ~98GB).
    /// </summary>
    public static MemoryBufferConfiguration CreateDefault() => new();

    /// <summary>
    /// Creates a small configuration for testing (10 × 256MB = 2.5GB).
    /// </summary>
    public static MemoryBufferConfiguration CreateSmall() => new()
    {
        SegmentCount = 10,
        SegmentSizeBytes = 256 * 1024 * 1024 // 256MB
    };

    /// <summary>
    /// Creates a large configuration (100 × 2GB = ~200GB).
    /// </summary>
    public static MemoryBufferConfiguration CreateLarge() => new()
    {
        SegmentCount = 100,
        SegmentSizeBytes = (2L * 1024 * 1024 * 1024) - (1L * 1024 * 1024) // ~2GB
    };

    /// <summary>
    /// Creates a custom configuration.
    /// </summary>
    public static MemoryBufferConfiguration Create(int segmentCount, long segmentSizeBytes) => new()
    {
        SegmentCount = segmentCount,
        SegmentSizeBytes = segmentSizeBytes
    };

    /// <summary>
    /// Validates the configuration for logical consistency.
    /// </summary>
    /// <exception cref="ArgumentException">If configuration is invalid</exception>
    public void Validate()
    {
        if (SegmentCount <= 0)
            throw new ArgumentException("SegmentCount must be greater than 0", nameof(SegmentCount));

        if (SegmentSizeBytes <= 0)
            throw new ArgumentException("SegmentSizeBytes must be greater than 0", nameof(SegmentSizeBytes));

        if (MaxCapacityBytes <= 0)
            throw new ArgumentException("MaxCapacityBytes must be greater than 0", nameof(MaxCapacityBytes));
    }
}
