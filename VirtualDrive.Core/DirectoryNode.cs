using System;
using System.Collections.Generic;

namespace VirtualDrive.Core;

/// <summary>
/// Represents a node in the hierarchical directory tree.
/// Supports O(1) lookups within each directory level and efficient traversal.
/// </summary>
public class DirectoryNode
{
    /// <summary>
    /// Normalized name (lowercase) used for case-insensitive lookups.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Original case-preserved name for display and API responses.
    /// </summary>
    public string OriginalName { get; set; }

    /// <summary>Parent directory node; null for root</summary>
    public DirectoryNode? Parent { get; set; }
    
    /// <summary>Subdirectories in this node</summary>
    public Dictionary<string, DirectoryNode> Subdirectories { get; }
    
    /// <summary>Files contained in this directory</summary>
    public Dictionary<string, FileMetadata> Files { get; }

    public DirectoryNode(string name, DirectoryNode? parent = null)
    {
        var normalizedName = NormalizeName(name);
        Name = normalizedName;
        OriginalName = name ?? "";
        Parent = parent;
        Subdirectories = new Dictionary<string, DirectoryNode>(StringComparer.OrdinalIgnoreCase);
        Files = new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the full path from root to this node, using original case.
    /// </summary>
    public string GetFullPath()
    {
        var parts = new List<string>();
        var current = this;

        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.OriginalName))
                parts.Add(current.OriginalName);
            current = current.Parent;
        }

        parts.Reverse();
        return parts.Count == 0 ? "\\" : "\\" + string.Join("\\", parts);
    }

    /// <summary>
    /// Creates a new subdirectory with the given name.
    /// Returns the created DirectoryNode or null if already exists.
    /// </summary>
    public DirectoryNode? CreateSubdirectory(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string normalizedName = NormalizeName(name);

        if (Subdirectories.ContainsKey(normalizedName))
            return null; // Already exists

        var newDir = new DirectoryNode(name, this); // Pass original name; constructor handles normalization
        Subdirectories[normalizedName] = newDir;
        return newDir;
    }

    /// <summary>
    /// Gets a subdirectory by name (case-insensitive).
    /// </summary>
    public DirectoryNode? GetSubdirectory(string name)
    {
        string normalizedName = NormalizeName(name);
        return Subdirectories.TryGetValue(normalizedName, out var dir) ? dir : null;
    }

    /// <summary>
    /// Deletes a subdirectory and all its contents.
    /// Returns true if successful, false if not found or directory not empty.
    /// </summary>
    public bool DeleteSubdirectory(string name)
    {
        string normalizedName = NormalizeName(name);

        if (!Subdirectories.TryGetValue(normalizedName, out var dir))
            return false;

        // Directory must be empty
        if (dir.Subdirectories.Count > 0 || dir.Files.Count > 0)
            return false;

        Subdirectories.Remove(normalizedName);
        dir.Parent = null;
        return true;
    }

    /// <summary>
    /// Creates a new file in this directory.
    /// Returns true if created, false if already exists.
    /// </summary>
    public bool CreateFile(string name, FileMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(name) || metadata == null)
            return false;

        string normalizedName = NormalizeName(name);

        if (Files.ContainsKey(normalizedName))
            return false; // Already exists

        Files[normalizedName] = metadata;
        return true;
    }

    /// <summary>
    /// Gets a file by name (case-insensitive).
    /// </summary>
    public FileMetadata? GetFile(string name)
    {
        string normalizedName = NormalizeName(name);
        return Files.TryGetValue(normalizedName, out var file) ? file : null;
    }

    /// <summary>
    /// Deletes a file from this directory.
    /// </summary>
    public bool DeleteFile(string name)
    {
        string normalizedName = NormalizeName(name);
        return Files.Remove(normalizedName);
    }

    /// <summary>
    /// Navigates to a node using a full path (e.g., "\folder1\folder2").
    /// Returns null if path doesn't exist.
    /// </summary>
    public DirectoryNode? NavigateToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "\\")
            return GetRoot();

        path = path.Trim('\\');
        string[] parts = path.Split('\\');

        var current = GetRoot();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            current = current.GetSubdirectory(part);
            if (current == null)
                return null;
        }

        return current;
    }

    /// <summary>
    /// Gets the root directory.
    /// </summary>
    public DirectoryNode GetRoot()
    {
        var current = this;
        while (current.Parent != null)
            current = current.Parent;
        return current;
    }

    /// <summary>
    /// Normalizes directory/file names (case-insensitive, trim whitespace).
    /// </summary>
    private static string NormalizeName(string name)
    {
        return (name ?? "").Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets total file count recursively in this directory tree.
    /// </summary>
    public int GetFileCount()
    {
        int count = Files.Count;
        foreach (var subdir in Subdirectories.Values)
            count += subdir.GetFileCount();
        return count;
    }

    /// <summary>
    /// Gets total used bytes recursively for all files in this directory tree.
    /// </summary>
    public long GetTotalBytes()
    {
        long total = 0;
        foreach (var file in Files.Values)
            total += file.Size;

        foreach (var subdir in Subdirectories.Values)
            total += subdir.GetTotalBytes();

        return total;
    }
}
