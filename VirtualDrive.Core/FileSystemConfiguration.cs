using System;
using System.Text.Json;
using System.IO;

namespace VirtualDrive.Core;

/// <summary>
/// Unified configuration for VirtualDrive file system.
/// Combines sector allocator and memory buffer settings in a single configuration.
/// Can be loaded from JSON file for easy customization without code changes.
/// </summary>
public class FileSystemConfiguration
{
    /// <summary>
    /// Sector allocator configuration (sector size, max capacity).
    /// </summary>
    public SectorAllocatorConfiguration SectorAllocator { get; set; } = SectorAllocatorConfiguration.CreateDefault();

    /// <summary>
    /// Memory buffer configuration (segment count, size).
    /// </summary>
    public MemoryBufferConfiguration MemoryBuffer { get; set; } = MemoryBufferConfiguration.CreateDefault();

    /// <summary>
    /// Creates a default configuration with balanced settings.
    /// - 8KB sectors, 100GB max capacity
    /// - 50 × 2GB segments (~98GB capacity)
    /// </summary>
    public static FileSystemConfiguration CreateDefault() => new();

    /// <summary>
    /// Creates a small configuration suitable for testing.
    /// - 4KB sectors, 10GB max capacity
    /// - 10 × 256MB segments (2.5GB capacity)
    /// </summary>
    public static FileSystemConfiguration CreateSmall() => new()
    {
        SectorAllocator = SectorAllocatorConfiguration.CreateSmall(),
        MemoryBuffer = MemoryBufferConfiguration.CreateSmall()
    };

    /// <summary>
    /// Creates a large configuration for high-throughput scenarios.
    /// - 16KB sectors, 500GB max capacity
    /// - 100 × 2GB segments (~200GB capacity)
    /// </summary>
    public static FileSystemConfiguration CreateLarge() => new()
    {
        SectorAllocator = SectorAllocatorConfiguration.CreateLarge(),
        MemoryBuffer = MemoryBufferConfiguration.CreateLarge()
    };

    /// <summary>
    /// Creates a custom configuration.
    /// </summary>
    public static FileSystemConfiguration Create(
        SectorAllocatorConfiguration sectorConfig,
        MemoryBufferConfiguration memoryConfig) => new()
    {
        SectorAllocator = sectorConfig ?? SectorAllocatorConfiguration.CreateDefault(),
        MemoryBuffer = memoryConfig ?? MemoryBufferConfiguration.CreateDefault()
    };

    /// <summary>
    /// Loads configuration from a JSON file.
    /// File should contain nested structure with "sectorAllocator" and "memoryBuffer" objects.
    /// 
    /// Example JSON:
    /// {
    ///   "sectorAllocator": {
    ///     "sectorSizeBytes": 8192,
    ///     "maxCapacityBytes": 107374182400
    ///   },
    ///   "memoryBuffer": {
    ///     "segmentCount": 50,
    ///     "segmentSizeBytes": 2145386496
    ///   }
    /// }
    /// </summary>
    /// <param name="filePath">Path to JSON configuration file</param>
    /// <returns>Loaded configuration, or default if file doesn't exist</returns>
    /// <exception cref="InvalidOperationException">If JSON is malformed</exception>
    public static FileSystemConfiguration LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return CreateDefault();

        try
        {
            string json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<FileSystemConfiguration>(json, options);
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
    public static FileSystemConfiguration LoadFromJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return CreateDefault();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<FileSystemConfiguration>(jsonContent, options);
            return config ?? CreateDefault();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse configuration JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves configuration to a JSON file with readable formatting.
    /// </summary>
    /// <param name="filePath">Path where to save the configuration file</param>
    public void SaveToFile(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Exports configuration to a JSON string.
    /// </summary>
    /// <returns>Pretty-printed JSON representation of configuration</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Validates the entire configuration for consistency.
    /// Ensures both sector allocator and memory buffer settings are valid and compatible.
    /// </summary>
    /// <exception cref="ArgumentException">If configuration is invalid</exception>
    public void Validate()
    {
        if (SectorAllocator == null)
            throw new ArgumentException("SectorAllocator configuration cannot be null", nameof(SectorAllocator));

        if (MemoryBuffer == null)
            throw new ArgumentException("MemoryBuffer configuration cannot be null", nameof(MemoryBuffer));

        SectorAllocator.Validate();
        MemoryBuffer.Validate();

        // Ensure memory buffer capacity >= sector allocator max capacity
        if (MemoryBuffer.MaxCapacityBytes < SectorAllocator.MaxCapacityBytes)
            throw new ArgumentException(
                $"Memory buffer capacity ({MemoryBuffer.MaxCapacityBytes} bytes) must be >= " +
                $"sector allocator capacity ({SectorAllocator.MaxCapacityBytes} bytes)",
                nameof(MemoryBuffer));
    }
}
