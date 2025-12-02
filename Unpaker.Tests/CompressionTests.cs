using Xunit;

namespace Unpaker.Tests;

public class CompressionTests
{
    [Theory]
    [InlineData("zlib", Compression.Zlib)]
    [InlineData("Zlib", Compression.Zlib)]
    [InlineData("ZLIB", Compression.Zlib)]
    [InlineData("gzip", Compression.Gzip)]
    [InlineData("zstd", Compression.Zstd)]
    [InlineData("lz4", Compression.LZ4)]
    [InlineData("oodle", Compression.Oodle)]
    public void FromString_ParsesCorrectly(string input, Compression expected)
    {
        var result = CompressionExtensions.FromString(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("invalid")]
    public void FromString_ReturnsNullForInvalid(string input)
    {
        var result = CompressionExtensions.FromString(input);
        Assert.Null(result);
    }

    [Fact]
    public void ToCompressionString_ReturnsCorrectStrings()
    {
        Assert.Equal("Zlib", Compression.Zlib.ToCompressionString());
        Assert.Equal("Gzip", Compression.Gzip.ToCompressionString());
        Assert.Equal("Zstd", Compression.Zstd.ToCompressionString());
        Assert.Equal("LZ4", Compression.LZ4.ToCompressionString());
        Assert.Equal("Oodle", Compression.Oodle.ToCompressionString());
    }

    [Fact]
    public void ZlibCompressionRoundTrip()
    {
        var originalData = new byte[1000];
        new Random(42).NextBytes(originalData);

        var compressed = CompressionHelper.Compress(originalData, Compression.Zlib);
        var decompressed = CompressionHelper.Decompress(compressed, Compression.Zlib, originalData.Length);

        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void GzipCompressionRoundTrip()
    {
        var originalData = new byte[1000];
        new Random(42).NextBytes(originalData);

        var compressed = CompressionHelper.Compress(originalData, Compression.Gzip);
        var decompressed = CompressionHelper.Decompress(compressed, Compression.Gzip, originalData.Length);

        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void ZstdCompressionRoundTrip()
    {
        var originalData = new byte[1000];
        new Random(42).NextBytes(originalData);

        var compressed = CompressionHelper.Compress(originalData, Compression.Zstd);
        var decompressed = CompressionHelper.Decompress(compressed, Compression.Zstd, originalData.Length);

        Assert.Equal(originalData, decompressed);
    }

#if OODLE_SUPPORTED
    [Fact]
    public void OodleCompressionRoundTrip()
    {
        // Skip if Oodle is not available (native library not found)
        if (!CompressionHelper.IsOodleAvailable)
        {
            return;
        }
        
        var originalData = new byte[1000];
        new Random(42).NextBytes(originalData);

        var compressed = CompressionHelper.Compress(originalData, Compression.Oodle);
        var decompressed = CompressionHelper.Decompress(compressed, Compression.Oodle, originalData.Length);

        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public void OodleIsAvailable()
    {
        // Verify that Oodle is available in the test environment
        Assert.True(CompressionHelper.IsOodleAvailable, 
            "Oodle native library should be available in test environment");
    }
#else
    [Fact]
    public void OodleCompressionThrowsNotSupported()
    {
        var data = new byte[100];
        Assert.Throws<CompressionNotSupportedException>(() =>
            CompressionHelper.Compress(data, Compression.Oodle));
    }
#endif
}

