namespace AssetStudio.LzhamWrapper;

public enum DecompressionStatus
{
    NotFinished = 0,
    HasMoreOutput,
    NeedsMoreInput,
    FirstSuccessOrFailCode,
    Success = FirstSuccessOrFailCode,
    FirstFailureCode,
    FailedInitializing = FirstFailureCode,
    FailedDestBufTooSmall,
    FailedExpectedMoreRawBytes,
    FailedBadCode,
    FailedAdler32,
    FailedBadRawBlock,
    FailedBadCompBlockSyncCheck,
    FailedBadZlibHeader,
    FailedNeedSeedBytes,
    FailedBadSeedBytes,
    FailedBadSyncBlock,
    InvalidParameter
}