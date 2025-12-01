namespace Unpaker;

/// <summary>
/// Information about a file entry in a pak archive
/// </summary>
public class EntryInfo
{
    public string Path { get; set; } = "";
    public ulong Offset { get; set; }
    public ulong CompressedSize { get; set; }
    public ulong UncompressedSize { get; set; }
    public Compression? Compression { get; set; }
    public bool IsEncrypted { get; set; }
}

