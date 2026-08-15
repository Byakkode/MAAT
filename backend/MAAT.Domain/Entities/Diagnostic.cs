using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class Diagnostic
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; set; }
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.InProgress;
    public decimal? GlobalScore { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Décidé une fois, à la complétion (questionnaire.md, section 6, cas 13), depuis
    // SectorWeightLookup.UsedDefaultFallback — jamais dérivé de DomainScore.SectorWeight :
    // la renormalisation de scoring.md (cas 7) ramène le coefficient effectif à 1.00 quand un
    // seul domaine est actif, que la pondération d'origine soit spécifique ou par défaut, ce
    // qui rendrait les deux cas indiscernables après coup. Sans signification tant que Status
    // vaut InProgress.
    public bool DefaultSectorWeightingApplied { get; set; }

    private Diagnostic()
    {
    }

    public Diagnostic(Guid companyId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // Réservé à l'import et au jeu de données de démonstration (voir DemoDataSeeder) : un
    // diagnostic réellement créé via l'API passe toujours par le constructeur ci-dessus,
    // horodaté à l'instant présent.
    public Diagnostic(Guid companyId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        CreatedAt = createdAt;
    }
}
