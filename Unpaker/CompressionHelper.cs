using System.IO.Compression;
using System.Reflection;
using K4os.Compression.LZ4;
#if OODLE_SUPPORTED
using OodleDotNet;
#endif
using ZstdSharp;

namespace Unpaker;

/// <summary>
/// Helper for compression and decompression operations
/// </summary>
public static class CompressionHelper
{
#if OODLE_SUPPORTED
    private static readonly Lazy<Oodle?> _oodle = new(InitializeOodle);
#endif

    /// <summary>
    /// The name of the native Oodle library file
    /// </summary>
    public const string OodleLibraryName = "oo2core_9_win64.dll";

    /// <summary>
    /// Gets whether Oodle compression is available
    /// </summary>
#if OODLE_SUPPORTED
    public static bool IsOodleAvailable => _oodle.Value != null;
#else
    public static bool IsOodleAvailable => false;
#endif

#if OODLE_SUPPORTED
    private static Oodle? InitializeOodle()
    {
        // Try to find the native Oodle library in various locations
        var searchPaths = new List<string>();
        
        // 1. Current directory
        searchPaths.Add(Path.Combine(Environment.CurrentDirectory, OodleLibraryName));
        
        // 2. Executing assembly directory
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                searchPaths.Add(Path.Combine(assemblyDir, OodleLibraryName));
            }
        }
        
        // 3. Entry assembly directory (for applications using this library)
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var entryLocation = entryAssembly.Location;
            if (!string.IsNullOrEmpty(entryLocation))
            {
                var entryDir = Path.GetDirectoryName(entryLocation);
                if (!string.IsNullOrEmpty(entryDir))
                {
                    searchPaths.Add(Path.Combine(entryDir, OodleLibraryName));
                }
            }
        }
        
        // 4. AppDomain base directory
        searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, OodleLibraryName));
        
        foreach (var path in searchPaths.Distinct())
        {
            if (File.Exists(path))
            {
                try
                {
                    return new Oodle(path);
                }
                catch
                {
                    // Continue searching if initialization fails
                }
            }
        }
        
        return null;
    }
#endif

    /// <summary>
    /// Decompress data using the specified compression method
    /// </summary>
    public static byte[] Decompress(byte[] data, Compression compression, int uncompressedSize)
    {
        return compression switch
        {
            Compression.None => data,
            Compression.Zlib => DecompressZlib(data),
            Compression.Gzip => DecompressGzip(data),
            Compression.Zstd => DecompressZstd(data),
            Compression.LZ4 => DecompressLZ4(data, uncompressedSize),
            Compression.Oodle => DecompressOodle(data, uncompressedSize),
            _ => throw new CompressionNotSupportedException(compression),
        };
    }

    /// <summary>
    /// Compress data using the specified compression method
    /// </summary>
    public static byte[] Compress(byte[] data, Compression compression)
    {
        return compression switch
        {
            Compression.None => data,
            Compression.Zlib => CompressZlib(data),
            Compression.Gzip => CompressGzip(data),
            Compression.Zstd => CompressZstd(data),
            Compression.LZ4 => CompressLZ4(data),
            Compression.Oodle => CompressOodle(data),
            _ => throw new CompressionNotSupportedException(compression),
        };
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        using var inputStream = new MemoryStream(data);
        using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        zlibStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(outputStream, CompressionLevel.Fastest))
        {
            zlibStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    private static byte[] DecompressGzip(byte[] data)
    {
        using var inputStream = new MemoryStream(data);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    private static byte[] CompressGzip(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    private static byte[] DecompressZstd(byte[] data)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(data).ToArray();
    }

    private static byte[] CompressZstd(byte[] data)
    {
        using var compressor = new Compressor();
        return compressor.Wrap(data).ToArray();
    }

    private static byte[] DecompressLZ4(byte[] data, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        int decoded = LZ4Codec.Decode(data, 0, data.Length, output, 0, uncompressedSize);
        if (decoded != uncompressedSize)
        {
            throw new DecompressionFailedException(Compression.LZ4);
        }
        return output;
    }

    private static byte[] CompressLZ4(byte[] data)
    {
        return LZ4Pickler.Pickle(data);
    }

    private static byte[] DecompressOodle(byte[] data, int uncompressedSize)
    {
#if OODLE_SUPPORTED
        var oodle = _oodle.Value ?? throw new OodleNotAvailableException();
        
        var output = new byte[uncompressedSize];
        var decompressedSize = oodle.Decompress(data, output);
        
        if (decompressedSize != uncompressedSize)
        {
            throw new DecompressionFailedException(Compression.Oodle);
        }
        
        return output;
#else
        throw new CompressionNotSupportedException(Compression.Oodle);
#endif
    }

    private static byte[] CompressOodle(byte[] data)
    {
#if OODLE_SUPPORTED
        var oodle = _oodle.Value ?? throw new OodleNotAvailableException();
        
        // Get the required buffer size for compression
        var compressedBufferSize = oodle.GetCompressedBufferSizeNeeded(OodleCompressor.Kraken, data.Length);
        var compressedBuffer = new byte[compressedBufferSize];
        
        // Compress using Kraken compressor with Normal compression level
        var compressedSize = (int)oodle.Compress(OodleCompressor.Kraken, OodleCompressionLevel.Normal, data, compressedBuffer);
        
        if (compressedSize <= 0)
        {
            throw new CompressionFailedException(Compression.Oodle);
        }
        
        // Return only the compressed data (trim excess buffer)
        var result = new byte[compressedSize];
        Array.Copy(compressedBuffer, result, compressedSize);
        return result;
#else
        throw new CompressionNotSupportedException(Compression.Oodle);
#endif
    }
}
