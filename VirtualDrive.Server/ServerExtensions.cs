using System;
using VirtualDrive.Core;
using VirtualDrive.Server.Transport;

namespace VirtualDrive.Server.Extensions;

/// <summary>
/// Configuration options for HTTP/gRPC network server
/// </summary>
public class NetworkServerOptions
{
    /// <summary>
    /// The host binding (default: "0.0.0.0" for all interfaces)
    /// </summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>
    /// The port number (default: 5051)
    /// </summary>
    public int Port { get; set; } = 5051;

    /// <summary>
    /// Whether to use HTTPS/TLS (default: false)
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// Certificate path for HTTPS (optional)
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Certificate password for HTTPS (optional)
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Buffer configuration for memory management
    /// </summary>
    public MemoryBufferConfiguration? BufferConfig { get; set; }

    /// <summary>
    /// Build the complete HTTP endpoint URL from configuration
    /// </summary>
    public string BuildEndpoint()
    {
        var scheme = UseHttps ? "https" : "http";
        return $"{scheme}://{Host}:{Port}";
    }
}

/// <summary>
/// Extension methods for easy VirtualDrive server initialization and configuration
/// </summary>
public static class VirtualDriveServerExtensions
{
    /// <summary>
    /// Create and initialize a VirtualDrive server with default configuration
    /// </summary>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDrive(
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return VirtualDriveServer.CreateDefault(bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server for local Named Pipes access
    /// </summary>
    /// <param name="pipeName">The Named Pipes name (default: "VirtualDrive.Server")</param>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveLocal(
        string pipeName = "VirtualDrive.Server",
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return VirtualDriveServer.CreateLocal(pipeName, bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server for network HTTP/gRPC access
    /// </summary>
    /// <param name="httpEndpoint">The HTTP endpoint (default: "http://0.0.0.0:5051")</param>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveNetwork(
        string httpEndpoint = "http://0.0.0.0:5051",
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return VirtualDriveServer.CreateNetwork(httpEndpoint, bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server for network HTTP/gRPC access with port configuration
    /// </summary>
    /// <param name="host">The host binding (default: "0.0.0.0" for all interfaces, or "127.0.0.1" for localhost only)</param>
    /// <param name="port">The port number (default: 5051)</param>
    /// <param name="useHttps">Whether to use HTTPS/TLS (default: false)</param>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveNetwork(
        string host,
        int port,
        bool useHttps = false,
        MemoryBufferConfiguration? bufferConfig = null)
    {
        var scheme = useHttps ? "https" : "http";
        var endpoint = $"{scheme}://{host}:{port}";
        return VirtualDriveServer.CreateNetwork(endpoint, bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server for network HTTP/gRPC access with full configuration
    /// </summary>
    /// <param name="port">The port number (default: 5051)</param>
    /// <param name="host">The host binding (default: "0.0.0.0" for all interfaces)</param>
    /// <param name="useHttps">Whether to use HTTPS/TLS (default: false)</param>
    /// <param name="useLocalhost">If true, binds to 127.0.0.1 instead of 0.0.0.0 (default: false)</param>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveNetworkWithPort(
        int port = 5051,
        string? host = null,
        bool useHttps = false,
        bool useLocalhost = false,
        MemoryBufferConfiguration? bufferConfig = null)
    {
        var resolvedHost = useLocalhost ? "127.0.0.1" : (host ?? "0.0.0.0");
        var scheme = useHttps ? "https" : "http";
        var endpoint = $"{scheme}://{resolvedHost}:{port}";
        return VirtualDriveServer.CreateNetwork(endpoint, bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server optimized for high-throughput scenarios
    /// </summary>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveHighThroughput(
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return VirtualDriveServer.CreateHighThroughput(bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server with custom transport configuration
    /// </summary>
    /// <param name="config">Custom transport configuration</param>
    /// <param name="bufferConfig">Optional memory buffer configuration. Uses defaults if not provided.</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDrive(
        TransportConfiguration config,
        MemoryBufferConfiguration? bufferConfig = null)
    {
        return new VirtualDriveServer(config, bufferConfig);
    }

    /// <summary>
    /// Create and initialize a VirtualDrive server for network access with comprehensive options
    /// </summary>
    /// <param name="options">Network server configuration options</param>
    /// <returns>Initialized VirtualDrive server instance</returns>
    public static VirtualDriveServer UseVirtualDriveNetwork(NetworkServerOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var endpoint = options.BuildEndpoint();
        return VirtualDriveServer.CreateNetwork(endpoint, options.BufferConfig);
    }
}
