namespace MAAT.Application.Interfaces;

// docs/specs/rapport-pdf.md, section 7 : implémentation dans MAAT.Infrastructure (SkiaSharp),
// que cette couche ne connaît pas. Décode le fichier reçu, vérifie qu'il s'agit bien d'une
// image PNG, JPEG ou WebP de dimensions raisonnables, la réduit si besoin et la réencode en
// PNG. Lève InvalidLogoImageException sinon — jamais d'octets non décodés transmis au rapport.
public interface ILogoImageProcessor
{
    byte[] NormalizeToPng(byte[] uploaded);
}
