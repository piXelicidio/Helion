using System;
using Helion.Geometry.Vectors;
using Helion.Graphics;
using Helion.Render.OpenGL.Texture.Legacy;
using Helion.Render.OpenGL.Util;
using Helion.Resources;
using OpenTK.Graphics.OpenGL;
using ResourceTexture = Helion.Resources.Texture;

namespace Helion.Render.OpenGL.Renderers.Legacy.World.SkyNew;

public class SkyTexture : IDisposable
{
    public readonly GLLegacyTexture Texture;
    public readonly Color UpperFade;
    public readonly Color LowerFade;
    public readonly float ScaleU;
    private readonly int m_textureIdx;
    private readonly string m_textureName;
    private bool m_disposed;

    public SkyTexture(ResourceTexture texture)
    {
        if (texture.Image == null)
            throw new($"Trying to create a sky texture from a null image {texture.Name}");

        m_textureIdx = texture.Index;
        m_textureName = texture.Name;
        Texture = CreateTexture(texture, texture.Image);
        ScaleU = CalculateScaleU(texture.Image.Width);
        UpperFade = CalculateUpperFade(texture.Image);
        LowerFade = CalculateLowerFade(texture.Image);
    }

    ~SkyTexture()
    {
        PerformDispose();
    }

    private static unsafe GLLegacyTexture CreateTexture(ResourceTexture texture, Image image)
    {
        int textureId = GL.GenTexture();
        GLLegacyTexture glTexture = new(textureId, texture.Name, image.Dimension, Vec2I.Zero, 
            ResourceNamespace.Textures, TextureTarget.Texture2D, texture.Image.TransparentPixelCount());
        
        glTexture.Bind();
        GLHelper.ObjectLabel(ObjectLabelIdentifier.Texture, textureId, $"[Texture] Sky: {texture.Name}");

        fixed (uint* pixelPtr = image.Pixels)
        {
            IntPtr ptr = new(pixelPtr);
            // Because the C# image format is 'ARGB', we can get it into the
            // RGBA format by doing a BGRA format and then reversing it.
            GL.TexImage2D(glTexture.Target, 0, PixelInternalFormat.Rgba8, image.Width, image.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedInt8888Reversed, ptr);
        }

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        
        glTexture.Unbind();

        return glTexture;
    }

    private static float CalculateScaleU(int imageWidth)
    {
        // If the texture is huge, we'll just assume the user wants a one-
        // to-one scaling. See the bottom return comment on why this is
        // negative.
        if (imageWidth >= 1024)
            return -1.0f;

        // We want to fit either 4 '256 width textures' onto the sphere
        // or 1 '1024 width texture' onto the same area. While we're at
        // it, we can just make it so that the texture scales around to
        // it's nearest power of two.
        //
        // To do so, first find out X when we have width = 2^X. We need
        // to force this to be a whole number so we round. This is likely
        // not correct due to how a value at 0.5 won't do what we think,
        // but we'll deal with this later if the need ever arises.
        double roundedExponent = Math.Round(Math.Log(imageWidth, 2));

        // We want to fit it onto a sky that is 1024 in width. We can now
        // do `1024 / width` where width is a power of two. We can find out
        // the scaling factor with the following rearrangement:
        //      f = 1024 / width
        //        = 2^10 / 2^x       [because x is a whole number now]
        //        = 2^(10 - x)
        float scalingFactor = (float)Math.Pow(2, 10 - roundedExponent);

        // We make the scale negative so that the U coordinate is reversed.
        // The sphere is made in a counter-clockwise direction but drawing
        // the texture in other ports appears visually to be clockwise. By
        // setting the U scaling to be negative, the shader will reverse
        // the direction of the texturing (which is what we want).
        return -scalingFactor;
    }

    private static Color CalculateUpperFade(Image image)
    {
        (int sumR, int sumG, int sumB) = (0, 0, 0);

        // Sum everything up before dividing. Use the top 8 rows to decide the color.
        int maxRows = Math.Min(image.Height, 8);
        for (int y = 0; y < maxRows; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                sumR += pixel.R;
                sumG += pixel.G;
                sumB += pixel.B;
            }
        }
        
        int pixelCount = maxRows * image.Width;
        int avgR = sumR / pixelCount;
        int avgG = sumG / pixelCount;
        int avgB = sumB / pixelCount;
        
        return new((byte)avgR, (byte)avgG, (byte)avgB);
    }

    private static Color CalculateLowerFade(Image image)
    {
        (int sumR, int sumG, int sumB) = (0, 0, 0);

        // Sum everything up before dividing. Use the bottom 8 rows to decide the color.
        int maxRows = Math.Min(image.Height, 8);
        for (int iterY = 0; iterY < maxRows; iterY++)
        {
            int y = image.Height - 1 - iterY;
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                sumR += pixel.R;
                sumG += pixel.G;
                sumB += pixel.B;
            }
        }
        
        int pixelCount = maxRows * image.Width;
        int avgR = sumR / pixelCount;
        int avgG = sumG / pixelCount;
        int avgB = sumB / pixelCount;
        
        return new((byte)avgR, (byte)avgG, (byte)avgB);
    }

    private void PerformDispose()
    {
        if (m_disposed)
            return;
        
        Texture.Dispose();

        m_disposed = true;
    }

    public void Dispose()
    {
        PerformDispose();
        GC.SuppressFinalize(this);
    }
    
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    public override int GetHashCode() => m_textureIdx;
    public override string ToString() => $"{m_textureName} ({m_textureIdx})";
}