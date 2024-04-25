using System;
using System.Collections.Generic;
using System.Linq;
using Helion.Geometry;
using Helion.Geometry.Vectors;
using Helion.Render.Common;
using Helion.Resources;
using Helion.Resources.Archives.Collection;
using Helion.Resources.Definitions.Fonts.Definition;
using Helion.Resources.Images;
using Helion.Util.Extensions;
using static Helion.Util.Assertion.Assert;

namespace Helion.Graphics.Fonts;

public static class BitmapFont
{
    /// <summary>
    /// Reads a bitmap font.
    /// </summary>
    /// <param name="definition">The font definition.</param>
    /// <param name="archiveCollection">The source of the images.</param>
    /// <returns>The font, or null if it cannot be made.</returns>
    public static Font? From(FontDefinition definition, ArchiveCollection archiveCollection)
    {
        if (!definition.IsValid())
            return null;

        try
        {
            Dictionary<char, Image> charImages = GetCharacterImages(definition, archiveCollection,
                out int maxHeight, out ImageType imageType);

            if (charImages.Empty())
                return null;

            AddSpaceGlyphIfMissing(charImages, definition, maxHeight, imageType);
            var (glyphs, image) = CreateGlyphs(definition, charImages, maxHeight, imageType);
            
            if (definition.Grayscale)
                image.ConvertToGrayscale(definition.GrayscaleNormalization);
            
            return new Font(definition.Name, glyphs, image, fixedHeight: definition.FixedHeight);
        }
        catch
        {
            return null;
        }
    }

    private static int CalculateMaxHeight(Dictionary<char, Image> charImages)
    {
        return charImages.Values.Select(i => i.Height).Max();
    }

    private static bool NotAllSameImageType(ImageType type, Dictionary<char, Image> charImages)
    {
        return charImages.Values.Any(i => i.ImageType != type);
    }

    private static void AddSpaceGlyphIfMissing(Dictionary<char, Image> charImages, FontDefinition definition,
        int maxHeight, ImageType imageType)
    {
        if (charImages.ContainsKey(' '))
            return;

        Precondition(definition.SpaceWidth != null, "Invalid definition detected, has no space image nor spacing attribute");
        int width = definition.SpaceWidth ?? 1;
        int height = definition.FixedHeight ?? maxHeight;

        charImages[' '] = new Image(width, height, imageType);
    }

    private static Dictionary<char, Image> GetCharacterImages(FontDefinition definition,
        ArchiveCollection archiveCollection, out int maxHeight, out ImageType imageType)
    {
        imageType = ImageType.Argb;
        maxHeight = 0;

        Dictionary<char, Image> charImages = new();

        // TODO: TEMPORARY: The texture manager should do all of this for us later on!
        IImageRetriever imageRetriever = new ArchiveImageRetriever(archiveCollection);

        // Unfortunately we need to know the max height, and require all of
        // the images beforehand to make such a calculation.
        foreach ((char c, CharDefinition charDef) in definition.CharDefinitions)
        {
            Image? image = imageRetriever.Get(charDef.ImageName, ResourceNamespace.Graphics);
            if (image != null)
                charImages[c] = image;
        }

        if (charImages.Empty())
            return new Dictionary<char, Image>();

        maxHeight = CalculateMaxHeight(charImages);

        imageType = charImages.Values.First().ImageType;
        if (NotAllSameImageType(imageType, charImages))
            throw new Exception("Mixing different image types when making bitmap font");

        Dictionary<char, Image> processedCharImages = new();
        foreach ((char c, Image charImage) in charImages)
        {
            FontAlignment alignment = definition.CharDefinitions[c].Alignment ?? definition.Alignment;
            processedCharImages[c] = CreateCharImage(charImage, maxHeight, alignment, imageType, definition.FixedHeight);
        }

        return processedCharImages;
    }

    private static (Dictionary<char, Glyph>, Image) CreateGlyphs(FontDefinition definition, Dictionary<char, Image> charImages,
        int maxHeight, ImageType imageType)
    {
        Dictionary<char, Glyph> glyphs = new();
        int offsetX = 0;
        const int padding = 1;
        int width = charImages.Values.Select(i => i.Width).Sum() + padding * charImages.Count * 2;
        if (definition.FixedWidth != null)
            width = (charImages.Count * definition.FixedWidth.Value) + (padding * charImages.Count * 2);

        if (definition.FixedHeight != null)
            maxHeight = definition.FixedHeight.Value;

        Dimension atlasDimension = (width, maxHeight);
        Image atlas = new(width, maxHeight, imageType);

        foreach ((char c, Image charImage) in charImages)
        {
            offsetX += padding;

            int charWidth = definition.FixedWidth.HasValue ? definition.FixedWidth.Value : charImage.Width;
            var offset = definition.UseOffset ? RenderDimensions.TranslateDoomOffset(charImage.Offset) : Vec2I.Zero;
            offset.X += offsetX;

            charImage.DrawOnTopOf(atlas, offset);
            var glyphDimension = charImage.Dimension;
            glyphDimension.Width = charWidth;
            glyphs[c] = new Glyph(c, (offsetX, 0), glyphDimension, atlasDimension);

            offsetX += charWidth + padding;
        }

        return (glyphs, atlas);
    }

    private static Image CreateCharImage(Image image, int maxHeight, FontAlignment alignment,
        ImageType imageType, int? fixedHeight)
    {
        Precondition(maxHeight >= image.Height, "Miscalculated max height when making font");

        if (fixedHeight.HasValue)
            maxHeight = fixedHeight.Value;

        if (image.Height == maxHeight)
            return image;

        int startY = 0;
        switch (alignment)
        {
            case FontAlignment.Top:
                // We're done, the default value is correct already.
                break;
            case FontAlignment.Center:
                startY = (maxHeight / 2) - (image.Height / 2);
                break;
            case FontAlignment.Bottom:
                startY = maxHeight - image.Height;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unexpected font alignment in glyph creation");
        }

        Image glyphImage = new(image.Width, maxHeight, imageType, offset: image.Offset);
        image.DrawOnTopOf(glyphImage, (0, startY));

        return glyphImage;
    }
}
