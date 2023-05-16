namespace AssetStudio.LzhamWrapper;

public class DecompressionParameters
{
    public uint DictionarySize { get; set; }

    public TableUpdateRate UpdateRate { get; set; }
    
    public DecompressionFlags Flags { get; set; }
    
    public byte[]? SeedBytes { get; set; }
    
    public uint MaxUpdateInterval { get; set; }
    
    public uint UpdateIntervalSlowRate { get; set; }
}