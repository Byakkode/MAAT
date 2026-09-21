using MAAT.Domain.Services;

namespace MAAT.Domain.Tests;

public class NafSectionResolverTests
{
    // Cas nominaux — un représentant par section
    [Theory]
    [InlineData("0111Z", "A")]   // Agriculture
    [InlineData("0311Z", "A")]   // Aquaculture
    [InlineData("0510Z", "B")]   // Extraction houille
    [InlineData("0990Z", "B")]   // Autres mines
    [InlineData("1011Z", "C")]   // Abattoir
    [InlineData("3320Z", "C")]   // Installation machines industrielles
    [InlineData("3511Z", "D")]   // Production électricité
    [InlineData("3600Z", "E")]   // Captage eau
    [InlineData("3900Z", "E")]   // Dépollution
    [InlineData("4110A", "F")]   // Promotion immobilière logements
    [InlineData("4321A", "F")]   // Travaux d'installation électrique
    [InlineData("4511Z", "G")]   // Commerce voitures
    [InlineData("4719B", "G")]   // Commerce de détail
    [InlineData("4941A", "H")]   // Transport routier de fret
    [InlineData("5310Z", "H")]   // Activités de poste
    [InlineData("5510Z", "I")]   // Hôtels et hébergement similaire
    [InlineData("5610A", "I")]   // Restauration traditionnelle
    [InlineData("5811Z", "J")]   // Édition livres
    [InlineData("6201Z", "J")]   // Programmation informatique
    [InlineData("6209Z", "J")]   // Autres services informatiques
    [InlineData("6411Z", "K")]   // Banque centrale
    [InlineData("6612Z", "K")]   // Courtage valeurs mobilières
    [InlineData("6810Z", "L")]   // Marchands de biens immobiliers
    [InlineData("6910Z", "M")]   // Activités juridiques
    [InlineData("7022Z", "M")]   // Conseil en gestion
    [InlineData("7500Z", "M")]   // Activités vétérinaires
    [InlineData("7711A", "N")]   // Location voitures
    [InlineData("8219Z", "N")]   // Photocopie, frappe, activités de soutien
    [InlineData("8411Z", "O")]   // Administration publique générale
    [InlineData("8531Z", "P")]   // Enseignement secondaire général
    [InlineData("8610Z", "Q")]   // Activités hospitalières
    [InlineData("8810A", "Q")]   // Aide à domicile
    [InlineData("9001Z", "R")]   // Arts du spectacle vivant
    [InlineData("9329Z", "R")]   // Autres activités récréatives
    [InlineData("9411Z", "S")]   // Activités syndicats patronaux
    [InlineData("9601A", "S")]   // Blanchisserie-teinturerie
    public void GetSection_CodeValide_RetourneLaSectionAttendue(string code, string expectedSection)
    {
        Assert.Equal(expectedSection, NafSectionResolver.GetSection(code));
    }

    // Sections T et U hors scope PME : pas de mapping → repli sur défaut global
    [Theory]
    [InlineData("9700Z")] // Ménages employeurs (T)
    [InlineData("9900Z")] // Organisations extra-territoriales (U)
    [InlineData("9999Z")] // Code fictif hors plage (U)
    public void GetSection_SectionHorsScope_RetourneNull(string code)
    {
        Assert.Null(NafSectionResolver.GetSection(code));
    }

    // Cas limites
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]          // Code court non numérique
    [InlineData("XY12Z")]      // Préfixe non numérique
    [InlineData("0000Z")]      // Division 00 — n'existe pas dans la NAF
    public void GetSection_CodeInvalideOuNull_RetourneNull(string? code)
    {
        Assert.Null(NafSectionResolver.GetSection(code));
    }
}
