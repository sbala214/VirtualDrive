# VirtualDrive Configuration Guide

## Configuration File Location

Place `virtualdrive.config.json` in the VirtualDrive.Core project root directory or specify a custom path when loading.

## Configuration Options

### sectorAllocator

Controls sector allocation behavior:

- **sectorSizeBytes** (default: 8192)
  - Size of each sector in bytes
  - Must be power of 2 and >= 512
  - Larger sizes = fewer allocations overhead but more internal fragmentation
  - Common values: 512, 1024, 2048, 4096, 8192

- **maxCapacityBytes** (default: 107374182400 = 100 GB)
  - Maximum total capacity of the virtual filesystem
  - Affects initialization time and memory overhead
  - Calculated as: sector size × max sectors

### memoryBuffer

Controls segmented memory allocation:

- **segmentCount** (default: 50)
  - Number of memory segments to allocate
  - Each segment is allocated independently
  - Helps with memory fragmentation

- **segmentSizeBytes** (default: 2145386496 ≈ 2GB)
  - Size of each memory segment
  - Total RAM available ≈ segmentCount × segmentSizeBytes
  - Must fit within available system memory

## Example Configurations

### Small Development Setup

```json
{
  "sectorAllocator": {
    "sectorSizeBytes": 4096,
    "maxCapacityBytes": 1073741824
  },
  "memoryBuffer": {
    "segmentCount": 10,
    "segmentSizeBytes": 104857600
  }
}
```

- Total RAM: ~1 GB
- Virtual capacity: 1 GB
- Sector size: 4 KB

### Large Production Setup

```json
{
  "sectorAllocator": {
    "sectorSizeBytes": 8192,
    "maxCapacityBytes": 1099511627776
  },
  "memoryBuffer": {
    "segmentCount": 100,
    "segmentSizeBytes": 10737418240
  }
}
```

- Total RAM: ~100 GB
- Virtual capacity: 1 TB
- Sector size: 8 KB

## Usage in Code

```csharp
// Load configuration from file
var configuration = VirtualDriveConfiguration.LoadFromFile("path/to/virtualdrive.config.json");

// Or load from stream
var configuration = VirtualDriveConfiguration.LoadFromStream(jsonStream);

// Or use defaults
var configuration = VirtualDriveConfiguration.Default;

// Create FileSystem with configuration
var fileSystem = new FileSystem(configuration);
```

## Performance Tuning Tips

1. **Sector Size**: Larger sectors = fewer allocations but more wasted space for small files
2. **Segment Size**: Smaller segments = better memory fragmentation distribution but more overhead
3. **Max Capacity**: Set based on actual expected virtual drive size, not system RAM
4. **Segment Count**: Usually 50-100 is optimal balance

## Validation

Configuration is validated on load:

- Sector size must be power of 2 and >= 512
- Segment count must be > 0
- Segment size must be > 0
- maxCapacityBytes must be > segment size
- All values are logged during initialization
