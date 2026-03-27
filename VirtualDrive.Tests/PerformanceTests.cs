using System.Diagnostics;
using VirtualDrive.Core;
using Xunit;
using Xunit.Abstractions;

namespace VirtualDrive.Tests;

/// <summary>
/// Performance benchmarks for VirtualDrive.Core.
/// Measures throughput (GB/s) for various I/O operations.
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;
    private const string PerfVolume = "PerfVolume";

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Sequential_Write_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512); // 512MB

        const int fileSize = 50 * 1024 * 1024; // 50MB
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);

        var sw = Stopwatch.StartNew();
        api.WriteFile(PerfVolume, "\\large_file.bin", data);
        sw.Stop();

        double throughputGBps = (fileSize / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Sequential Write: {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms for {fileSize / (1024 * 1024)}MB)");
        
        Assert.True(sw.ElapsedMilliseconds > 0, "Timing too fast to measure");
    }

    [Fact]
    public void Sequential_Read_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512);

        const int fileSize = 50 * 1024 * 1024;
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);
        api.WriteFile(PerfVolume, "\\large_file.bin", data);

        var sw = Stopwatch.StartNew();
        var readData = api.ReadFile(PerfVolume, "\\large_file.bin");
        sw.Stop();

        double throughputGBps = (fileSize / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Sequential Read: {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms for {fileSize / (1024 * 1024)}MB)");

        Assert.Equal(data, readData);
    }

    [Fact]
    public void Streaming_Write_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512);

        const int chunkSize = 5 * 1024 * 1024; // 5MB chunks
        const int numChunks = 10;
        var chunk = new byte[chunkSize];
        new Random(42).NextBytes(chunk);

        var sw = Stopwatch.StartNew();
        api.WriteFile(PerfVolume, "\\streamed_file.bin", Array.Empty<byte>()); // Create empty file

        for (int i = 0; i < numChunks; i++)
        {
            api.WriteFileAt(PerfVolume, "\\streamed_file.bin", chunk, i * chunkSize);
        }
        sw.Stop();

        long totalBytes = (long)chunkSize * numChunks;
        double throughputGBps = (totalBytes / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Streaming Write (WriteFileAt): {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms for {totalBytes / (1024 * 1024)}MB)");
    }

    [Fact]
    public void Streaming_Read_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512);

        const int fileSize = 50 * 1024 * 1024;
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);
        api.WriteFile(PerfVolume, "\\large_file.bin", data);

        const int bufferSize = 1024 * 1024; // 1MB buffer
        var buffer = new byte[bufferSize];
        long totalRead = 0;

        var sw = Stopwatch.StartNew();
        for (long offset = 0; offset < fileSize; offset += bufferSize)
        {
            int bytesRead = api.ReadFileAt(PerfVolume, "\\large_file.bin", buffer, offset);
            totalRead += bytesRead;
            if (bytesRead < bufferSize) break;
        }
        sw.Stop();

        double throughputGBps = (totalRead / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Streaming Read (ReadFileAt): {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms for {totalRead / (1024 * 1024)}MB)");
    }

    [Fact]
    public void Concurrent_Read_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512);

        const int fileSize = 50 * 1024 * 1024;
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);
        api.WriteFile(PerfVolume, "\\large_file.bin", data);

        const int numThreads = 4;
        var threads = new Thread[numThreads];
        long totalBytesRead = 0;
        object lockObj = new object();

        var sw = Stopwatch.StartNew();

        for (int t = 0; t < numThreads; t++)
        {
            threads[t] = new Thread(() =>
            {
                var readData = api.ReadFile(PerfVolume, "\\large_file.bin");
                lock (lockObj)
                {
                    totalBytesRead += readData.Length;
                }
            });
            threads[t].Start();
        }

        for (int t = 0; t < numThreads; t++)
        {
            threads[t].Join();
        }

        sw.Stop();

        double throughputGBps = (totalBytesRead / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Concurrent Read ({numThreads} threads): {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms, {totalBytesRead / (1024 * 1024)}MB total)");
    }

    [Fact]
    public void Small_File_Operations_IOPS()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 512);

        const int numFiles = 1000;
        var smallData = new byte[1024]; // 1KB files
        new Random(42).NextBytes(smallData);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < numFiles; i++)
        {
            api.WriteFile(PerfVolume, $"\\file_{i:D5}.bin", smallData);
        }
        sw.Stop();

        double iops = numFiles / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Small File Write IOPS: {iops:F0} ops/sec ({sw.ElapsedMilliseconds}ms for {numFiles} files)");

        sw.Restart();
        for (int i = 0; i < numFiles; i++)
        {
            api.ReadFile(PerfVolume, $"\\file_{i:D5}.bin");
        }
        sw.Stop();

        iops = numFiles / (sw.ElapsedMilliseconds / 1000.0);
        _output.WriteLine($"Small File Read IOPS: {iops:F0} ops/sec ({sw.ElapsedMilliseconds}ms for {numFiles} files)");
    }

    [Fact]
    public void Different_Configurations_Throughput()
    {
        const int fileSize = 10 * 1024 * 1024; // 10MB for quick test
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);

        var configs = new[]
        {
            ("Small (10×256MB)", MemoryBufferConfiguration.CreateSmall()),
            ("Default (50×2GB)", MemoryBufferConfiguration.CreateDefault()),
            ("Large (100×2GB)", MemoryBufferConfiguration.CreateLarge()),
        };

        foreach (var (name, config) in configs)
        {
            var api = new VirtualDriveApi(config);
            api.CreateVolume(PerfVolume, 50); // 50MB volume

            var sw = Stopwatch.StartNew();
            api.WriteFile(PerfVolume, "\\test.bin", data);
            sw.Stop();

            double throughputGBps = (fileSize / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"{name} Write: {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms)");

            sw.Restart();
            api.ReadFile(PerfVolume, "\\test.bin");
            sw.Stop();

            throughputGBps = (fileSize / (1024.0 * 1024 * 1024)) / (sw.ElapsedMilliseconds / 1000.0);
            _output.WriteLine($"{name} Read: {throughputGBps:F2} GB/s ({sw.ElapsedMilliseconds}ms)");
        }
    }

    [Fact]
    public void Directory_Traversal_Performance()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 256);

        const int numFiles = 100;
        const string basePath = "\\Documents\\Projects\\Data";
        api.CreateDirectory(PerfVolume, basePath);

        // Create files
        var data = new byte[10 * 1024]; // 10KB each
        for (int i = 0; i < numFiles; i++)
        {
            api.WriteFile(PerfVolume, $"{basePath}\\file_{i:D3}.txt", data);
        }

        var sw = Stopwatch.StartNew();
        var files = api.ListFiles(PerfVolume, basePath).ToList();
        sw.Stop();

        _output.WriteLine($"Directory Listing: {sw.ElapsedMilliseconds}ms for {files.Count} files");
        Assert.Equal(numFiles, files.Count);
    }

    [Fact]
    public void Memory_Usage_Pattern()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 256);

        const int fileSize = 25 * 1024 * 1024; // 25MB
        var data = new byte[fileSize];
        new Random(42).NextBytes(data);

        // Write gradually
        _output.WriteLine("Memory usage during streaming writes:");
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 5; i++)
        {
            api.WriteFile(PerfVolume, $"\\file_{i}.bin", data);
            var info = api.GetVolumeInfo(PerfVolume);
            _output.WriteLine($"  File {i + 1}: Used {info.UsedBytes / (1024 * 1024)}MB / {info.CapacityBytes / (1024 * 1024)}MB");
        }

        sw.Stop();
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Volume_Capacity_Limits()
    {
        var api = new VirtualDriveApi();
        api.CreateVolume(PerfVolume, 10); // 10MB volume

        var data = new byte[5 * 1024 * 1024]; // 5MB
        new Random(42).NextBytes(data);

        // First write should succeed
        api.WriteFile(PerfVolume, "\\file1.bin", data);
        var info = api.GetVolumeInfo(PerfVolume);
        _output.WriteLine($"After first write: {info.UsedBytes / (1024 * 1024)}MB used");

        // Second write should succeed
        api.WriteFile(PerfVolume, "\\file2.bin", data);
        info = api.GetVolumeInfo(PerfVolume);
        _output.WriteLine($"After second write: {info.UsedBytes / (1024 * 1024)}MB used");

        // Third write should fail (disk full)
        var ex = Assert.Throws<InvalidOperationException>(() =>
            api.WriteFile(PerfVolume, "\\file3.bin", data)
        );
        _output.WriteLine($"Third write failed as expected: {ex.Message}");
    }
}
