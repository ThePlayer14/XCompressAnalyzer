# XCompressAnalyzer

A Windows-only C# console application that analyzes Xbox 360 XMemCompress LZXNATIVE compressed blobs (the "Manual Decompression" format produced by `xbcompress /n`). It parses the file header, prints all codec parameters and size information, enumerates compressed blocks, and optionally validates the decompression context using `xcompress.dll`.

## Features

- **Pure analysis** — no extraction or compression performed
- **Loads `xcompress.dll` via P/Invoke**
- **Parses LZXNATIVE format** (magic `0x0FF512EE`, version 1.3)
- **Prints all header fields**: magic, version, context flags, codec params, sizes, block config
- **Enumerates compressed blocks** with sizes and offsets (`-v` / `--verbose`)
- **Validates decompression context** against `xcompress.dll` (`--validate`)


## Requirements

- **Windows x64** (the P/Invoke target is `xcompress.dll`)
- `xcompress.dll` (x64) must be present in the same directory as the executable, or in a `x64/` subdirectory

## Building

```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```

Output: `bin/Release/net8.0-windows/win-x64/publish/XCompressAnalyzer.exe`

## Usage

```cmd
XCompressAnalyzer.exe <file> [options]

Options:
  -v, --verbose     Show detailed block information
  --validate        Validate decompression context creation with xcompress.dll
```

### Example

```cmd
XCompressAnalyzer.exe compressed.dat --verbose --validate
```

## Output Format

```
=== XCompress LZXNATIVE Analysis: compressed.dat ===
File size: 67,584 bytes (66.00 KB)
Endianness: Big-Endian (Xbox 360 native)

--- File Header (XCOMPRESS_FILE_HEADER_LZXNATIVE) ---
  Magic (Identifier):     0x0FF512EE (LZXNATIVE Big-Endian, Xbox 360 native)
  Version:                0x01030000 (Major: 1, Minor: 3)
  Reserved:               0x00000000
  ContextFlags:           0x00000000 (None (standard))

--- Codec Parameters (XMEMCODEC_PARAMETERS_LZX) ---
  WindowSize:             262,144 bytes (256 KB)  <-- xbcompress /w flag
  CompressionPartitionSize: 524,288 bytes (512 KB)  <-- xbcompress /p flag

--- Size Information ---
  UncompressedSize:       8,388,616 bytes (8.00 MB)
  CompressedSize:         66,000 bytes (0.06 MB)
  Compression Ratio:      0.8% of original (99.2% reduction)

--- Block Configuration ---
  UncompressedBlockSize:  1,048,576 bytes (1024 KB)  <-- xbcompress /n flag
  CompressedBlockSizeMax: 31,204 bytes (30.5 KB)

Version 1.3: Standard LZXNATIVE format (Xbox 360 File Compression Tool)
--- Compressed Blocks ---
  Block 0: CompressedSize=4,554 bytes, Offset=0x00000034
  Block 1: CompressedSize=1,382 bytes, Offset=0x00001202
  ...
  Total blocks: 9

--- Decompression Context Validation ---
  XMemGetDecompressionContextSize failed: 0x0003315D
  XMemCreateDecompressionContext: SUCCESS
  Context handle: 0x1037A10
```

## File Format

The tool expects the **Xbcompress** LZXNATIVE header layout (48 bytes), which is the format produced by `xbcompress /n:1024 /w:256`:

```
XCOMPRESS_FILE_HEADER (12 bytes):
  Identifier (4)   = 0x0FF512EE
  Version (4)      = 0x01030000
  Reserved (4)     = 0

ContextFlags (4)   = 0

Codec Parameters (8 bytes):
  WindowSize (4)              = xbcompress /w value in KB * 1024
  CompressionPartitionSize (4)= xbcompress /p value in KB * 1024

Size Fields (16 bytes):
  UncompressedSizeHigh (4)
  UncompressedSizeLow  (4)
  CompressedSizeHigh   (4)
  CompressedSizeLow    (4)

Block Config (8 bytes):
  UncompressedBlockSize (4)   = xbcompress /n value in KB * 1024
  CompressedBlockSizeMax (4)
```

Followed by a sequence of compressed blocks:
```
[BlockSize:4 (BE)] [CompressedData:BlockSize] ...
```

# Note
- OpenCode's Zen models were used during the development and testing phase for this project.


# License
 - FreeMote License for the `FreeMote.XMemCompress` package. Anything else is unser the MIT License.