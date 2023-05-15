using System.Text;
using AssetStudio.LzhamWrapper;
using Xunit.Abstractions;

namespace AssetStudio.Tests;

public class LzhamDecoderTests
{
    [Fact]
    public void GetVersion_ShouldReturnValidVersion()
    {
        var version = (int)LzhamDecoder.GetVersion();

        Assert.Equal(0x1010, version);
    }

    [Fact]
    public void DecompressMemory_WithValidInput_DecompressesCorrectly()
    {
        // Arrange
        DecompressionParameters parameters = new DecompressionParameters
        {
            DictionarySize = 28, Flags = DecompressionFlags.ComputeAdler32
        };

        byte[] inBuf = {
            65, 141, 207, 77, 133, 70, 134, 151, 50, 3, 248, 65, 1, 235,
            163, 43, 154, 88, 2, 123, 24, 101, 50, 0, 51, 52, 53, 54, 55,
            94, 147, 168, 1, 176, 192, 21, 178, 28, 81
        };
        int inBufSize = inBuf.Length;
        int inBufOffset = 0;

        byte[] outBuf = new byte[1024];
        int outBufSize = outBuf.Length;
        int outBufOffset = 0;
        
        uint adler32 = 0;

        // Act
        var result = LzhamDecoder.DecompressMemory(parameters, outBuf, ref outBufSize, outBufOffset, inBuf, inBufSize, inBufOffset, ref adler32);

        // Assert
        Assert.NotNull(outBuf);
        Assert.NotEmpty(outBuf);
        Assert.Equal(DecompressionStatus.Success, result);
        string expectedOutput = "This is a test.This is a test.This is a test.1234567This is a test.This is a test.123456";
        byte[] expectedOutputBytes = Encoding.UTF8.GetBytes(expectedOutput);
        Assert.Equal(expectedOutputBytes, outBuf.AsSpan(0, outBufSize).ToArray());
    }
}