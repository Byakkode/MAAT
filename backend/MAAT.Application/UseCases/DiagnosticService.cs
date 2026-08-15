using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

public class DiagnosticService(
    IDiagnosticRepository diagnosticRepository,
    IResponseRepository responseRepository,
    IDomainScoreRepository domainScoreRepository,
    IReportRepository reportRepository,
    IQuestionRepository questionRepository,
    ISectorWeightRepository sectorWeightRepository,
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    ICurrentUserContext currentUser,
    IUnitOfWork unitOfWork,
    IScoringService scoringService,
    IRecommendationRepository recommendationRepository,
    IDiagnosticRecommendationRepository diagnosticRecommendationRepository,
    IRecommendationEngine recommendationEngine,
    IReportGenerator reportGenerator,
    TimeProvider timeProvider)
{
    // docs/specs/rapport-pdf.md, section 4, bloc « Mentions » : version du référentiel de
    // questions actif. Le modèle de données n'a pas (encore) de colonne de version sur
    // Question — constante ici plutôt qu'une donnée absente, à faire évoluer le jour où un
    // véritable versionnement du référentiel existe.
    private const string ReferentialVersion = "1.0";

    // Limite du plan d'actions affiché dans le rapport (section 4 : « limiter aux vingt
    // premières et indiquer le total »).
    private const int MaxReportRecommendations = 20;
    // docs/specs/questionnaire.md, section 1 : une entreprise ne peut avoir qu'un seul
    // diagnostic InProgress à la fois — sans quoi la reprise devient ambiguë.
    public async Task<Diagnostic> CreateAsync(CancellationToken ct)
    {
        var existing = await diagnosticRepository.FindInProgressForCurrentCompanyAsync(ct);
        if (existing is not null)
        {
            throw new DiagnosticAlreadyInProgressException(existing.Id);
        }

        var diagnostic = await diagnosticRepository.CreateAsync(ct);
        await unitOfWork.SaveChangesAsync(ct);
        return diagnostic;
    }

    public Task<Diagnostic?> GetByIdAsync(Guid diagnosticId, CancellationToken ct) =>
        diagnosticRepository.FindByIdAsync(diagnosticId, ct);

    // section 5 : le diagnostic InProgress de l'entreprise courante, ou null (404).
    public Task<Diagnostic?> GetCurrentInProgressAsync(CancellationToken ct) =>
        diagnosticRepository.FindInProgressForCurrentCompanyAsync(ct);

    public async Task<int> CountAnsweredQuestionsAsync(Guid diagnosticId, CancellationToken ct) =>
        (await responseRepository.FindAllForDiagnosticAsync(diagnosticId, ct)).Count;

    public async Task<int> CountActiveQuestionsAsync(CancellationToken ct) =>
        (await questionRepository.FindAllActiveAsync(ct)).Count;

    // section 1 : fait passer un diagnostic InProgress en Archived. Retourne null si le
    // diagnostic n'existe pas ou n'appartient pas à l'entreprise courante (404) ; lève si
    // le diagnostic existe mais n'est pas InProgress (409).
    public async Task<Diagnostic?> AbandonAsync(Guid diagnosticId, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        if (diagnostic.Status != DiagnosticStatus.InProgress)
        {
            throw new DiagnosticNotInProgressException();
        }

        diagnostic.Status = DiagnosticStatus.Archived;
        await unitOfWork.SaveChangesAsync(ct);
        return diagnostic;
    }

    public Task<Response?> GetResponseByIdAsync(Guid responseId, CancellationToken ct) =>
        responseRepository.FindByIdAsync(responseId, ct);

    // section 4 : upsert sur (diagnostic_id, question_id), jamais un second insert. Lève
    // si le diagnostic n'est pas InProgress (409), ou si la question n'existe pas / n'est
    // pas active (400) ; ArgumentOutOfRangeException (400) si la valeur est hors 0-5,
    // levée par Response elle-même.
    public async Task<(Response Response, bool Created)?> UpsertResponseAsync(
        Guid diagnosticId, string questionCode, int value, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        if (diagnostic.Status != DiagnosticStatus.InProgress)
        {
            throw new DiagnosticNotInProgressException();
        }

        var question = await questionRepository.FindByCodeAsync(questionCode, ct);
        if (question is null || !question.IsActive)
        {
            throw new QuestionNotAvailableException(questionCode);
        }

        var existing = await responseRepository.FindByDiagnosticAndQuestionAsync(diagnosticId, question.Id, ct);

        Response response;
        bool created;
        if (existing is not null)
        {
            existing.UpdateValue(value);
            response = existing;
            created = false;
        }
        else
        {
            response = new Response(diagnosticId, question.Id, value);
            await responseRepository.AddAsync(response, ct);
            created = true;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return (response, created);
    }

    // section 6 : préconditions (InProgress, toutes questions actives répondues), puis
    // calcul de score, écriture des cinq DomainScore et passage à Completed, le tout dans
    // une seule transaction — un échec du calcul (pondération manquante, données
    // incohérentes) ne doit laisser aucune trace partielle (cas 15, le plus important).
    public async Task<Diagnostic?> CompleteAsync(Guid diagnosticId, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        if (diagnostic.Status != DiagnosticStatus.InProgress)
        {
            throw new DiagnosticNotInProgressException();
        }

        var activeQuestions = await questionRepository.FindAllActiveAsync(ct);
        var responses = await responseRepository.FindAllForDiagnosticAsync(diagnosticId, ct);
        var valueByQuestionId = responses.ToDictionary(r => r.QuestionId, r => r.Value);

        var missingCodes = activeQuestions
            .Where(q => !valueByQuestionId.ContainsKey(q.Id))
            .Select(q => q.Code)
            .ToList();

        if (missingCodes.Count > 0)
        {
            throw new IncompleteQuestionnaireException(missingCodes);
        }

        var company = await companyRepository.GetByIdAsync(currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException("Entreprise du principal authentifié introuvable.");

        await unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var sectorWeightLookup = await sectorWeightRepository.GetForSectorOrDefaultAsync(company.SectorCode, innerCt);

            var inputs = activeQuestions
                .Select(q => new QuestionScoreInput(q.Domain, q.Weight, valueByQuestionId[q.Id]))
                .ToList();

            // Peut lever (pondération manquante pour un domaine, données incohérentes) :
            // la transaction englobante annule alors tout ce qui suit (cas 15).
            var result = scoringService.CalculateScore(inputs, sectorWeightLookup.Weights);

            foreach (var detail in result.DomainScores)
            {
                await domainScoreRepository.AddAsync(
                    new DomainScore(diagnosticId, detail.Domain, detail.Score, detail.EffectiveSectorWeight, detail.Numerator, detail.Denominator), innerCt);
            }

            diagnostic.GlobalScore = result.GlobalScore;
            diagnostic.Status = DiagnosticStatus.Completed;
            diagnostic.CompletedAt = DateTimeOffset.UtcNow;
            diagnostic.DefaultSectorWeightingApplied = sectorWeightLookup.UsedDefaultFallback;

            // docs/specs/recommandations.md, section 1 : étape 5 de la complétion, dans la
            // même transaction. Peut lever (question déclencheuse sans réponse, domaine sans
            // pondération effective) : la transaction englobante annule alors tout ce qui
            // précède, y compris les DomainScore déjà ajoutés ci-dessus (cas 12).
            var activeRecommendations = await recommendationRepository.FindAllActiveAsync(innerCt);
            var responseValueByQuestionCode = activeQuestions.ToDictionary(q => q.Code, q => valueByQuestionId[q.Id]);
            var triggered = recommendationEngine.SelectTriggered(activeRecommendations, responseValueByQuestionCode);

            // section 2 : la pondération effectivement appliquée, celle qu'on vient d'écrire
            // dans DomainScore ci-dessus — jamais une relecture de SectorWeight (cas 11).
            var effectiveWeightByDomain = result.DomainScores.ToDictionary(d => d.Domain, d => d.EffectiveSectorWeight);
            var prioritized = recommendationEngine.Prioritize(triggered, effectiveWeightByDomain);

            var diagnosticRecommendations = prioritized
                .Select(p => new DiagnosticRecommendation(diagnosticId, p.Recommendation.Id, p.PriorityRank))
                .ToList();
            await diagnosticRecommendationRepository.AddRangeAsync(diagnosticRecommendations, innerCt);

            await unitOfWork.SaveChangesAsync(innerCt);
        }, ct);

        return diagnostic;
    }

    // docs/specs/questionnaire.md, section 2 (GET /api/diagnostics/{id}/questions), cas
    // 21 à 24 : lecture seule, accessible aux trois rôles, y compris sur un diagnostic
    // Completed — aucune vérification de Status ici, contrairement à UpsertResponseAsync
    // et CompleteAsync qui l'exigent InProgress. FindByIdAsync scope déjà par entreprise
    // courante (retourne null → 404 si le diagnostic appartient à une autre entreprise).
    public async Task<IReadOnlyList<QuestionAnswerView>?> GetQuestionsWithAnswersAsync(Guid diagnosticId, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        var activeQuestions = await questionRepository.FindAllActiveAsync(ct);
        var responses = await responseRepository.FindAllForDiagnosticAsync(diagnosticId, ct);
        var valueByQuestionId = responses.ToDictionary(r => r.QuestionId, r => r.Value);

        return activeQuestions
            .OrderBy(q => q.Domain)
            .ThenBy(q => q.DisplayOrder)
            .Select(q => new QuestionAnswerView(
                q.Code,
                q.Text,
                q.HelpText,
                q.Domain,
                q.DisplayOrder,
                valueByQuestionId.TryGetValue(q.Id, out var value) ? value : null))
            .ToList();
    }

    public Task<DomainScore?> GetDomainScoreAsync(Guid diagnosticId, RseDomain domain, CancellationToken ct) =>
        domainScoreRepository.FindAsync(diagnosticId, domain, ct);

    public Task<Report?> GetReportByIdAsync(Guid reportId, CancellationToken ct) =>
        reportRepository.FindByIdAsync(reportId, ct);

    // docs/specs/recommandations.md, section 4 : lecture seule, accessible aux trois rôles,
    // y compris sur un diagnostic Completed — aucune vérification de Status ici, comme
    // GetQuestionsWithAnswersAsync. Null si le diagnostic n'existe pas / n'appartient pas à
    // l'entreprise courante (404) ; liste vide un résultat valide (aucun déclenchement,
    // cas 5).
    public async Task<IReadOnlyList<DiagnosticRecommendationView>?> GetRecommendationsAsync(Guid diagnosticId, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        return await diagnosticRecommendationRepository.FindAllForDiagnosticAsync(diagnosticId, ct);
    }

    // section 5 : bascule is_completed / completed_at. Autorisée même sur un diagnostic
    // Completed — c'est le cas normal, seule exception à l'immuabilité posée par
    // questionnaire.md section 1 (on modifie le suivi, jamais les réponses ni le score,
    // cas 20). Null si le diagnostic n'existe pas, si le code de recommandation n'existe
    // pas, ou s'il n'a jamais été déclenché pour ce diagnostic — 404 dans les trois cas.
    public async Task<DiagnosticRecommendation?> UpdateRecommendationProgressAsync(
        Guid diagnosticId, string recommendationCode, bool isCompleted, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        var recommendation = await recommendationRepository.FindByCodeAsync(recommendationCode, ct);
        if (recommendation is null)
        {
            return null;
        }

        var entry = await diagnosticRecommendationRepository.FindAsync(diagnosticId, recommendation.Id, ct);
        if (entry is null)
        {
            return null;
        }

        entry.SetProgress(isCompleted);
        await unitOfWork.SaveChangesAsync(ct);
        return entry;
    }

    // docs/specs/rapport-pdf.md, section 2. Préconditions dans cet ordre : existence/
    // cloisonnement (404, FindByIdAsync scope déjà par entreprise courante), statut (409),
    // adresse e-mail vérifiée (403, auth-securite-rgpd.md section 1 — restriction ciblée sur
    // la seule génération de rapport). Aucune ligne Report n'est écrite avant que
    // reportGenerator.Generate ait réussi (section 2 : « une génération qui échoue ne doit
    // pas laisser de trace d'un rapport qui n'a jamais existé »).
    public async Task<GeneratedReport?> GenerateReportAsync(Guid diagnosticId, CancellationToken ct)
    {
        var diagnostic = await diagnosticRepository.FindByIdAsync(diagnosticId, ct);
        if (diagnostic is null)
        {
            return null;
        }

        if (diagnostic.Status != DiagnosticStatus.Completed)
        {
            throw new DiagnosticNotCompletedException();
        }

        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new InvalidOperationException("Utilisateur du principal authentifié introuvable.");
        if (!user.EmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        var company = await companyRepository.GetByIdAsync(diagnostic.CompanyId, ct)
            ?? throw new InvalidOperationException("Entreprise du diagnostic introuvable.");

        var globalScore = diagnostic.GlobalScore
            ?? throw new InvalidOperationException("Un diagnostic Completed doit porter un score global.");
        var completedAt = diagnostic.CompletedAt
            ?? throw new InvalidOperationException("Un diagnostic Completed doit porter une date de complétion.");

        // Tri par domaine (ordre de l'énumération RseDomain) plutôt que l'ordre de retour du
        // dépôt : déterminisme du cas 10/11 — deux générations doivent produire des blocs
        // identiques indépendamment de tout ordre de lecture non garanti côté base.
        var domainScores = await domainScoreRepository.FindAllForDiagnosticAsync(diagnosticId, ct);
        var reportDomainScores = domainScores
            .OrderBy(ds => (int)ds.Domain)
            .Select(ds => new ReportDomainScore(ds.Domain, ds.Score, ds.SectorWeight, ds.Numerator, ds.Denominator))
            .ToList();

        // Déjà triées par priority_rank par le dépôt (section 4 : jamais un recalcul, cas 12
        // de recommandations.md) ; is_active ignoré, comme pour la consultation du plan
        // d'actions (une recommandation désactivée après coup reste dans le rapport).
        var allRecommendations = await diagnosticRecommendationRepository.FindAllForDiagnosticAsync(diagnosticId, ct);
        var reportRecommendations = allRecommendations
            .Take(MaxReportRecommendations)
            .Select(r => new ReportRecommendation(r.ActionText, r.Domain, r.EffortLevel, r.IsCompleted))
            .ToList();

        var roundedGlobalScore = ScoringService.RoundForDisplay(globalScore);

        var reportData = new ReportData(
            company.Name,
            company.SectorCode,
            company.SizeRange,
            company.Region,
            completedAt,
            globalScore,
            ScoreLabel.For(roundedGlobalScore),
            diagnostic.DefaultSectorWeightingApplied,
            reportDomainScores,
            reportRecommendations,
            allRecommendations.Count,
            timeProvider.GetUtcNow(),
            ReferentialVersion);

        // Peut lever : aucune ligne Report ne doit alors être écrite (cas 8), d'où l'appel
        // avant AddAsync/SaveChangesAsync ci-dessous plutôt qu'après.
        var bytes = reportGenerator.Generate(reportData);

        await reportRepository.AddAsync(new Report(diagnosticId, currentUser.UserId), ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new GeneratedReport(bytes, BuildFileName(company.Name, completedAt));
    }

    // docs/specs/rapport-pdf.md, section 2 : « maat-diagnostic-{code-entreprise-normalisé}-
    // {aaaa-mm-jj}.pdf », la date étant celle de complétion du diagnostic, jamais celle de la
    // génération.
    private static string BuildFileName(string companyName, DateTimeOffset completedAt) =>
        $"maat-diagnostic-{Slugify(companyName)}-{completedAt:yyyy-MM-dd}.pdf";

    private static string Slugify(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                withoutDiacritics.Append(ch);
            }
        }

        var lowered = withoutDiacritics.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var slug = Regex.Replace(lowered, "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "entreprise" : slug;
    }
}
