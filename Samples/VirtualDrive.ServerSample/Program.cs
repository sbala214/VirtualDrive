/*
 * VirtualDrive.Server - Sample Application
 * 
 * This example demonstrates how to:
 * 1. Initialize a VirtualDrive server using extension methods
 * 2. Choose between different transport protocols
 * 3. Configure network options for HTTP/gRPC servers
 * 4. Run the server with proper lifecycle management
 * 
 * NuGet Package Required:
 * dotnet add package VirtualDrive.Server
 * 
 * Basic Usage:
 * var server = UseVirtualDriveLocal();  // Named Pipes (local only)
 * var server = UseVirtualDriveNetwork(); // HTTP/gRPC (network accessible)
 * var server = UseVirtualDriveHighThroughput(); // Optimized for performance
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualDrive.Core;
using VirtualDrive.Server.Extensions;
using VirtualDrive.Server.Transport;
using static VirtualDrive.Server.Extensions.VirtualDriveServerExtensions;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║   VirtualDrive.Server - Sample App         ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        // Display available transport options
        Console.WriteLine("Available Transport Protocols:\n");
        Console.WriteLine("[1] Named Pipes");
        Console.WriteLine("    → Local/Intranet only");
        Console.WriteLine("    → Low latency (same machine)");
        Console.WriteLine("    → Best for development & testing\n");

        Console.WriteLine("[2] HTTP/gRPC");
        Console.WriteLine("    → Network accessible");
        Console.WriteLine("    → Supports internet access");
        Console.WriteLine("    → Configurable host/port/HTTPS\n");

        Console.WriteLine("[3] High-Throughput");
        Console.WriteLine("    → Optimized Named Pipes");
        Console.WriteLine("    → Maximum performance");
        Console.WriteLine("    → For intensive workloads\n");

        Console.WriteLine("[4] Custom Network Config");
        Console.WriteLine("    → Advanced HTTP/gRPC options");
        Console.WriteLine("    → HTTPS, custom ports");
        Console.WriteLine("    → Full control\n");

        Console.Write("Select transport (1-4): ");
        var choice = Console.ReadLine()?.Trim() ?? "1";

        VirtualDriveServer server;
        var bufferConfig = MemoryBufferConfiguration.CreateDefault();

        try
        {
            server = choice switch
            {
                // Named Pipes - Local interprocess communication
                "1" => UseVirtualDriveLocal("VirtualDrive.Server", bufferConfig),

                // HTTP/gRPC - Network accessible with default configuration
                "2" => UseVirtualDriveNetwork("http://0.0.0.0:5051", bufferConfig),

                // High-throughput - Optimized for performance
                "3" => UseVirtualDriveHighThroughput(bufferConfig),

                // Custom network configuration with detailed options
                "4" => UseVirtualDriveNetworkWithCustomOptions(bufferConfig),

                _ => UseVirtualDriveLocal("VirtualDrive.Server", bufferConfig)
            };

            // Display server configuration
            Console.WriteLine("\n╔════════════════════════════════════════════╗");
            Console.WriteLine("║         Server Started Successfully         ║");
            Console.WriteLine("╚════════════════════════════════════════════╝\n");
            
            Console.WriteLine("Server is running. Press Ctrl+C to shutdown.\n");

            // Start server with graceful shutdown
            await using (server)
            {
                var cts = new CancellationTokenSource();

                // Handle Ctrl+C gracefully
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n\nShutdown signal received. Cleaning up...");
                    cts.Cancel();
                };

                // Run server until cancelled
                await server.RunAsync(cts.Token);
            }

            Console.WriteLine("Server shutdown complete.");
        }
        catch (OperationCanceledException)
        {
            // Expected when user presses Ctrl+C
            Console.WriteLine("Server was cancelled by user.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n╔════════════════════════════════════════════╗");
            Console.Error.WriteLine($"║              Server Error                   ║");
            Console.Error.WriteLine($"╚════════════════════════════════════════════╝\n");
            Console.Error.WriteLine($"Type: {ex.GetType().Name}");
            Console.Error.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
            Console.Error.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Demonstrates advanced HTTP/gRPC server configuration with NetworkServerOptions
    /// </summary>
    static VirtualDriveServer UseVirtualDriveNetworkWithCustomOptions(MemoryBufferConfiguration? bufferConfig)
    {
        // Create configuration with custom settings
        var options = new NetworkServerOptions
        {
            Host = "0.0.0.0",           // All network interfaces
            Port = 5051,                // Custom port number
            UseHttps = false,           // HTTP only (set to true for HTTPS)
            BufferConfig = bufferConfig
            
            // Optional: Configure HTTPS
            // UseHttps = true,
            // CertificatePath = "./certs/server.pfx",
            // CertificatePassword = "your-password"
        };

        Console.WriteLine($"\n[Config] HTTP Server: {options.BuildEndpoint()}");
        Console.WriteLine($"[Config] Host: {options.Host}");
        Console.WriteLine($"[Config] Port: {options.Port}");
        Console.WriteLine($"[Config] HTTPS: {(options.UseHttps ? "Enabled" : "Disabled")}\n");

        return UseVirtualDriveNetwork(options);
    }
}
