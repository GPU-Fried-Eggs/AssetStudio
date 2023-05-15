using System;
using Microsoft.Win32.SafeHandles;

namespace AssetStudio.LzhamWrapper;

public class DecompressionHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DecompressionHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        LzhamDecoder.DecompressDeinit(handle);
        return true;
    }
}