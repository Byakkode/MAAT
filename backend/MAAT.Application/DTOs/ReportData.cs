using MAAT.Domain.Enums;

namespace MAAT.Application.DTOs;

// docs/specs/rapport-pdf.md, section 4. Tout ce dont IReportGenerator a besoin pour composer
// le document, assemblé par DiagnosticService.GenerateReportAsync depuis des données déjà
// persistées (aucune I/O, aucune horloge système côté générateur — section 3, déterminisme).
// GeneratedAt est une valeur transmise par l'appelant (TimeProvider injecté), jamais lue par
// le générateur lui-même.
public sealed record ReportData(
    string CompanyName,
    string SectorCode,
    CompanySizeRange SizeRange,
    string Region,
    DateTimeOffset CompletedAt,
    decimal GlobalScore,
    string GlobalScoreLabel,
    IReadOnlyList<ReportDomainScore> DomainScores,
    IReadOnlyList<ReportRecommendation> Recommendations,
    int TotalRecommendationCount,
    DateTimeOffset GeneratedAt,
    string ReferentialVersion);

// Numerator/Denominator : Σ(r×w) et Σ(w×5) du domaine, persistés dans DomainScore
// (modele-donnees.md) — jamais recalculés à la génération, pour la raison qui y est
// documentée. Contribution = Score × SectorWeight, affichée telle quelle dans le tableau de
// détail (section 4) : part du domaine dans le score global.
public sealed record ReportDomainScore(
    RseDomain Domain,
    decimal Score,
    decimal SectorWeight,
    decimal Numerator,
    decimal Denominator)
{
    public decimal Contribution => Score * SectorWeight;
}

// Limité aux vingt premières par priority_rank côté appelant (section 4) : cette liste ne
// porte que ce qui doit apparaître dans le document, TotalRecommendationCount ci-dessus porte
// le total réel.
public sealed record ReportRecommendation(
    string ActionText,
    RseDomain Domain,
    EffortLevel EffortLevel,
    bool IsCompleted);

// Retour de DiagnosticService.GenerateReportAsync : le document et le nom de fichier
// déterministe attendu par le Content-Disposition (rapport-pdf.md, section 2).
public sealed record GeneratedReport(byte[] Bytes, string FileName);
