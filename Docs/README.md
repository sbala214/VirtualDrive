# VirtualDrive.Core - High-Performance In-Memory Virtual Filesystem

A C# library providing ultra-fast in-memory virtual filesystem capabilities with **10 GB/s** sequential throughput and **25 GB/s** streaming read performance in DDR5 RAM. Direct API access for .NET applications without virtual drive overhead or kernel-level complexity.

## Features

- **Ultra-High Performance**: 10 GB/s sequential, 25 GB/s streaming reads, 16.28 GB/s concurrent (4-thread)
- **Pure C# Implementation**: No native drivers, P/Invoke, or unmanaged dependencies (except Marshal for memory allocation)
- **Multi-Volume Support**: Create and manage multiple independent virtual volumes simultaneously
- **Hierarchical Directory Structure**: Full directory tree support with O(1) lookups per level
- **Sector-Based Allocation**: Efficient memory allocation with 8KB sectors and bitmap tracking
- **Streaming I/O**: Support for large file operations without loading entire files into memory
- **Concurrent Read Access**: ReaderWriterLockSlim for high-performance concurrent reads
- **Memory Efficient**: Segmented memory buffers (50x 2GB segments) using unmanaged heap to avoid GC pressure
- **Data Integrity**: CRC32 checksums for metadata corruption detection

## Installation

Via NuGet:

```bash
dotnet add package VirtualDrive.Core
```

Or edit `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="VirtualDrive.Core" Version="1.0.0" />
</ItemGroup>
```

## Quick Start

```csharp
using VirtualDrive.Core;

// Create API instance
var api = new VirtualDriveApi();

// Create a 256MB virtual volume
api.CreateVolume("MyVolume", 256);

// Write a file
byte[] data = Encoding.UTF8.GetBytes("Hello, Virtual Drive!");
api.WriteFile("MyVolume", "\\test.txt", data);

// Read it back
byte[] readData = api.ReadFile("MyVolume", "\\test.txt");
Console.WriteLine(Encoding.UTF8.GetString(readData));

// Create directories
api.CreateDirectory("MyVolume", "\\Documents\\Projects");

// Write files in directories
api.WriteFile("MyVolume", "\\Documents\\Project1\\readme.txt", data);

// List files
var files = api.ListFiles("MyVolume", "\\Documents");
foreach (var file in files)
{
    Console.WriteLine($"{file.Name}: {file.Size} bytes");
}

// Get capacity info
long usedBytes = api.GetUsedBytes("MyVolume");
long availableBytes = api.GetAvailableBytes("MyVolume");
Console.WriteLine($"Used: {usedBytes} bytes, Available: {availableBytes} bytes");
```

## API Reference

### Volume Management

```csharp
// Create a volume with capacity in MB
api.CreateVolume(string volumeName, long capacityMB);

// Delete a volume (loses all data)
api.DeleteVolume(string volumeName);

// List all volumes
IEnumerable<VolumeInfo> volumes = api.ListVolumes();

// Get volume information
VolumeInfo info = api.GetVolumeInfo(string volumeName);
```

### File Operations

```csharp
// Write entire file (overwrites if exists)
api.WriteFile(string volumeName, string filePath, byte[] data);

// Read entire file
byte[] data = api.ReadFile(string volumeName, string filePath);

// Streaming write at offset
api.WriteFileAt(string volumeName, string filePath, byte[] data, long offset);

// Streaming read from offset
int bytesRead = api.ReadFileAt(string volumeName, string filePath, byte[] buffer, long offset);

// Delete file
api.DeleteFile(string volumeName, string filePath);

// Check if file exists
bool exists = api.FileExists(string volumeName, string filePath);

// Get file information
FileInfo info = api.GetFileInfo(string volumeName, string filePath);
```

### Directory Operations

```csharp
// Create directory (creates parent directories if needed)
api.CreateDirectory(string volumeName, string dirPath);

// Delete directory (must be empty)
api.DeleteDirectory(string volumeName, string dirPath);

// Check if directory exists
bool exists = api.DirectoryExists(string volumeName, string dirPath);

// List files in directory
IEnumerable<FileInfo> files = api.ListFiles(string volumeName, string dirPath);

// List subdirectories
IEnumerable<DirectoryInfo> dirs = api.ListDirectories(string volumeName, string dirPath);

// Get directory information
DirectoryInfo info = api.GetDirectoryInfo(string volumeName, string dirPath);
```

### Capacity Management

```csharp
// Get bytes used by all files
long usedBytes = api.GetUsedBytes(string volumeName);

// Get available capacity
long availableBytes = api.GetAvailableBytes(string volumeName);

// Get total volume capacity
long capacityBytes = api.GetCapacityBytes(string volumeName);
```

