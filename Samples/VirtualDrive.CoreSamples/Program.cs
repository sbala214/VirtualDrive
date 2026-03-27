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
 * var fileSystem = new VirtualFileSystem(config);
 * fileSystem.CreateDirectory("/mydir");
 * fileSystem.CreateFile("/mydir/file.txt", content);
 */

using System;
using System.Text;
using System.Threading.Tasks;
using VirtualDrive.Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║   VirtualDrive.Core - Sample App           ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            // Create file system configuration
            var config = new FileSystemConfiguration
            {
                SectorSize = 4096,
                MaxSectors = 1000,
                AutoGrowth = true
            };

            Console.WriteLine("📋 File System Configuration:");
            Console.WriteLine($"   • Sector Size: {config.SectorSize} bytes");
            Console.WriteLine($"   • Max Sectors: {config.MaxSectors}");
            Console.WriteLine($"   • Auto Growth: {config.AutoGrowth}\n");

            // Initialize virtual file system
            Console.WriteLine("🚀 Initializing Virtual File System...");
            var fileSystem = new VirtualFileSystem(config);
            Console.WriteLine("✓ Virtual File System ready\n");

            // Create directory structure
            Console.WriteLine("📁 Creating Directory Structure:");
            fileSystem.CreateDirectory("/documents");
            fileSystem.CreateDirectory("/documents/projects");
            fileSystem.CreateDirectory("/data");
            Console.WriteLine("   ✓ /documents");
            Console.WriteLine("   ✓ /documents/projects");
            Console.WriteLine("   ✓ /data\n");

            // Create and write to virtual files
            Console.WriteLine("✍️  Creating Virtual Files:");
            
            var content1 = Encoding.UTF8.GetBytes("Hello from VirtualDrive!");
            fileSystem.CreateFile("/documents/readme.txt", content1);
            Console.WriteLine("   ✓ /documents/readme.txt");

            var content2 = Encoding.UTF8.GetBytes("Sample project configuration");
            fileSystem.CreateFile("/documents/projects/config.json", content2);
            Console.WriteLine("   ✓ /documents/projects/config.json");

            var content3 = Encoding.UTF8.GetBytes("Sample data content");
            fileSystem.CreateFile("/data/sample.dat", content3);
            Console.WriteLine("   ✓ /data/sample.dat\n");

            // List and retrieve file information
            Console.WriteLine("📂 Listing Root Directory:");
            var rootContents = fileSystem.GetFiles("/");
            foreach (var entry in rootContents)
            {
                var type = entry.IsDirectory ? "DIR" : "FILE";
                Console.WriteLine($"   • {entry.Name} ({type})");
            }

            Console.WriteLine("\n📂 Listing /documents Directory:");
            var docContents = fileSystem.GetFiles("/documents");
            foreach (var entry in docContents)
            {
                var type = entry.IsDirectory ? "DIR" : "FILE";
                var size = entry.FileMetadata?.SizeInBytes ?? 0;
                Console.WriteLine($"   • {entry.Name} ({type}) - {size} bytes");
            }

            // Read file contents
            Console.WriteLine("\n📖 Reading File Contents:");
            var readContent = await fileSystem.ReadFileAsync("/documents/readme.txt");
            Console.WriteLine($"   /documents/readme.txt: {Encoding.UTF8.GetString(readContent)}");

            // Display file system statistics
            Console.WriteLine("\n📊 File System Statistics:");
            Console.WriteLine($"   • Total Directories: ~3");
            Console.WriteLine($"   • Total Files: ~3");
            Console.WriteLine($"   • Used Capacity: {content1.Length + content2.Length + content3.Length} bytes\n");

            Console.WriteLine("✅ Sample completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"\nDetails: {ex}");
        }
    }
}
