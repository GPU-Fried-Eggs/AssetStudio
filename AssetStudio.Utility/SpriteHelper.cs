using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AssetStudio.Utility;

public static class SpriteHelper
{
    public static Image<Bgra32> GetImage(this Sprite sprite)
    {
        if (sprite.m_SpriteAtlas != null && sprite.m_SpriteAtlas.TryGet(out var spriteAtlas))
        {
            if (spriteAtlas.m_RenderDataMap.TryGetValue(sprite.m_RenderDataKey, out var spriteAtlasData) && spriteAtlasData.texture.TryGet(out var m_Texture2D))
            {
                return CutImage(sprite, m_Texture2D, spriteAtlasData.textureRect, spriteAtlasData.textureRectOffset, spriteAtlasData.downscaleMultiplier, spriteAtlasData.settingsRaw);
            }
        }
        else
        {
            if (sprite.m_RD.texture.TryGet(out var texture2D))
            {
                return CutImage(sprite, texture2D, sprite.m_RD.textureRect, sprite.m_RD.textureRectOffset, sprite.m_RD.downscaleMultiplier, sprite.m_RD.settingsRaw);
            }
        }
        return null;
    }

    private static Image<Bgra32> CutImage(Sprite sprite, Texture2D texture2D, Rectf textureRect, Vector2 textureRectOffset, float downscaleMultiplier, SpriteSettings settingsRaw)
    {
        var originalImage = texture2D.ConvertToImage(false);
        if (originalImage != null)
        {
            using (originalImage)
            {
                if (downscaleMultiplier > 0f && downscaleMultiplier != 1f)
                {
                    var width = (int)(texture2D.m_Width / downscaleMultiplier);
                    var height = (int)(texture2D.m_Height / downscaleMultiplier);
                    originalImage.Mutate(x => x.Resize(width, height));
                }
                var rectX = (int)Math.Floor(textureRect.x);
                var rectY = (int)Math.Floor(textureRect.y);
                var rectRight = (int)Math.Ceiling(textureRect.x + textureRect.width);
                var rectBottom = (int)Math.Ceiling(textureRect.y + textureRect.height);
                rectRight = Math.Min(rectRight, originalImage.Width);
                rectBottom = Math.Min(rectBottom, originalImage.Height);
                var rect = new Rectangle(rectX, rectY, rectRight - rectX, rectBottom - rectY);
                var spriteImage = originalImage.Clone(x => x.Crop(rect));
                if (settingsRaw.packed == 1)
                {
                    //RotateAndFlip
                    switch (settingsRaw.packingRotation)
                    {
                        case SpritePackingRotation.FlipHorizontal:
                            spriteImage.Mutate(x => x.Flip(FlipMode.Horizontal));
                            break;
                        case SpritePackingRotation.FlipVertical:
                            spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                            break;
                        case SpritePackingRotation.Rotate180:
                            spriteImage.Mutate(x => x.Rotate(180));
                            break;
                        case SpritePackingRotation.Rotate90:
                            spriteImage.Mutate(x => x.Rotate(270));
                            break;
                    }
                }

                //Tight
                if (settingsRaw.packingMode == SpritePackingMode.Tight)
                {
                    try
                    {
                        var triangles = GetTriangles(sprite.m_RD);
                        var polygons = triangles.Select(x => new Polygon(new LinearLineSegment(x.Select(y => new PointF(y.X, y.Y)).ToArray()))).ToArray();
                        IPathCollection path = new PathCollection(polygons);
                        var matrix = Matrix3x2.CreateScale(sprite.m_PixelsToUnits);
                        matrix *= Matrix3x2.CreateTranslation(sprite.m_Rect.width * sprite.m_Pivot.X - textureRectOffset.X, sprite.m_Rect.height * sprite.m_Pivot.Y - textureRectOffset.Y);
                        path = path.Transform(matrix);
                        var graphicsOptions = new GraphicsOptions
                        {
                            Antialias = false,
                            AlphaCompositionMode = PixelAlphaCompositionMode.DestOut
                        };
                        var options = new DrawingOptions { GraphicsOptions = graphicsOptions };
                        using var mask = new Image<Bgra32>(rect.Width, rect.Height, SixLabors.ImageSharp.Color.Black);
                        mask.Mutate(x => x.Fill(options, SixLabors.ImageSharp.Color.Red, path));
                        var bush = new ImageBrush(mask);
                        spriteImage.Mutate(x => x.Fill(options, bush));
                        spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                        return spriteImage;
                    }
                    catch
                    {
                        // ignored
                    }
                }

                //Rectangle
                spriteImage.Mutate(x => x.Flip(FlipMode.Vertical));
                return spriteImage;
            }
        }

        return null;
    }

    private static Vector2[][] GetTriangles(SpriteRenderData renderData)
    {
        if (renderData.vertices != null) //5.6 down
        {
            var vertices = renderData.vertices.Select(x => (Vector2)x.pos).ToArray();
            var triangleCount = renderData.indices.Length / 3;
            var triangles = new Vector2[triangleCount][];
            for (int i = 0; i < triangleCount; i++)
            {
                var first = renderData.indices[i * 3];
                var second = renderData.indices[i * 3 + 1];
                var third = renderData.indices[i * 3 + 2];
                var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                triangles[i] = triangle;
            }
            return triangles;
        }
        else //5.6 and up
        {
            var triangles = new List<Vector2[]>();
            var vertexData = renderData.m_VertexData;
            var channel = vertexData.m_Channels[0]; //kShaderChannelVertex
            var stream = vertexData.m_Streams[channel.stream];
            using (var vertexReader = new BinaryReader(new MemoryStream(vertexData.m_DataSize)))
            {
                using (var indexReader = new BinaryReader(new MemoryStream(renderData.m_IndexBuffer)))
                {
                    foreach (var subMesh in renderData.m_SubMeshes)
                    {
                        vertexReader.BaseStream.Position = stream.offset + subMesh.firstVertex * stream.stride + channel.offset;

                        var vertices = new Vector2[subMesh.vertexCount];
                        for (int v = 0; v < subMesh.vertexCount; v++)
                        {
                            vertices[v] = vertexReader.ReadVector3();
                            vertexReader.BaseStream.Position += stream.stride - 12;
                        }

                        indexReader.BaseStream.Position = subMesh.firstByte;

                        var triangleCount = subMesh.indexCount / 3u;
                        for (int i = 0; i < triangleCount; i++)
                        {
                            var first = indexReader.ReadUInt16() - subMesh.firstVertex;
                            var second = indexReader.ReadUInt16() - subMesh.firstVertex;
                            var third = indexReader.ReadUInt16() - subMesh.firstVertex;
                            var triangle = new[] { vertices[first], vertices[second], vertices[third] };
                            triangles.Add(triangle);
                        }
                    }
                }
            }
            return triangles.ToArray();
        }
    }
}