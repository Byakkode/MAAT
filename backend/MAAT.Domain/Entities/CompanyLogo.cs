namespace MAAT.Domain.Entities;

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise, posé sur la page de garde du
// rapport. Un par entreprise, supprimé avec elle. Table à part plutôt qu'une colonne de
// Company : chaque requête qui charge l'entreprise (inscription, rapport, export RGPD) n'a
// pas à ramener l'image avec elle.
// PngContent est toujours l'image déjà normalisée (décodée, redimensionnée, réencodée en
// PNG), jamais le fichier reçu tel quel : c'est ce qui garantit que le rapport ne dessine
// qu'une image valide, et qu'un même logo produit toujours les mêmes octets (section 3).
public class CompanyLogo
{
    public Guid CompanyId { get; private set; }
    public byte[] PngContent { get; private set; } = default!;
    public DateTimeOffset UpdatedAt { get; private set; }

    private CompanyLogo()
    {
    }

    public CompanyLogo(Guid companyId, byte[] pngContent, DateTimeOffset now)
    {
        CompanyId = companyId;
        Replace(pngContent, now);
    }

    public void Replace(byte[] pngContent, DateTimeOffset now)
    {
        PngContent = pngContent;
        UpdatedAt = now;
    }
}
