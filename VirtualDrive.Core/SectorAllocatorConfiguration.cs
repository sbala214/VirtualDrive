using System;
using System.Text.Json;
using System.IO;

namespace VirtualDrive.Core;

/// <summary>
/// Configuration for SectorAllocator behavior.
/// Allows customization of sector size and maximum capacity.
/// </summary>
public class SectorAllocatorConfiguration
{
    /// <summary>
    /// Size of each sector in bytes (default: 8192 = 8KB).
    /// Must be a power of 2 for efficient bit shifting in address calculations.
    /// </summary>
    public int SectorSizeBytes { get; set; } = 8192;

    /// <summary>
    /// Maximum total capacity in bytes (default: 100GB).
    /// Determines the bitmap size and total addressable space.
    /// </summary>
    public long MaxCapacityBytes { get; set; } = 100L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets the total number of sectors based on capacity and sector size.
    /// </summary>
    public long TotalSectors => MaxCapacityBytes / SectorSizeBytes;

    /// <summary>
    /// Creates default configuration (8KB sectors, 100GB capacity).
    /// </summary>
    public static SectorAllocatorConfiguration CreateDefault() => new();

    /// <summary>
    /// Creates a small configuration for testing (4KB sectors, 10GB capacity).
    /// </summary>
    public static SectorAllocatorConfiguration CreateSmall() => new()
    {
        SectorSizeBytes = 4096,
        MaxCapacityBytes = 10L * 1024 * 1024 * 1024
    };

    /// <summary>
    /// Creates a large configuration (16KB sectors, 500GB capacity).
    /// </summary>
    public static SectorAllocatorConfiguration CreateLarge() => new()
    {
        SectorSizeBytes = 16384,
        MaxCapacityBytes = 500L * 1024 * 1024 * 1024
    };

    /// <summary>
    /// Creates a custom configuration.
    /// </summary>
    /// <param name="sectorSizeBytes">Sector size in bytes (should be power of 2)</param>
    /// <param name="maxCapacityBytes">Maximum capacity in bytes</param>
    public static SectorAllocatorConfiguration Create(int sectorSizeBytes, long maxCapacityBytes) => new()
    {
        SectorSizeBytes = sectorSizeBytes,
        MaxCapacityBytes = maxCapacityBytes
    };

    /// <summary>
    /// Loads configuration from a JSON file.
    /// File should contain JSON object with SectorSizeBytes and MaxCapacityBytes properties.
    /// </summary>
    /// <param name="filePath">Path to JSON configuration file</param>
    /// <returns>Loaded configuration, or default if file doesn't exist</returns>
    /// <exception cref="InvalidOperationException">If JSON is malformed</exception>
    public static SectorAllocatorConfiguration LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return CreateDefault();

        try
        {
            string json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<SectorAllocatorConfiguration>(json, options);
            return config ?? CreateDefault();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse configuration file '{filePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads configuration from a JSON string.
    /// </summary>
    /// <param name="jsonContent">JSON string containing configuration</param>
    /// <returns>Loaded configuration, or default if JSON is empty</returns>
    public static SectorAllocatorConfiguration LoadFromJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return CreateDefault();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<SectorAllocatorConfiguration>(jsonContent, options);
            return config ?? CreateDefault();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse configuration JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves configuration to a JSON file.
    /// </summary>
    /// <param name="filePath">Path where to save the configuration file</param>
    public void SaveToFile(string filePath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Validates the configuration for logical consistency.
    /// </summary>
    /// <exception cref="ArgumentException">If configuration is invalid</exception>
    public void Validate()
    {
        if (SectorSizeBytes <= 0)
            throw new ArgumentException("SectorSizeBytes must be greater than 0", nameof(SectorSizeBytes));

        if (MaxCapacityBytes <= 0)
            throw new ArgumentException("MaxCapacityBytes must be greater than 0", nameof(MaxCapacityBytes));

        if (MaxCapacityBytes < SectorSizeBytes)
            throw new ArgumentException("MaxCapacityBytes must be >= SectorSizeBytes", nameof(MaxCapacityBytes));

        // Check if sector size is power of 2
        if ((SectorSizeBytes & (SectorSizeBytes - 1)) != 0)
            throw new ArgumentException("SectorSizeBytes should be a power of 2 for optimal performance", nameof(SectorSizeBytes));
    }
}
