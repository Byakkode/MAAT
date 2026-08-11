using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using MAAT.Domain.Services;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace MAAT.Infrastructure.Seed;

// docs/specs/modele-donnees.md — jeu de données de démonstration : 45 questions, deux
// secteurs NAF pondérés et trois entreprises fictives (profils faible / moyen / mature),
// chacune avec un diagnostic complété à une date différente. Peuple un tableau de bord
// réaliste en développement avant que le contenu normatif définitif
// (MAAT.Infrastructure/Seed/*.csv) ne soit rédigé.
//
// Isolation stricte du référentiel réel — jamais confondu, jamais en production :
// - Contenu dans Seed/demo/*.csv, jamais Seed/*.csv (voir MAAT.Infrastructure.csproj).
// - Les 45 questions sont insérées avec is_active=false : ReferenceDataSeeder,
//   QuestionRepository.FindAllActiveAsync, donc tout questionnaire réel, les ignorent
//   totalement (voir QuestionConfiguration). Le calcul de score ci-dessous reçoit malgré
//   tout ces questions avec IsActive=true en mémoire : QuestionScoreInput.IsActive ne
//   reflète pas la colonne persistée, seulement ce qui doit compter pour CE calcul.
// - Les deux codes NAF (5610A, 4110B) ne recoupent aucun code déjà couvert par
//   Seed/sector-weights.csv.
// - Invoqué uniquement par "seed --demo" (jamais au démarrage, jamais par "seed" seul) et
//   uniquement si l'environnement est Development — revérifié ici en défense en profondeur,
//   Program.cs le vérifie déjà avant d'appeler ce service.
public class DemoDataSeeder(MaatDbContext context, IScoringService scoringService, IHostEnvironment environment)
{
    // Entreprises fictives : réponses par domaine, dans l'ordre display_order de
    // demo-questions.csv. Poids uniformes (1.00) dans ce fichier : le score d'un domaine est
    // donc directement la moyenne de ces valeurs rapportée à 5, ce qui rend les trois profils
    // faciles à vérifier de tête plutôt que de dépendre d'une pondération par question encore
    // provisoire.
    private static readonly IReadOnlyList<DemoCompanyProfile> Profiles =
    [
        new DemoCompanyProfile(
            "Bistrot des Trois Chênes",
            "5610A",
            CompanySizeRange.Small,
            "Nouvelle-Aquitaine",
            new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 2, 10, 9, 24, 0, TimeSpan.Zero),
            new Dictionary<RseDomain, int[]>
            {
                [RseDomain.Environmental] = [1, 1, 0, 1, 1, 2, 1, 0, 1, 1, 1],
                [RseDomain.Social] = [1, 1, 1, 0, 1, 1, 2, 1, 1, 0, 1],
                [RseDomain.Ethics] = [1, 1, 0, 1, 2, 1, 1, 0],
                [RseDomain.Procurement] = [2, 1, 1, 1, 2, 1, 1],
                [RseDomain.Governance] = [1, 2, 1, 0, 1, 1, 2, 1],
            }),
        new DemoCompanyProfile(
            "Verdoyer Promotion",
            "4110B",
            CompanySizeRange.Medium,
            "Île-de-France",
            new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 18, 14, 22, 0, TimeSpan.Zero),
            new Dictionary<RseDomain, int[]>
            {
                [RseDomain.Environmental] = [3, 3, 2, 3, 3, 4, 3, 2, 3, 3, 3],
                [RseDomain.Social] = [2, 3, 3, 2, 3, 3, 2, 3, 3, 2, 3],
                [RseDomain.Ethics] = [3, 2, 3, 3, 2, 3, 3, 2],
                [RseDomain.Procurement] = [3, 3, 2, 3, 3, 2, 3],
                [RseDomain.Governance] = [2, 3, 3, 2, 3, 3, 2, 3],
            }),
        new DemoCompanyProfile(
            "Table de Louise",
            "5610A",
            CompanySizeRange.Micro,
            "Occitanie",
            new DateTimeOffset(2026, 7, 25, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 25, 10, 19, 0, TimeSpan.Zero),
            new Dictionary<RseDomain, int[]>
            {
                [RseDomain.Environmental] = [4, 5, 4, 5, 4, 4, 5, 4, 5, 4, 4],
                [RseDomain.Social] = [4, 4, 5, 4, 4, 5, 4, 4, 5, 4, 4],
                [RseDomain.Ethics] = [5, 4, 4, 5, 4, 4, 5, 4],
                [RseDomain.Procurement] = [4, 4, 5, 4, 4, 5, 4],
                [RseDomain.Governance] = [4, 5, 4, 4, 5, 4, 4, 5],
            }),
    ];

    // Ne sème rien et retourne false si des migrations sont en attente — même garde-fou
    // qu'IReferenceDataSeeder.SeedAsync, pour la même raison (semer sans schéma à jour
    // échouerait avec une erreur de table manquante).
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "seed --demo est réservé à l'environnement Development : jamais en production.");
        }

        if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            return false;
        }

        var questionsByCode = await SeedDemoQuestionsAsync(ct);
        var sectorWeightsByCode = await SeedDemoSectorWeightsAsync(ct);
        var questionCodesByDomain = questionsByCode.Values
            .GroupBy(q => q.Domain)
            .ToDictionary(g => g.Key, g => g.OrderBy(q => q.DisplayOrder).Select(q => q.Code).ToList());

        foreach (var profile in Profiles)
        {
            // Idempotent : une entreprise de démonstration déjà présente (même nom) n'est
            // jamais recréée ni dupliquée sur un second "seed --demo".
            if (await context.Companies.AnyAsync(c => c.Name == profile.Name, ct))
            {
                continue;
            }

            var responsesByCode = profile.ResponsesByDomain
                .SelectMany(kv => questionCodesByDomain[kv.Key].Zip(kv.Value, (code, value) => (Code: code, Value: value)))
                .ToList();

            var inputs = responsesByCode
                .Select(x => new QuestionScoreInput(questionsByCode[x.Code].Domain, questionsByCode[x.Code].Weight, x.Value))
                .ToList();

            var result = scoringService.CalculateScore(inputs, sectorWeightsByCode[profile.SectorCode]);

            var company = new Company(profile.Name, profile.SectorCode, profile.CompanySizeRange, profile.Region);
            context.Companies.Add(company);

            var diagnostic = new Diagnostic(company.Id, profile.CreatedAt)
            {
                Status = DiagnosticStatus.Completed,
                CompletedAt = profile.CompletedAt,
                GlobalScore = result.GlobalScore,
            };
            context.Diagnostics.Add(diagnostic);

            foreach (var (code, value) in responsesByCode)
            {
                context.Responses.Add(new Response(diagnostic.Id, questionsByCode[code].Id, value));
            }

            foreach (var detail in result.DomainScores)
            {
                context.DomainScores.Add(new DomainScore(diagnostic.Id, detail.Domain, detail.Score, detail.EffectiveSectorWeight, detail.Numerator, detail.Denominator));
            }
        }

        await context.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Dictionary<string, Question>> SeedDemoQuestionsAsync(CancellationToken ct)
    {
        var existingByCode = await context.Questions.Where(q => q.Code.StartsWith("DEMO-")).ToDictionaryAsync(q => q.Code, ct);

        foreach (var row in ReadDemoCsv("demo-questions.csv"))
        {
            var parsed = ReferenceDataRows.ParseQuestion("demo-questions.csv", row);

            if (existingByCode.TryGetValue(parsed.Code, out var question))
            {
                question.Text = parsed.Text;
                question.Domain = parsed.Domain;
                question.Weight = parsed.Weight;
                question.DisplayOrder = parsed.DisplayOrder;
                question.IsActive = parsed.IsActive;
            }
            else
            {
                question = new Question(parsed.Code, parsed.Text, parsed.Domain, parsed.Weight, parsed.DisplayOrder)
                {
                    IsActive = parsed.IsActive,
                };
                context.Questions.Add(question);
                existingByCode[parsed.Code] = question;
            }
        }

        return existingByCode;
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<RseDomain, decimal>>> SeedDemoSectorWeightsAsync(CancellationToken ct)
    {
        var existingByKey = (await context.SectorWeights.ToListAsync(ct))
            .ToDictionary(sw => (sw.SectorCode, sw.Domain));

        var byCode = new Dictionary<string, Dictionary<RseDomain, decimal>>();

        foreach (var row in ReadDemoCsv("demo-sector-weights.csv"))
        {
            var parsed = ReferenceDataRows.ParseSectorWeight("demo-sector-weights.csv", row);
            var sectorCode = parsed.SectorCode
                ?? throw new InvalidOperationException("demo-sector-weights.csv : sector_code ne doit jamais être vide.");

            if (existingByKey.TryGetValue((sectorCode, parsed.Domain), out var sectorWeight))
            {
                sectorWeight.Weight = parsed.Weight;
            }
            else
            {
                context.SectorWeights.Add(SectorWeight.ForSector(sectorCode, parsed.Domain, parsed.Weight));
            }

            if (!byCode.TryGetValue(sectorCode, out var domainWeights))
            {
                domainWeights = [];
                byCode[sectorCode] = domainWeights;
            }

            domainWeights[parsed.Domain] = parsed.Weight;
        }

        return byCode.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<RseDomain, decimal>)kv.Value);
    }

    private static IReadOnlyList<CsvRow> ReadDemoCsv(string fileName)
    {
        var assembly = typeof(DemoDataSeeder).Assembly;
        var resourceName = $"MAAT.Infrastructure.Seed.demo.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Fichier de seed de démonstration introuvable : {resourceName}");
        return CsvFile.ReadRows(stream, fileName);
    }

    private sealed record DemoCompanyProfile(
        string Name,
        string SectorCode,
        CompanySizeRange CompanySizeRange,
        string Region,
        DateTimeOffset CreatedAt,
        DateTimeOffset CompletedAt,
        IReadOnlyDictionary<RseDomain, int[]> ResponsesByDomain);
}
