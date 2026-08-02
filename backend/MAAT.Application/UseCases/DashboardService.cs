using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/dashboard.md. Lecture seule (contrairement à DiagnosticService) : aucune
// transaction, chaque composante de l'écran vient d'un dépôt déjà scopé par entreprise
// (voir chaque interface), sauf le benchmark sectoriel — seul point de comparaison
// inter-entreprises du produit, voir ISectorBenchmarkRepository.
public class DashboardService(
    IDiagnosticRepository diagnosticRepository,
    IDomainScoreRepository domainScoreRepository,
    IDiagnosticRecommendationRepository diagnosticRecommendationRepository,
    ICompanyRepository companyRepository,
    ISectorBenchmarkRepository sectorBenchmarkRepository,
    IResponseRepository responseRepository,
    IQuestionRepository questionRepository,
    ICurrentUserContext currentUser)
{
    // Seuil d'anonymat RGPD (section 5) : en-deçà, les scores redeviennent
    // ré-identifiables par recoupement.
    private const int MinimumBenchmarkSampleSize = 5;

    private const int ActionPlanItemsShown = 5;

    public async Task<DashboardView> GetAsync(CancellationToken ct)
    {
        // Triés du plus ancien au plus récent (section 4) : le dernier sert de référence pour
        // le score courant, les cinq DomainScore, le benchmark et le plan d'actions (cas 4).
        var completed = await diagnosticRepository.FindAllCompletedForCurrentCompanyAsync(ct);
        var latest = completed.Count > 0 ? completed[^1] : null;

        LatestDiagnosticView? latestView = null;
        IReadOnlyList<DomainScoreView> domainScores = [];
        SectorBenchmarkView? benchmark = null;
        var actionPlan = new ActionPlanView([], 0, 0);

        if (latest is not null)
        {
            var company = await companyRepository.GetByIdAsync(currentUser.CompanyId, ct)
                ?? throw new InvalidOperationException("Entreprise du principal authentifié introuvable.");

            latestView = new LatestDiagnosticView(latest.Id, latest.GlobalScore!.Value, latest.CompletedAt!.Value, company.SectorCode);

            // Chargée avant les DomainScore ci-dessous : le radar (section 3) a besoin, par
            // domaine, du nombre de recommandations déclenchées sur l'ensemble du plan — pas
            // seulement les cinq premières que ActionPlanView.Items retiendra plus bas.
            var recommendations = await diagnosticRecommendationRepository.FindAllForDiagnosticAsync(latest.Id, ct);
            var triggeredCountByDomain = recommendations.GroupBy(r => r.Domain).ToDictionary(g => g.Key, g => g.Count());

            var scores = await domainScoreRepository.FindAllForDiagnosticAsync(latest.Id, ct);
            domainScores = scores
                .Select(s => new DomainScoreView(s.Domain, s.Score, s.SectorWeight, triggeredCountByDomain.GetValueOrDefault(s.Domain)))
                .ToList();

            benchmark = await BuildBenchmarkAsync(company.SectorCode, latest.GlobalScore!.Value, ct);

            actionPlan = new ActionPlanView(
                recommendations.Take(ActionPlanItemsShown).ToList(),
                recommendations.Count,
                recommendations.Count(r => r.IsCompleted));
        }

        var inProgressView = await BuildInProgressViewAsync(ct);

        return new DashboardView(
            HasCompletedDiagnostic: latest is not null,
            latestView,
            domainScores,
            BuildHistory(completed),
            benchmark,
            actionPlan,
            inProgressView);
    }

    private static IReadOnlyList<DiagnosticHistoryPoint> BuildHistory(IReadOnlyList<Diagnostic> completedAscending)
    {
        var points = new List<DiagnosticHistoryPoint>(completedAscending.Count);
        decimal? previousScore = null;

        foreach (var diagnostic in completedAscending)
        {
            var score = diagnostic.GlobalScore!.Value;
            points.Add(new DiagnosticHistoryPoint(diagnostic.CompletedAt!.Value, score, previousScore is null ? null : score - previousScore));
            previousScore = score;
        }

        return points;
    }

    private async Task<SectorBenchmarkView> BuildBenchmarkAsync(string sectorCode, decimal ownScore, CancellationToken ct)
    {
        // Un score par entreprise du secteur, y compris la nôtre (voir
        // ISectorBenchmarkRepository) : jamais d'identifiant, de nom ni de diagnostic
        // individuel d'une autre entreprise dans ce que ce dépôt retourne (cas 11).
        var scores = await sectorBenchmarkRepository.FindLatestCompletedScoresBySectorAsync(sectorCode, ct);
        var sampleSize = scores.Count;

        if (sampleSize < MinimumBenchmarkSampleSize)
        {
            return new SectorBenchmarkView(
                Available: false,
                sampleSize,
                Percentile: null,
                Reason: $"La comparaison sectorielle sera disponible à partir de {MinimumBenchmarkSampleSize} entreprises de "
                    + $"votre secteur ayant réalisé un diagnostic (actuellement {sampleSize}).");
        }

        // Comparaison contre les autres entreprises du secteur (sampleSize - 1) : la nôtre
        // fait déjà partie de l'échantillon par construction, se comparer à soi-même n'a pas
        // de sens (docs/specs/dashboard.md, section 5).
        var scoredBelow = scores.Count(s => s < ownScore);
        var percentile = ScoringService.RoundForDisplay(100m * scoredBelow / (sampleSize - 1));

        return new SectorBenchmarkView(Available: true, sampleSize, percentile, Reason: null);
    }

    private async Task<InProgressDiagnosticView?> BuildInProgressViewAsync(CancellationToken ct)
    {
        var inProgress = await diagnosticRepository.FindInProgressForCurrentCompanyAsync(ct);
        if (inProgress is null)
        {
            return null;
        }

        var answeredCount = (await responseRepository.FindAllForDiagnosticAsync(inProgress.Id, ct)).Count;
        var totalActiveQuestions = (await questionRepository.FindAllActiveAsync(ct)).Count;

        return new InProgressDiagnosticView(inProgress.Id, answeredCount, totalActiveQuestions);
    }
}
