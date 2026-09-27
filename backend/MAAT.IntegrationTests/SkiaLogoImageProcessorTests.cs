using MAAT.Application.Exceptions;
using MAAT.Infrastructure.Pdf;
using SkiaSharp;

namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, section 7. Tests purs, sans base de données, comme
// RadarAxisLayoutTests : ce qui entre dans le rapport est toujours un PNG décodé et borné.
public class SkiaLogoImageProcessorTests
{
    private readonly SkiaLogoImageProcessor _processor = new();

    private static (SKEncodedImageFormat Format, int Width, int Height) Inspect(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return (codec.EncodedFormat, codec.Info.Width, codec.Info.Height);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Formats_acceptes_reencodes_en_png(SKEncodedImageFormat format)
    {
        var png = _processor.NormalizeToPng(CompanyLogoTests.MakeImage(300, 100, format));

        Assert.Equal((SKEncodedImageFormat.Png, 300, 100), Inspect(png));
    }

    [Fact]
    public void Image_plus_grande_que_600_pixels_reduite_en_gardant_ses_proportions()
    {
        var png = _processor.NormalizeToPng(CompanyLogoTests.MakeImage(900, 1800));

        Assert.Equal((SKEncodedImageFormat.Png, 300, 600), Inspect(png));
    }

    // Dimensions lues dans l'en-tête, avant tout décodage : l'image n'est jamais décompressée.
    [Fact]
    public void Image_de_plus_de_5000_pixels_de_cote_refusee()
    {
        var ex = Assert.Throws<InvalidLogoImageException>(() =>
            _processor.NormalizeToPng(CompanyLogoTests.MakeImage(SkiaLogoImageProcessor.MaxSourceSidePx + 1, 20)));

        Assert.Contains("trop grande", ex.Message);
    }

    [Fact]
    public void Image_minuscule_refusee()
    {
        Assert.Throws<InvalidLogoImageException>(() => _processor.NormalizeToPng(CompanyLogoTests.MakeImage(8, 8)));
    }

    [Fact]
    public void Contenu_qui_n_est_pas_une_image_refuse()
    {
        Assert.Throws<InvalidLogoImageException>(() => _processor.NormalizeToPng("<svg onload=alert(1)>"u8.ToArray()));
        Assert.Throws<InvalidLogoImageException>(() => _processor.NormalizeToPng([0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0]));
    }

    // GIF 1 × 1 valide, que Skia sait décoder : refusé pour son format, pas pour sa taille
    // (le format est vérifié en premier). Un GIF animé n'aurait pas de sens sur un PDF.
    private static readonly byte[] OnePixelGif =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B,
    ];

    [Fact]
    public void Format_decodable_mais_non_accepte_refuse()
    {
        Assert.Equal(SKEncodedImageFormat.Gif, Inspect(OnePixelGif).Format);

        var ex = Assert.Throws<InvalidLogoImageException>(() => _processor.NormalizeToPng(OnePixelGif));

        Assert.Contains("PNG, JPEG ou WebP", ex.Message);
    }

    // Même fichier, mêmes octets : c'est ce qui permet au rapport de rester déterministe
    // (section 3) sans jamais stocker le PDF.
    [Fact]
    public void Normalisation_deterministe()
    {
        var source = CompanyLogoTests.MakeImage(1200, 400, SKEncodedImageFormat.Jpeg);

        Assert.Equal(_processor.NormalizeToPng(source), _processor.NormalizeToPng(source));
    }
}
