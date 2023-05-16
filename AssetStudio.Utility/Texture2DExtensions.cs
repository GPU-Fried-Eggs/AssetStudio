using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AssetStudio.Utility;

public static class Texture2DExtensions
{
    public static Image<Bgra32> ConvertToImage(this Texture2D texture2D, bool flip)
    {
        var converter = new Texture2DConverter(texture2D);
        var buff = BigArrayPool<byte>.Shared.Rent(texture2D.m_Width * texture2D.m_Height * 4);
        try
        {
            if (converter.DecodeTexture2D(buff))
            {
                var image = Image.LoadPixelData<Bgra32>(buff, texture2D.m_Width, texture2D.m_Height);
                if (flip) image.Mutate(x => x.Flip(FlipMode.Vertical));
                return image;
            }
            return null;
        }
        finally
        {
            BigArrayPool<byte>.Shared.Return(buff);
        }
    }

    public static MemoryStream ConvertToStream(this Texture2D texture2D, ImageFormat imageFormat, bool flip)
    {
        var image = ConvertToImage(texture2D, flip);
        if (image != null)
        {
            using (image)
            {
                return image.ConvertToStream(imageFormat);
            }
        }
        return null;
    }
}