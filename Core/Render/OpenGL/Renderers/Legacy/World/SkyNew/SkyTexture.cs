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