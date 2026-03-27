using System;
using System.Collections.Generic;

namespace VirtualDrive.Server.Transport;

/// <summary>
/// Specifies the transport protocol for interprocess communication
/// </summary>
public enum TransportProtocol
{
    /// <summary>
    /// Named pipes (Windows) or Unix domain sockets (Unix) - local/intranet only
    /// Low latency, no network overhead, same-machine or LAN
    /// </summary>
    NamedPipes,

    /// <summary>
    /// gRPC over HTTP/2 - local, intranet, and internet
    /// Higher throughput, supports network access, production-grade
    /// </summary>
    GrpcHttp2,

    /// <summary>
    /// gRPC over HTTP/3 (future) - optimal balance
    /// When .NET and gRPC stabilize HTTP/3 support
    /// </summary>
    GrpcHttp3
}

/// <summary>
/// Configuration for VirtualDrive server transport layer
/// </summary>
public class TransportConfiguration
{
    /// <summary>
    /// Protocol to use for IPC
    /// </summary>
    public TransportProtocol Protocol { get; set; } = TransportProtocol.NamedPipes;

    /// <summary>
    /// Named pipe name (for NamedPipes protocol)
    /// Example: "VirtualDrive.Server"
    /// </summary>
    public string NamedPipeName { get; set; } = "VirtualDrive.Server";

    /// <summary>
    /// HTTP server endpoint (for gRPC protocols)
    /// Example: "http://0.0.0.0:5051"
    /// Supports multiple addresses separated by semicolon
    /// </summary>
    public string HttpEndpoint { get; set; } = "http://localhost:5051";

    /// <summary>
    /// Maximum concurrent connections
    /// </summary>
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Request timeout in milliseconds
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Enable compression for large payloads (gRPC only)
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Buffer size for streaming operations (bytes)
    /// </summary>
    public int StreamingBufferSize { get; set; } = 1024 * 1024; // 1MB
}

/// <summary>
/// Factory for creating transport configured instances
/// </summary>
public static class TransportConfigurationFactory
{
    /// <summary>
    /// Default configuration using Named Pipes for local/intranet
    /// </summary>
    public static TransportConfiguration CreateDefault() => new()
    {
        Protocol = TransportProtocol.NamedPipes,
        NamedPipeName = "VirtualDrive.Server",
        MaxConnections = 100,
        ConnectionTimeoutMs = 30000,
        RequestTimeoutMs = 60000
    };

    /// <summary>
    /// Configuration for local Named Pipes access
    /// Low latency, no network overhead
    /// </summary>
    public static TransportConfiguration CreateLocalPipes(string pipeName = "VirtualDrive.Server") => new()
    {
        Protocol = TransportProtocol.NamedPipes,
        NamedPipeName = pipeName,
        MaxConnections = 50,
        ConnectionTimeoutMs = 30000,
        RequestTimeoutMs = 30000
    };

    /// <summary>
    /// Configuration for HTTP/gRPC access (local and network)
    /// Supports LAN and internet access
    /// </summary>
    public static TransportConfiguration CreateGrpcHttp(string httpEndpoint = "http://0.0.0.0:5051") => new()
    {
        Protocol = TransportProtocol.GrpcHttp2,
        HttpEndpoint = httpEndpoint,
        MaxConnections = 100,
        ConnectionTimeoutMs = 30000,
        RequestTimeoutMs = 60000,
        EnableCompression = true
    };

    /// <summary>
    /// Configuration for internet deployment with HTTPS
    /// </summary>
    public static TransportConfiguration CreateSecureGrpcHttp(
        string httpsEndpoint = "https://0.0.0.0:5051",
        string? certPath = null,
        string? certPassword = null) => new()
    {
        Protocol = TransportProtocol.GrpcHttp2,
        HttpEndpoint = httpsEndpoint,
        MaxConnections = 200,
        ConnectionTimeoutMs = 60000,
        RequestTimeoutMs = 120000,
        EnableCompression = true
    };

    /// <summary>
    /// High-throughput configuration for low-latency scenarios
    /// </summary>
    public static TransportConfiguration CreateHighThroughput() => new()
    {
        Protocol = TransportProtocol.NamedPipes,
        NamedPipeName = "VirtualDrive.HighThroughput",
        MaxConnections = 200,
        ConnectionTimeoutMs = 60000,
        RequestTimeoutMs = 120000,
        StreamingBufferSize = 4 * 1024 * 1024 // 4MB
    };
}