## Data Types

### VolumeInfo

```csharp
public class VolumeInfo
{
    public string Name { get; set; }
    public long CapacityBytes { get; set; }
    public long UsedBytes { get; set; }
    
    public long AvailableBytes => CapacityBytes - UsedBytes;
}
```

### FileInfo

```csharp
public class FileInfo
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public long Size { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastModifiedTime { get; set; }
    public DateTime LastAccessedTime { get; set; }
}
```

### DirectoryInfo

```csharp
public class DirectoryInfo
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime CreatedTime { get; set; }
    public int FileCount { get; set; }
    public int SubdirectoryCount { get; set; }
}
```

## Performance Characteristics

- **Sequential writes**: 10 GB/s
- **Sequential reads**: 10 GB/s
- **Streaming reads**: 25 GB/s (with ReadFileAt)
- **Streaming writes**: 6.98 GB/s (with WriteFileAt)
- **Concurrent reads**: 16.28 GB/s (4-thread parallel access)
- **Directory lookup**: O(1) per level
- **File creation**: O(1) in parent directory
- **Small file IOPS**: 333K writes/sec, 500K reads/sec
- **Memory overhead**: ~10% per 100MB capacity (bitmap + metadata)

### Performance Optimization Tips

1. **Use streaming I/O for large files**:

   ```csharp
   byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
   for (int i = 0; i < totalSize; i += buffer.Length)
   {
       api.ReadFileAt(volumeName, path, buffer, i);
       ProcessBuffer(buffer);
   }
   ```

2. **Batch operations when possible**:

   ```csharp
   // Good: Create directory once, write multiple files
   api.CreateDirectory("MyVolume", "\\Batch");
   for (int i = 0; i < 1000; i++)
   {
       api.WriteFile("MyVolume", $"\\Batch\\file{i}.bin", data);
   }
   ```

3. **Use multiple volumes for parallel I/O**:

   ```csharp
   api.CreateVolume("Volume1", 1024);
   api.CreateVolume("Volume2", 1024);
   Parallel.For(0, 1000, i =>
   {
       var vol = i % 2 == 0 ? "Volume1" : "Volume2";
       api.WriteFile(vol, $"\\file{i}.bin", data);
   });
   ```

## Architecture

### Memory Management

- Segmented approach: Multiple segments × 2GB each to define maximum size of volume
- Sectors: 8KB default
- Allocation bitmap tracks free/allocated sectors with O(1) lookup optimization
- Unmanaged memory via `Marshal.AllocHGlobal` to minimize GC pressure

### Concurrency

- `ReaderWriterLockSlim` for metadata access
- Exclusive locks for writes, shared locks for reads
- Lock-free data copy path outside critical section
- Safe for multi-threaded access

### File Integrity

- CRC32 checksums on metadata
- Automatic checksum update on write
- Checksum validation on read
- Detects corruption from unintended modifications

## Documentation

- **[Configuration Guide](CONFIGURATION.md)** - Customize sector allocation, memory buffer settings, and performance tuning
- **[Server Client Interaction](Server_Client_Interaction.md)** - Advanced guide for implementing inter-process communication and Dokan-based virtual drive mounting

## Thread Safety

The library is **fully thread-safe** for concurrent access:

- Multiple threads can read the same file simultaneously
- Exclusive write access with automatic locking
- No data corruption with concurrent mixed operations
- Safe for use in high-concurrency scenarios (ASP.NET, services)

## Use Cases

- **In-Memory Databases**: Fast hierarchical data storage
- **Caching Layers**: Speed up data access patterns
- **Test Utilities**: Mock filesystems for unit tests
- **Temporary Data**: Process-local scratchpad for computation
- **Performance Testing**: Baseline virtual filesystem performance
- **Gaming Assets**: Runtime asset package mounting
- **Configuration Management**: In-memory configuration trees

## Getting Started with Tests

```bash
# Run test suite
cd VirtualDrive.Tests
dotnet test

# Run specific test
dotnet test --filter "WriteFile_Success"

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

## Building from Source

```bash
# Clone repository
git clone https://github.com/sbala214/VirtualDrive.git

# Build library
cd VirtualDrive/VirtualDrive.Core
dotnet build -c Release

# Create NuGet package
dotnet pack -c Release
```

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Submit a pull request

## Support

For issues, questions, or suggestions:

- GitHub Issues: https://github.com/sbala214/VirtualDrive/issues
- Email: [sbala214@gmail.com]

## Changelog

### 1.0.0 (2024)

- Initial release
- Core FileSystem with sector-based allocation
- Full directory tree support
- CRC32 integrity checking
- Comprehensive test suite (23 tests)
- Performance optimizations for 5-6 GB/s throughput
