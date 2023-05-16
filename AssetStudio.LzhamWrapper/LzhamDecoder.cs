using System;
using AssetStudio.PInvoke;

namespace AssetStudio.LzhamWrapper;

public static partial class LzhamDecoder
{
    static LzhamDecoder() => DllLoader.PreloadDll(LzhamDll.DllName);

    public static unsafe DecompressionHandle DecompressInit(DecompressionParameters parameters)
    {
        var decompressionParameters = new NativeDecompressionParameters
        {
            m_struct_size = (uint)sizeof(NativeDecompressionParameters),
            m_decompress_flags = parameters.Flags,
            m_dict_size_log2 = parameters.DictionarySize,
            m_table_max_update_interval = parameters.MaxUpdateInterval,
            m_table_update_interval_slow_rate = parameters.UpdateIntervalSlowRate,
            m_table_update_rate = parameters.UpdateRate
        };

        if (parameters.SeedBytes != null)
            decompressionParameters.m_num_seed_bytes = (uint)parameters.SeedBytes.Length;

        fixed (byte* seedBytes = parameters.SeedBytes)
        {
            decompressionParameters.m_pSeed_bytes = seedBytes;
            var pBytes = (byte*)&decompressionParameters;
            return lzham_decompress_init(pBytes);
        }
    }

    public static unsafe DecompressionHandle DecompressReinit(DecompressionHandle state, DecompressionParameters parameters)
    {
        NativeDecompressionParameters decompressionParameters = new NativeDecompressionParameters
        {
            m_struct_size = (uint)sizeof(NativeDecompressionParameters),
            m_decompress_flags = parameters.Flags,
            m_dict_size_log2 = parameters.DictionarySize,
            m_table_max_update_interval = parameters.MaxUpdateInterval,
            m_table_update_interval_slow_rate = parameters.UpdateIntervalSlowRate,
            m_table_update_rate = parameters.UpdateRate
        };

        if (parameters.SeedBytes != null)
            decompressionParameters.m_num_seed_bytes = (uint)parameters.SeedBytes.Length;

        fixed (byte* seedBytes = parameters.SeedBytes)
        {
            decompressionParameters.m_pSeed_bytes = seedBytes;
            var pBytes = (byte*)&decompressionParameters;
            return lzham_decompress_reinit(state, pBytes);
        }
    }

    public static uint DecompressDeinit(IntPtr state)
    {
        return (uint)lzham_decompress_deinit(state);
    }

    public static unsafe DecompressionStatus Decompress(DecompressionHandle state, byte[] inBuf, ref int inBufSize, int inBufOffset, byte[] outBuf, ref int outBufSize, int outBufOffset, bool noMoreInputBytesFlag)
    {
        if (inBufOffset + inBufSize > inBuf.Length)
            throw new ArgumentException("Offset plus count is larger than the length of array", nameof(inBuf));
        if (outBufOffset + outBufSize > outBuf.Length)
            throw new ArgumentException("Offset plus count is larger than the length of array", nameof(outBuf));

        fixed (byte* inBytes = inBuf, outBytes = outBuf)
        {
            var inSize = new IntPtr(inBufSize);
            var outSize = new IntPtr(outBufSize);
            var result = (DecompressionStatus)lzham_decompress(state, inBytes+inBufOffset, ref inSize, outBytes+outBufOffset, ref outSize, noMoreInputBytesFlag ? 1 : 0);
            inBufSize = inSize.ToInt32();
            outBufSize = outSize.ToInt32();
            return result;
        }
    }

    public static unsafe DecompressionStatus DecompressMemory(DecompressionParameters parameters, byte[] outBuf, ref int outBufSize, int outBufOffset, byte[] inBuf, int inBufSize, int inBufOffset, ref uint adler32)
    {
        if (outBufOffset + outBufSize > outBuf.Length)
            throw new ArgumentException("Offset plus count is larger than the length of array", nameof(outBuf));
        if (inBufOffset + inBufSize > inBuf.Length)
            throw new ArgumentException("Offset plus count is larger than the length of array", nameof(inBuf));

        var decompressionParameters = new NativeDecompressionParameters
        {
            m_struct_size = (uint)sizeof(NativeDecompressionParameters),
            m_decompress_flags = parameters.Flags,
            m_dict_size_log2 = parameters.DictionarySize,
            m_table_max_update_interval = parameters.MaxUpdateInterval,
            m_table_update_interval_slow_rate = parameters.UpdateIntervalSlowRate,
            m_table_update_rate = parameters.UpdateRate
        };

        if (parameters.SeedBytes != null)
            decompressionParameters.m_num_seed_bytes = (uint)parameters.SeedBytes.Length;

        fixed (byte* seedBytes = parameters.SeedBytes)
        fixed (byte* outBytes = outBuf, inBytes = inBuf)
        {
            decompressionParameters.m_pSeed_bytes = seedBytes;
            var pBytes = (byte*)&decompressionParameters;
            var outSize = new IntPtr(outBufSize);
            var result = (DecompressionStatus)lzham_decompress_memory(pBytes, outBytes+outBufOffset, ref outSize, inBytes+inBufOffset, inBufSize, ref adler32);
            outBufSize = outSize.ToInt32();
            return result;
        }
    }

    public static uint GetVersion() => lzham_get_version();
}