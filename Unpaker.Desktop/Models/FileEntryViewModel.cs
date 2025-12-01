namespace Unpaker.Desktop.Models;

public class FileEntryViewModel
{
    public string FilePath { get; set; } = "";
    public string Size { get; set; } = "";
    public string Compression { get; set; } = "";
    public string Offset { get; set; } = "";
    public string CompressedSize { get; set; } = "";
    public string UncompressedSize { get; set; } = "";

    public string OriginalPath { get; set; } = "";
}

