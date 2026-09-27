using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using SkiaSharp;

namespace MAAT.Infrastructure.Pdf;

// docs/specs/rapport-pdf.md, section 7. Le fichier envoyé n'est jamais stocké tel quel : il
// est décodé, réduit si besoin, puis réencodé en PNG. Trois raisons, à pouvoir justifier :
// - seul un fichier qu'un décodeur d'image accepte réellement arrive jusqu'au rapport — le
//   type annoncé par le navigateur et l'extension du nom ne prouvent rien ;
// - le réencodage supprime les métadonnées embarquées (EXIF, coordonnées GPS d'une photo
//   prise au téléphone) avant qu'elles ne circulent dans un document envoyé à des tiers ;
// - une image bornée à MaxStoredSidePx garde le PDF léger, quelle que soit la source.
public sealed class SkiaLogoImageProcessor : ILogoImageProcessor
{
    // Lu dans l'en-tête avant tout décodage : une image de 30 000 × 30 000 px tient en
    // quelques Ko compressés, mais occuperait plusieurs Go de mémoire une fois décodée.
    internal const int MaxSourceSidePx = 5000;

    internal const int MinSourceSidePx = 16;

    // Le logo s'affiche au plus sur 150 × 60 pt en page de garde : 600 px en largeur, c'est
    // quatre fois la taille d'affichage, au-delà des 300 dpi d'impression (section 5).
    internal const int MaxStoredSidePx = 600;

    private static readonly SKEncodedImageFormat[] AcceptedFormats =
        [SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Webp];

    private const string UnsupportedMessage = "Format non reconnu : envoyez une image PNG, JPEG ou WebP.";

    public byte[] NormalizeToPng(byte[] uploaded)
    {
        using var data = SKData.CreateCopy(uploaded);
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidLogoImageException(UnsupportedMessage);

        if (!AcceptedFormats.Contains(codec.EncodedFormat))
        {
            throw new InvalidLogoImageException(UnsupportedMessage);
        }

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width > MaxSourceSidePx || height > MaxSourceSidePx)
        {
            throw new InvalidLogoImageException($"Image trop grande : {MaxSourceSidePx} pixels de côté au maximum.");
        }

        if (width < MinSourceSidePx || height < MinSourceSidePx)
        {
            throw new InvalidLogoImageException($"Image trop petite : {MinSourceSidePx} pixels de côté au minimum.");
        }

        using var decoded = SKBitmap.Decode(codec, codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul))
            ?? throw new InvalidLogoImageException("L'image n'a pas pu être lue : le fichier est peut-être endommagé.");

        using var resized = ResizeToFit(decoded);
        using var image = SKImage.FromBitmap(resized ?? decoded);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    // null : l'image tient déjà dans MaxStoredSidePx, elle est gardée à sa taille.
    private static SKBitmap? ResizeToFit(SKBitmap source)
    {
        var longestSide = Math.Max(source.Width, source.Height);
        if (longestSide <= MaxStoredSidePx)
        {
            return null;
        }

        var scale = (double)MaxStoredSidePx / longestSide;
        var target = source.Info.WithSize(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));

        return source.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidLogoImageException("L'image n'a pas pu être redimensionnée.");
    }
}
