/*
 * VirtualDrive.Core - Remote Client Sample (Named Pipes Transport)
 * 
 * This example demonstrates how to:
 * 1. Connect to a running VirtualDrive server via Named Pipes
 * 2. Create volumes
 * 3. Write and read files
 * 4. Work with directories
 * 
 * NOTE: This sample uses Named Pipes transport for local/intranet communication.
 *       To use HTTP transport instead, install VirtualDrive.Client.Http and
 *       replace 'NamedPipesClient' with 'HttpClient' below.
 */

using System;
using System.Text;
using System.Threading.Tasks;
using VirtualDrive.Client.Abstractions;
using VirtualDrive.Client.NamedPipes;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║   VirtualDrive.Core - Client Sample        ║");
        Console.WriteLine("║        (Named Pipes Transport)             ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        Console.Write("Pipe name [VirtualDrive.Server]: ");
        var pipeName = Console.ReadLine()?.Trim() ?? "VirtualDrive.Server";

        ITransportClient client = new NamedPipesClient(pipeName);
        string serverLocation = $"named pipe '{pipeName}'";

        try
        {
            await using (client)
            {
                Console.WriteLine($"\n[Connecting] to {serverLocation}...");
                await client.ConnectAsync();
                Console.WriteLine("[Connected] ✓\n");

                // Run interactive menu
                await RunInteractiveMenu(client);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[Error] {ex.GetType().Name}: {ex.Message}");
            return;
        }
    }

    static async Task RunInteractiveMenu(ITransportClient client)
    {
        while (true)
        {
            Console.WriteLine("\n╔════════════════════════════════╗");
            Console.WriteLine("║        Main Menu               ║");
            Console.WriteLine("╠════════════════════════════════╣");
            Console.WriteLine("║ [1] Create Volume              ║");
            Console.WriteLine("║ [2] List Volumes               ║");
            Console.WriteLine("║ [3] Write File                 ║");
            Console.WriteLine("║ [4] Read File                  ║");
            Console.WriteLine("║ [5] Create Directory           ║");
            Console.WriteLine("║ [6] List Files                 ║");
            Console.WriteLine("║ [7] Get Capacity Info          ║");
            Console.WriteLine("║ [8] Exit                       ║");
            Console.WriteLine("╚════════════════════════════════╝");
            Console.Write("\nChoice: ");

            var choice = Console.ReadLine()?.Trim() ?? "8";

            try
            {
                switch (choice)
                {
                    case "1":
                        await CreateVolume(client);
                        break;
                    case "2":
                        await ListVolumes(client);
                        break;
                    case "3":
                        await WriteFile(client);
                        break;
                    case "4":
                        await ReadFile(client);
                        break;
                    case "5":
                        await CreateDirectory(client);
                        break;
                    case "6":
                        await ListFiles(client);
                        break;
                    case "7":
                        await GetCapacityInfo(client);
                        break;
                    case "8":
                        Console.WriteLine("\n[Exit] Goodbye!");
                        return;
                    default:
                        Console.WriteLine("[Error] Invalid choice");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Error] {ex.Message}");
            }
        }
    }

    static async Task CreateVolume(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var name = Console.ReadLine()?.Trim() ?? "MyVolume";

        Console.Write("Capacity (MB): ");
        if (!long.TryParse(Console.ReadLine()?.Trim(), out long capacity))
            capacity = 256;

        await client.CreateVolumeAsync(name, capacity);
        Console.WriteLine($"[Success] Created volume '{name}' with {capacity}MB capacity");
    }

    static async Task ListVolumes(ITransportClient client)
    {
        var volumes = await client.ListVolumesAsync();

        Console.WriteLine($"\n[Volumes] Found {volumes.Count} volume(s):");
        foreach (var vol in volumes)
        {
            var usedMB = vol.UsedBytes / (1024 * 1024);
            var totalMB = vol.CapacityBytes / (1024 * 1024);
            var percent = vol.CapacityBytes > 0 ? (vol.UsedBytes * 100) / vol.CapacityBytes : 0;

            Console.WriteLine($"  • {vol.Name}: {usedMB}/{totalMB} MB ({percent}%)");
        }
    }

    static async Task WriteFile(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var volume = Console.ReadLine()?.Trim() ?? "MyVolume";

        Console.Write("File path: ");
        var path = Console.ReadLine()?.Trim() ?? "\\test.txt";

        Console.Write("File content: ");
        var content = Console.ReadLine() ?? "Hello, Virtual Drive!";

        var data = Encoding.UTF8.GetBytes(content);
        await client.WriteFileAsync(volume, path, data);

        Console.WriteLine($"[Success] Wrote {data.Length} bytes to '{path}'");
    }

    static async Task ReadFile(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var volume = Console.ReadLine()?.Trim() ?? "MyVolume";

        Console.Write("File path: ");
        var path = Console.ReadLine()?.Trim() ?? "\\test.txt";

        var data = await client.ReadFileAsync(volume, path);
        var content = Encoding.UTF8.GetString(data);

        Console.WriteLine($"[Success] Read {data.Length} bytes: {content}");
    }

    static async Task CreateDirectory(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var volume = Console.ReadLine()?.Trim() ?? "MyVolume";

        Console.Write("Directory path: ");
        var path = Console.ReadLine()?.Trim() ?? "\\Documents";

        await client.CreateDirectoryAsync(volume, path);
        Console.WriteLine($"[Success] Created directory '{path}'");
    }

    static async Task ListFiles(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var volume = Console.ReadLine()?.Trim() ?? "MyVolume";

        Console.Write("Directory path: ");
        var path = Console.ReadLine()?.Trim() ?? "\\";

        var files = await client.ListFilesAsync(volume, path);

        Console.WriteLine($"\n[Files] Found {files.Count} file(s) in '{path}':");
        foreach (var file in files)
        {
            var sizeMB = file.Size / (1024 * 1024);
            Console.WriteLine($"  • {file.Name}: {file.Size} bytes ({sizeMB}MB)");
        }
    }

    static async Task GetCapacityInfo(ITransportClient client)
    {
        Console.Write("\nVolume name: ");
        var volume = Console.ReadLine()?.Trim() ?? "MyVolume";

        var capacity = await client.GetCapacityAsync(volume);
        if (capacity != null)
        {
            var totalMB = capacity.TotalBytes / (1024 * 1024);
            var usedMB = capacity.UsedBytes / (1024 * 1024);
            Console.WriteLine($"\n[Capacity] {volume}:");
            Console.WriteLine($"  Total: {totalMB} MB");
            Console.WriteLine($"  Used:  {usedMB} MB ({capacity.PercentUsed:F1}%)");
            Console.WriteLine($"  Free:  {(capacity.TotalBytes - capacity.UsedBytes) / (1024 * 1024)} MB");
        }
    }
}
