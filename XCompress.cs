using System;
using System.Runtime.InteropServices;

namespace XCompressAnalyzer
{
    public enum XMEMCODEC_TYPE : int
    {
        XMEMCODEC_DEFAULT = 0,
        XMEMCODEC_LZX = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XMEMCODEC_PARAMETERS_LZX
    {
        public uint Flags;
        public uint WindowSize;
        public uint CompressionPartitionSize;
    }

    public class XCOMPRESS_FILE_HEADER_LZXNATIVE
    {
        // Common header
        public uint Identifier { get; set; }
        public uint Version { get; set; }      // 4 bytes
        public uint Reserved { get; set; }      // 4 bytes
        
        // ContextFlags (4)
        public uint ContextFlags { get; set; }
        
        // Codec parameters (8 - no separate Flags field)
        public uint WindowSize { get; set; }
        public uint CompressionPartitionSize { get; set; }
        
        // Size fields (16)
        public uint UncompressedSizeHigh { get; set; }
        public uint UncompressedSizeLow { get; set; }
        public uint CompressedSizeHigh { get; set; }
        public uint CompressedSizeLow { get; set; }
        
        // Block config (8)
        public uint UncompressedBlockSize { get; set; }
        public uint CompressedBlockSizeMax { get; set; }

        public ulong UncompressedSize => ((ulong)UncompressedSizeHigh << 32) | UncompressedSizeLow;
        
        // xbcompress quirk: CompressedSizeHigh often stores UncompressedBlockSize
        // and CompressedSizeLow stores CompressedBlockSizeMax
        public ulong CompressedSizeFromParts => ((ulong)CompressedSizeHigh << 32) | CompressedSizeLow;
        public uint CompressedSize32 => CompressedSizeHigh;

        public uint VersionMajor => (Version >> 24) & 0xFF;
        public uint VersionMinor => (Version >> 16) & 0xFF;
    }

    internal static class XCompress
    {
        private const string DLL = "xcompress";

        static XCompress()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetDllDirectory();
            }
        }

        private static void SetDllDirectory()
        {
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string arch = Environment.Is64BitProcess ? "x64" : "x86";
                string dir = Path.Combine(path, arch);
                if (File.Exists(Path.Combine(dir, DLL + ".dll")) || File.Exists(Path.Combine(path, DLL + ".dll")))
                {
                    SetDllDirectory(path);
                }
            }
            catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemGetDecompressionContextSize(XMEMCODEC_TYPE codecType, ref int contextSize);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemGetCompressionContextSize(XMEMCODEC_TYPE codecType, ref int contextSize);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemCreateDecompressionContext(XMEMCODEC_TYPE codecType, ref XMEMCODEC_PARAMETERS_LZX codecParams, int flags, ref IntPtr context);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemResetDecompressionContext(IntPtr context);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemDecompress(IntPtr context, byte[] dest, ref int destLen, byte[] src, int srcLen);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern void XMemDestroyDecompressionContext(IntPtr context);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemCreateCompressionContext(XMEMCODEC_TYPE codecType, ref XMEMCODEC_PARAMETERS_LZX codecParams, int flags, ref IntPtr context);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemResetCompressionContext(IntPtr context);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int XMemCompress(IntPtr context, byte[] dest, ref int destLen, byte[] src, int srcLen);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern void XMemDestroyCompressionContext(IntPtr context);
    }
}
