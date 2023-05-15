using System;
using System.Runtime.InteropServices;

namespace AssetStudio.LzhamWrapper;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public unsafe struct NativeDecompressionParameters
{
    public uint m_struct_size;

    public uint m_dict_size_log2;

    public TableUpdateRate m_table_update_rate;

    public DecompressionFlags m_decompress_flags;

    public uint m_num_seed_bytes;

    public byte* m_pSeed_bytes;

    public uint m_table_max_update_interval;

    public uint m_table_update_interval_slow_rate;
}

partial class LzhamDecoder
{
    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint lzham_get_version();

    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe DecompressionHandle lzham_decompress_init(byte* parameters);

    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe DecompressionHandle lzham_decompress_reinit(DecompressionHandle state, byte* parameters);

    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int lzham_decompress_deinit(IntPtr state);

    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int lzham_decompress(DecompressionHandle state, byte* inBuf, ref IntPtr inBufSize, byte* outBuf, ref IntPtr outBufSize, int noMoreInputBytesFlag);

    [DllImport(LzhamDll.DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int lzham_decompress_memory(byte* parameters, byte* dstBuffer, ref IntPtr dstLength, byte* srcBuffer, int srcLenght, ref uint adler32);
}