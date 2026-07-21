using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace XCompressAnalyzer
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr LoadLibrary(string lpFileName);
    }

    internal static class Program
    {
        private const uint XCOMPRESS_FILE_IDENTIFIER_LZXNATIVE = 0x0FF512EE;
        private const uint XCOMPRESS_FILE_IDENTIFIER_LZXNATIVE_LE = 0xEE12F50F;

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string inputPath = args[0];
            bool verbose = false;
            bool validate = false;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-v" || args[i] == "--verbose") verbose = true;
                else if (args[i] == "--validate") validate = true;
            }

            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Error: This application requires Windows (xcompress.dll is Windows-only).");
                return 2;
            }

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: File not found: {inputPath}");
                return 2;
            }

            if (!EnsureXCompressDllLoaded())
            {
                Console.Error.WriteLine("Error: Failed to load xcompress.dll. Ensure x64/xcompress.dll exists next to the executable.");
                return 3;
            }

            try
            {
                byte[] fileData = File.ReadAllBytes(inputPath);
                AnalyzeCompressedBlob(fileData, Path.GetFileName(inputPath), verbose, validate);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (verbose) Console.Error.WriteLine(ex.StackTrace);
                return 4;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("XCompressAnalyzer - Analyze Xbox 360 XMemCompress LZXNATIVE compressed blobs");
            Console.WriteLine();
            Console.WriteLine("Usage: XCompressAnalyzer <file> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -v, --verbose     Show detailed block information");
            Console.WriteLine("  --validate        Validate decompression context creation with xcompress.dll");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  XCompressAnalyzer compressed.dat --verbose --validate");
        }

        private static bool EnsureXCompressDllLoaded()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string archDir = Path.Combine(baseDir, "x64");
            string dllPath = Path.Combine(archDir, "xcompress.dll");

            if (!File.Exists(dllPath))
            {
                dllPath = Path.Combine(baseDir, "xcompress.dll");
                if (!File.Exists(dllPath))
                    return false;
            }

            try
            {
                NativeMethods.SetDllDirectory(archDir);
                IntPtr handle = NativeMethods.LoadLibrary(dllPath);
                return handle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        private static void AnalyzeCompressedBlob(byte[] data, string fileName, bool verbose, bool validate)
        {
            Console.WriteLine($"=== XCompress LZXNATIVE Analysis: {fileName} ===");
            Console.WriteLine($"File size: {data.Length:N0} bytes ({data.Length / 1024.0:F2} KB)");
            Console.WriteLine();

            if (data.Length < 48)
            {
                Console.WriteLine("Error: File too small to contain LZXNATIVE header (minimum 48 bytes)");
                return;
            }

            bool isBigEndian = data[0] == 0x0F && data[1] == 0xF5 && data[2] == 0x12 && data[3] == 0xEE;
            bool isLittleEndian = data[0] == 0xEE && data[1] == 0x12 && data[2] == 0xF5 && data[3] == 0x0F;

            if (!isBigEndian && !isLittleEndian)
            {
                uint magic = BitConverter.ToUInt32(data, 0);
                Console.WriteLine($"Error: Not an LZXNATIVE compressed blob. Magic: 0x{magic:X8}");
                Console.WriteLine($"Expected: 0x0FF512EE (big-endian) or 0xEE12F50F (little-endian)");
                return;
            }

            Console.WriteLine($"Endianness: {(isBigEndian ? "Big-Endian (Xbox 360 native)" : "Little-Endian")}");
            Console.WriteLine();

            var header = ReadHeader(data, isBigEndian);
            PrintHeader(header, isBigEndian);

            if (verbose)
            {
                PrintBlockDetails(data, header, isBigEndian);
            }

            if (validate)
            {
                ValidateDecompressionContext(header, fileName);
            }

            Console.WriteLine();
            Console.WriteLine("=== Analysis Complete ===");
        }

private static XCOMPRESS_FILE_HEADER_LZXNATIVE ReadHeader(byte[] data, bool bigEndian)
        {
            var header = new XCOMPRESS_FILE_HEADER_LZXNATIVE();
            int offset = 0;

            // FreeMote/Xbcompress header layout (48 bytes)
            header.Identifier = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.Version = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.Reserved = ReadUInt32(data, offset, bigEndian); offset += 4;
            
            // ContextFlags (4)
            header.ContextFlags = ReadUInt32(data, offset, bigEndian); offset += 4;
            
            // Codec parameters (8 - WindowSize, CompressionPartitionSize; no separate Flags)
            header.WindowSize = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.CompressionPartitionSize = ReadUInt32(data, offset, bigEndian); offset += 4;
            
            // Size fields (16)
            header.UncompressedSizeHigh = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.UncompressedSizeLow = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.CompressedSizeHigh = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.CompressedSizeLow = ReadUInt32(data, offset, bigEndian); offset += 4;
            
            // Block config (8)
            header.UncompressedBlockSize = ReadUInt32(data, offset, bigEndian); offset += 4;
            header.CompressedBlockSizeMax = ReadUInt32(data, offset, bigEndian); offset += 4;

            return header;
        }

        private static uint ReadUInt32(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                return ((uint)data[offset] << 24) |
                       ((uint)data[offset + 1] << 16) |
                       ((uint)data[offset + 2] << 8) |
                       (uint)data[offset + 3];
            }
            else
            {
                return ((uint)data[offset + 3] << 24) |
                       ((uint)data[offset + 2] << 16) |
                       ((uint)data[offset + 1] << 8) |
                       (uint)data[offset];
            }
        }

        private static ushort ReadUInt16(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                return (ushort)(((uint)data[offset] << 8) | (uint)data[offset + 1]);
            }
            else
            {
                return (ushort)(((uint)data[offset + 1] << 8) | (uint)data[offset]);
            }
        }

        private static void PrintHeader(XCOMPRESS_FILE_HEADER_LZXNATIVE header, bool bigEndian)
        {
            Console.WriteLine("--- File Header (XCOMPRESS_FILE_HEADER_LZXNATIVE) ---");
            Console.WriteLine($"  Magic (Identifier):     0x{header.Identifier:X8} ({GetMagicName(header.Identifier)})");
            Console.WriteLine($"  Version:                0x{header.Version:X8} (Major: {header.VersionMajor}, Minor: {header.VersionMinor})");
            Console.WriteLine($"  Reserved:               0x{header.Reserved:X8}");
            Console.WriteLine($"  ContextFlags:           0x{header.ContextFlags:X8} ({GetContextFlagsDescription(header.ContextFlags)})");
            Console.WriteLine();
            Console.WriteLine("--- Codec Parameters (XMEMCODEC_PARAMETERS_LZX) ---");
            Console.WriteLine($"  WindowSize:             {header.WindowSize:N0} bytes ({header.WindowSize / 1024} KB)  <-- xbcompress /w flag");
            Console.WriteLine($"  CompressionPartitionSize: {header.CompressionPartitionSize:N0} bytes ({header.CompressionPartitionSize / 1024} KB)  <-- xbcompress /p flag");
            Console.WriteLine();
            Console.WriteLine("--- Size Information ---");
            Console.WriteLine($"  UncompressedSize:       {header.UncompressedSize:N0} bytes ({header.UncompressedSize / 1024.0 / 1024.0:F2} MB)");
            
            // Handle xbcompress quirk: CompressedSizeHigh often stores UncompressedBlockSize
            // and CompressedSizeLow stores CompressedBlockSizeMax
            ulong compressedSizeFull = header.CompressedSizeFromParts;
            uint compressedSize32 = header.CompressedSize32;
            ulong compressedSize = compressedSizeFull;
            string sizeNote = "";
            
            if (compressedSizeFull > uint.MaxValue && compressedSize32 > 0 && compressedSize32 < 0x10000000)
            {
                // Likely the quirk: real size in High DWORD, Low DWORD repurposed
                compressedSize = compressedSize32;
                sizeNote = " (using High DWORD only; Low DWORD appears repurposed)";
            }
            
            Console.WriteLine($"  CompressedSize:         {compressedSize:N0} bytes ({compressedSize / 1024.0 / 1024.0:F2} MB){sizeNote}");
            if (header.UncompressedSize > 0)
            {
                double ratio = (double)compressedSize / header.UncompressedSize * 100;
                Console.WriteLine($"  Compression Ratio:      {ratio:F1}% of original ({100 - ratio:F1}% reduction)");
            }
            Console.WriteLine();
            Console.WriteLine("--- Block Configuration ---");
            Console.WriteLine($"  UncompressedBlockSize:  {header.UncompressedBlockSize:N0} bytes ({header.UncompressedBlockSize / 1024} KB)  <-- xbcompress /n flag");
            Console.WriteLine($"  CompressedBlockSizeMax: {header.CompressedBlockSizeMax:N0} bytes ({header.CompressedBlockSizeMax / 1024.0:F1} KB)");
            Console.WriteLine();

            if (header.VersionMajor == 1 && header.VersionMinor == 3)
            {
                Console.WriteLine("Version 1.3: Standard LZXNATIVE format (Xbox 360 File Compression Tool)");
            }
            else
            {
                Console.WriteLine($"Warning: Unrecognized version {header.VersionMajor}.{header.VersionMinor}");
            }
        }

        private static string GetMagicName(uint magic)
        {
            return magic switch
            {
                0x0FF512EE => "LZXNATIVE (Big-Endian, Xbox 360 native)",
                0xEE12F50F => "LZXNATIVE (Little-Endian)",
                _ => "Unknown"
            };
        }

        private static string GetContextFlagsDescription(uint flags)
        {
            if (flags == 0) return "None (standard)";
            var parts = new List<string>();
            if ((flags & 0x00000001) != 0) parts.Add("XMEMCODEC_FLAG_REALTIME");
            if ((flags & 0x00000002) != 0) parts.Add("XMEMCODEC_FLAG_HIGH_QUALITY");
            return parts.Count > 0 ? string.Join(" | ", parts) : $"Unknown (0x{flags:X8})";
        }

        private static void PrintBlockDetails(byte[] data, XCOMPRESS_FILE_HEADER_LZXNATIVE header, bool bigEndian)
        {
            Console.WriteLine("--- Compressed Blocks ---");
            int headerSize = 48; // FreeMote/Xbcompress header size (48 bytes)
            int pos = headerSize;
            int blockIndex = 0;
            
            // Use the actual compressed size from the header
            long compressedRemaining = (long)header.CompressedSizeFromParts;
            // If compressed size appears to be using the quirk (High=0, Low>0), use the low part
            if (compressedRemaining == 0 && header.CompressedSizeLow > 0)
            {
                compressedRemaining = header.CompressedSizeLow;
            }

            while (compressedRemaining > 0 && pos < data.Length)
            {
                if (pos + 4 > data.Length) break;

                uint blockSize = bigEndian
                    ? ((uint)data[pos] << 24) | ((uint)data[pos + 1] << 16) | ((uint)data[pos + 2] << 8) | (uint)data[pos + 3]
                    : ((uint)data[pos + 3] << 24) | ((uint)data[pos + 2] << 16) | ((uint)data[pos + 1] << 8) | (uint)data[pos];

                pos += 4;
                compressedRemaining -= 4;

                if (blockSize == 0 || blockSize > compressedRemaining || pos + blockSize > data.Length)
                {
                    Console.WriteLine($"  Block {blockIndex}: INVALID (size={blockSize}, remaining={compressedRemaining})");
                    break;
                }

                Console.WriteLine($"  Block {blockIndex}: CompressedSize={blockSize:N0} bytes, Offset=0x{pos:X8}");
                pos += (int)blockSize;
                compressedRemaining -= blockSize;
                blockIndex++;
            }

            Console.WriteLine($"  Total blocks: {blockIndex}");
            Console.WriteLine();
        }

        private static void ValidateDecompressionContext(XCOMPRESS_FILE_HEADER_LZXNATIVE header, string fileName)
        {
            Console.WriteLine("--- Decompression Context Validation ---");

            int contextSize = 0;
            int hr = XCompress.XMemGetDecompressionContextSize(XMEMCODEC_TYPE.XMEMCODEC_LZX, ref contextSize);
            if (hr != 0)
            {
                Console.WriteLine($"  XMemGetDecompressionContextSize failed: 0x{hr:X8}");
            }
            else
            {
                Console.WriteLine($"  Decompression context size: {contextSize:N0} bytes");
            }

            var parameters = new XMEMCODEC_PARAMETERS_LZX
            {
                Flags = header.ContextFlags,
                WindowSize = header.WindowSize,
                CompressionPartitionSize = header.CompressionPartitionSize
            };

            IntPtr context = IntPtr.Zero;
            hr = XCompress.XMemCreateDecompressionContext(XMEMCODEC_TYPE.XMEMCODEC_LZX, ref parameters, 0, ref context);

            if (hr == 0 && context != IntPtr.Zero)
            {
                Console.WriteLine("  XMemCreateDecompressionContext: SUCCESS");
                Console.WriteLine($"  Context handle: 0x{context.ToString("X")}");
                XCompress.XMemDestroyDecompressionContext(context);
            }
            else
            {
                Console.WriteLine($"  XMemCreateDecompressionContext: FAILED (0x{hr:X8})");
            }
            Console.WriteLine();
        }
    }
}