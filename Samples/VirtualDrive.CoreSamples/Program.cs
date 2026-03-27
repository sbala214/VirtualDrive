/*
/*
 * VirtualDrive.Core - Sample Application
 * 
 * This example demonstrates how to:
 * 1. Initialize the VirtualDrive core file system
 * 2. Create and manage virtual files and directories
 * 3. Perform read/write operations on virtual files
 * 4. Configure memory allocation and sectors
 * 5. Query file system information
 * 
 * NuGet Package Required:
 * dotnet add package VirtualDrive.Core
 * 
 * Basic Usage:
 * var bufferConfig = MemoryBufferConfiguration.CreateSmall();
 * var fileSystem = new FileSystem(bufferConfig);
 * fileSystem.CreateDirectory("\\mydir", "mydir");
 * fileSystem.CreateFile("\\mydir\\file.txt", "file.txt");
 * fileSystem.WriteFile("\\mydir\\file.txt", content);
 */

using System;
using System.Text;
using System.Linq;
using VirtualDrive.Core;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║   VirtualDrive.Core - Sample App           ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            // Create file system configuration
            var bufferConfig = MemoryBufferConfiguration.CreateSmall();
            var sectorConfig = SectorAllocatorConfiguration.CreateSmall();

            Console.WriteLine("📋 File System Configuration:");
            Console.WriteLine($"   • Sector Size: {sectorConfig.SectorSizeBytes} bytes");
            Console.WriteLine($"   • Max Capacity: {sectorConfig.MaxCapacityBytes / (1024 * 1024)} MB");
            Console.WriteLine($"   • Segment Count: {bufferConfig.SegmentCount}");
            Console.WriteLine($"   • Segment Size: {bufferConfig.SegmentSizeBytes / (1024 * 1024)} MB\n");

            // Initialize virtual file system
            Console.WriteLine("🚀 Initializing Virtual File System...");
            var fileSystem = new FileSystem(bufferConfig);
            Console.WriteLine("✓ Virtual File System ready\n");

            // Create directory structure
            Console.WriteLine("📁 Creating Directory Structure:");
            fileSystem.CreateDirectory("\\documents", "documents");
            fileSystem.CreateDirectory("\\documents\\projects", "projects");
            fileSystem.CreateDirectory("\\data", "data");
            Console.WriteLine("   ✓ \\documents");
            Console.WriteLine("   ✓ \\documents\\projects");
            Console.WriteLine("   ✓ \\data\n");

            // Create and write to virtual files
            Console.WriteLine("✍️  Creating Virtual Files:");
            
            var content1 = Encoding.UTF8.GetBytes("Hello from VirtualDrive!");
            fileSystem.CreateFile("\\documents\\readme.txt", "readme.txt");
            fileSystem.WriteFile("\\documents\\readme.txt", content1);
            Console.WriteLine("   ✓ \\documents\\readme.txt");

            var content2 = Encoding.UTF8.GetBytes("Sample project configuration");
            fileSystem.CreateFile("\\documents\\projects\\config.json", "config.json");
            fileSystem.WriteFile("\\documents\\projects\\config.json", content2);
            Console.WriteLine("   ✓ \\documents\\projects\\config.json");

            var content3 = Encoding.UTF8.GetBytes("Sample data content");
            fileSystem.CreateFile("\\data\\sample.dat", "sample.dat");
            fileSystem.WriteFile("\\data\\sample.dat", content3);
            Console.WriteLine("   ✓ \\data\\sample.dat\n");

            // List and retrieve file information
            Console.WriteLine("📂 All File System Entries:");
            var entries = fileSystem.GetAllEntries().ToList();
            foreach (var entry in entries.OrderBy(e => e.FullPath))
            {
                var type = entry.IsDirectory ? "DIR" : "FILE";
                var size = entry.IsDirectory ? "" : $" - {entry.Size} bytes";
                Console.WriteLine($"   • {entry.FullPath} ({type}){size}");
            }

            // Read file contents
            Console.WriteLine("\n📖 Reading File Contents:");
            var readContent = fileSystem.ReadFile("\\documents\\readme.txt");
            Console.WriteLine($"   \\documents\\readme.txt: {Encoding.UTF8.GetString(readContent)}");

            // Display file system statistics
            var totalUsed = fileSystem.GetTotalUsedBytes();
            Console.WriteLine("\n📊 File System Statistics:");
            Console.WriteLine($"   • Total Entries: {entries.Count}");
            Console.WriteLine($"   • Total Used: {totalUsed} bytes");
            Console.WriteLine($"   • Buffer Capacity: {bufferConfig.MaxCapacityBytes / (1024 * 1024)} MB\n");

            Console.WriteLine("✅ Sample completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"\nDetails: {ex}");
        }
    }
}
