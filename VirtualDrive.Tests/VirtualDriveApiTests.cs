using VirtualDrive.Core;
using Xunit;

namespace VirtualDrive.Tests;

public class VirtualDriveApiTests
{
    private readonly VirtualDriveApi _api = new();
    private const string TestVolumeName = "TestVolume";
    private const long TestVolumeSizeMB = 256;

    [Fact]
    public void CreateVolume_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var volumes = _api.ListVolumes().ToList();
        Assert.Single(volumes);
        Assert.Equal(TestVolumeName, volumes[0].Name);
        Assert.Equal(TestVolumeSizeMB * 1024 * 1024, volumes[0].CapacityBytes);
    }

    [Fact]
    public void CreateVolume_DuplicateThrows()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            _api.CreateVolume(TestVolumeName, TestVolumeSizeMB));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void DeleteVolume_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.DeleteVolume(TestVolumeName);
        
        var volumes = _api.ListVolumes().ToList();
        Assert.Empty(volumes);
    }

    [Fact]
    public void WriteFile_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        
        _api.WriteFile(TestVolumeName, "\\test.bin", testData);
        
        Assert.True(_api.FileExists(TestVolumeName, "\\test.bin"));
    }

    [Fact]
    public void ReadFile_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        
        _api.WriteFile(TestVolumeName, "\\test.bin", testData);
        var readData = _api.ReadFile(TestVolumeName, "\\test.bin");
        
        Assert.Equal(testData, readData);
    }

    [Fact]
    public void ReadFile_NonExistent_Throws()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            _api.ReadFile(TestVolumeName, "\\nonexistent.bin"));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void WriteFileAt_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        _api.WriteFileAt(TestVolumeName, "\\test.bin", new byte[] { 1, 2, 3 }, 0);
        _api.WriteFileAt(TestVolumeName, "\\test.bin", new byte[] { 4, 5 }, 3);
        
        var data = _api.ReadFile(TestVolumeName, "\\test.bin");
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, data);
    }

    [Fact]
    public void ReadFileAt_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.WriteFile(TestVolumeName, "\\test.bin", new byte[] { 1, 2, 3, 4, 5 });
        
        var buffer = new byte[3];
        var bytesRead = _api.ReadFileAt(TestVolumeName, "\\test.bin", buffer, 1);
        
        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 2, 3, 4 }, buffer);
    }

    [Fact]
    public void DeleteFile_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.WriteFile(TestVolumeName, "\\test.bin", new byte[] { 1, 2, 3 });
        
        _api.DeleteFile(TestVolumeName, "\\test.bin");
        
        Assert.False(_api.FileExists(TestVolumeName, "\\test.bin"));
    }

    [Fact]
    public void CreateDirectory_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        _api.CreateDirectory(TestVolumeName, "\\folder");
        
        Assert.True(_api.DirectoryExists(TestVolumeName, "\\folder"));
    }

    [Fact]
    public void CreateNestedDirectory_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        _api.CreateDirectory(TestVolumeName, "\\folder\\subfolder\\deep");
        
        Assert.True(_api.DirectoryExists(TestVolumeName, "\\folder\\subfolder\\deep"));
    }

    [Fact]
    public void WriteFileInDirectory_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        _api.WriteFile(TestVolumeName, "\\folder\\test.bin", new byte[] { 1, 2, 3 });
        
        Assert.True(_api.FileExists(TestVolumeName, "\\folder\\test.bin"));
        Assert.True(_api.DirectoryExists(TestVolumeName, "\\folder"));
    }

    [Fact]
    public void ListFiles_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.CreateDirectory(TestVolumeName, "\\folder");
        _api.WriteFile(TestVolumeName, "\\folder\\file1.txt", new byte[] { 1 });
        _api.WriteFile(TestVolumeName, "\\folder\\file2.txt", new byte[] { 2 });
        
        var files = _api.ListFiles(TestVolumeName, "\\folder").ToList();
        
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.Name == "file1.txt");
        Assert.Contains(files, f => f.Name == "file2.txt");
    }

    [Fact]
    public void ListDirectories_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.CreateDirectory(TestVolumeName, "\\dir1");
        _api.CreateDirectory(TestVolumeName, "\\dir2");
        
        var dirs = _api.ListDirectories(TestVolumeName, "\\").ToList();
        
        Assert.Equal(2, dirs.Count);
        Assert.Contains(dirs, d => d.Name == "dir1");
        Assert.Contains(dirs, d => d.Name == "dir2");
    }

    [Fact]
    public void GetFileInfo_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        _api.WriteFile(TestVolumeName, "\\test.bin", testData);
        
        var info = _api.GetFileInfo(TestVolumeName, "\\test.bin");
        
        Assert.Equal("test.bin", info.Name);
        Assert.Equal(5, info.Size);
    }

    [Fact]
    public void GetDirectoryInfo_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        _api.CreateDirectory(TestVolumeName, "\\folder");
        _api.WriteFile(TestVolumeName, "\\folder\\file1.txt", new byte[] { 1 });
        _api.WriteFile(TestVolumeName, "\\folder\\file2.txt", new byte[] { 2 });
        _api.CreateDirectory(TestVolumeName, "\\folder\\subfolder");
        
        var info = _api.GetDirectoryInfo(TestVolumeName, "\\folder");
        
        Assert.Equal("folder", info.Name);
        Assert.Equal(2, info.FileCount);
        Assert.Equal(1, info.SubdirectoryCount);
    }

    [Fact]
    public void GetUsedBytes_IncreaseWithWrites()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var before = _api.GetUsedBytes(TestVolumeName);
        _api.WriteFile(TestVolumeName, "\\test.bin", new byte[1024]);
        var after = _api.GetUsedBytes(TestVolumeName);
        
        Assert.True(after > before);
    }

    [Fact]
    public void GetAvailableBytes_DecreaseWithWrites()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var before = _api.GetAvailableBytes(TestVolumeName);
        _api.WriteFile(TestVolumeName, "\\test.bin", new byte[1024]);
        var after = _api.GetAvailableBytes(TestVolumeName);
        
        Assert.True(after < before);
    }

    [Fact]
    public void GetCapacityBytes_Correct()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        var capacity = _api.GetCapacityBytes(TestVolumeName);
        
        Assert.Equal(TestVolumeSizeMB * 1024 * 1024, capacity);
    }

    [Fact]
    public void LargeFileWrite_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        var largeData = new byte[10 * 1024 * 1024]; // 10 MB
        Random.Shared.NextBytes(largeData);
        
        _api.WriteFile(TestVolumeName, "\\large.bin", largeData);
        var readData = _api.ReadFile(TestVolumeName, "\\large.bin");
        
        Assert.Equal(largeData, readData);
    }

    [Fact]
    public void MultipleFiles_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        for (int i = 0; i < 100; i++)
        {
            _api.WriteFile(TestVolumeName, $"\\file{i:D3}.bin", new byte[1024]);
        }
        
        var files = _api.ListFiles(TestVolumeName, "\\").ToList();
        Assert.Equal(100, files.Count);
    }

    [Fact]
    public void NestedDirectoryStructure_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        
        for (int i = 0; i < 5; i++)
        {
            _api.CreateDirectory(TestVolumeName, $"\\level1\\level2__{i}\\level3");
        }
        
        var dirs = _api.ListDirectories(TestVolumeName, "\\level1").ToList();
        Assert.Equal(5, dirs.Count);
    }

    [Fact]
    public void FileEditing_Success()
    {
        _api.CreateVolume(TestVolumeName, TestVolumeSizeMB);
        var originalData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        
        _api.WriteFile(TestVolumeName, "\\test.bin", originalData);
        _api.WriteFileAt(TestVolumeName, "\\test.bin", new byte[] { 100 }, 5);
        
        var edited = _api.ReadFile(TestVolumeName, "\\test.bin");
        Assert.Equal(100, edited[5]);
        Assert.Equal(originalData[0], edited[0]);
    }
}
