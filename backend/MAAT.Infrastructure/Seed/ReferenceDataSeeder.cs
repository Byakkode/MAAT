using MAAT.Domain.Entities;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Seed;

// docs/specs/modele-donnees.md, section « Seed des données de référence ». Charge
// Question, Recommendation et SectorWeight depuis les fichiers CSV embarqués de ce
// dossier — jamais depuis les migrations, qui ne portent que le schéma (voir
// QuestionConfiguration, RecommendationConfiguration, SectorWeightConfiguration).
//
// Upsert idempotent : Question et Recommendation par leur colonne code, SectorWeight par
// (sector_code, domain) — elle n'a pas de colonne code propre. Jamais de suppression :
// retirer une ligne d'un CSV ne supprime rien en base ; désactiver via is_active est la
// façon prévue de retirer une question ou une recommandation du référentiel actif (et
// SectorWeight n'a pas d'équivalent is_active — une ligne qui ne doit plus s'appliquer se
// corrige en modifiant son poids, jamais en la supprimant du CSV).
public class ReferenceDataSeeder(MaatDbContext context)
{
    // Ne sème rien et retourne false si des migrations sont en attente : semer sans schéma
    // à jour échouerait avec une erreur de table manquante. Program.cs journalise alors une
    // invite à migrer d'abord plutôt que de laisser l'exception remonter telle quelle.
    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            return false;
        }

        await SeedQuestionsAsync(ct);
        await SeedSectorWeightsAsync(ct);
        // Après les questions : recommendations.trigger_question_code référence questions.code.
        await SeedRecommendationsAsync(ct);
        await context.SaveChangesAsync(ct);
        return true;
    }

    private async Task SeedQuestionsAsync(CancellationToken ct)
    {
        var existingByCode = await context.Questions.ToDictionaryAsync(q => q.Code, ct);

        foreach (var row in ReferenceDataRows.ReadCsv("questions.csv").Rows)
        {
            var parsed = ReferenceDataRows.ParseQuestion("questions.csv", row);

            if (existingByCode.TryGetValue(parsed.Code, out var question))
            {
                question.Text = parsed.Text;
                question.HelpText = parsed.HelpText;
                question.Domain = parsed.Domain;
                question.Weight = parsed.Weight;
                question.DisplayOrder = parsed.DisplayOrder;
                question.VsmeRef = parsed.VsmeRef;
                question.IsoRef = parsed.IsoRef;
                question.GriRef = parsed.GriRef;
                question.EcovadisRef = parsed.EcovadisRef;
                question.IsActive = parsed.IsActive;
            }
            else
            {
                context.Questions.Add(new Question(parsed.Code, parsed.Text, parsed.Domain, parsed.Weight, parsed.DisplayOrder)
                {
                    HelpText = parsed.HelpText,
                    VsmeRef = parsed.VsmeRef,
                    IsoRef = parsed.IsoRef,
                    GriRef = parsed.GriRef,
                    EcovadisRef = parsed.EcovadisRef,
                    IsActive = parsed.IsActive,
                });
            }
        }
    }

    private async Task SeedRecommendationsAsync(CancellationToken ct)
    {
        var existingByCode = await context.Recommendations.ToDictionaryAsync(r => r.Code, ct);

        foreach (var row in ReferenceDataRows.ReadCsv("recommendations.csv").Rows)
        {
            var parsed = ReferenceDataRows.ParseRecommendation("recommendations.csv", row);

            if (existingByCode.TryGetValue(parsed.Code, out var recommendation))
            {
                recommendation.Domain = parsed.Domain;
                recommendation.ActionText = parsed.ActionText;
                recommendation.DetailText = parsed.DetailText;
                recommendation.ImpactPoints = parsed.ImpactPoints;
                recommendation.EffortLevel = parsed.EffortLevel;
                recommendation.TriggerQuestionCode = parsed.TriggerQuestionCode;
                recommendation.TriggerMaxValue = parsed.TriggerMaxValue;
                recommendation.IsActive = parsed.IsActive;
            }
            else
            {
                context.Recommendations.Add(new Recommendation(
                    parsed.Code, parsed.Domain, parsed.ActionText, parsed.ImpactPoints, parsed.EffortLevel,
                    parsed.TriggerQuestionCode, parsed.TriggerMaxValue)
                {
                    DetailText = parsed.DetailText,
                    IsActive = parsed.IsActive,
                });
            }
        }
    }

    private async Task SeedSectorWeightsAsync(CancellationToken ct)
    {
        var existingByKey = (await context.SectorWeights.ToListAsync(ct))
            .ToDictionary(sw => (sw.SectorCode, sw.Domain));

        foreach (var row in ReferenceDataRows.ReadCsv("sector-weights.csv").Rows)
        {
            var parsed = ReferenceDataRows.ParseSectorWeight("sector-weights.csv", row);

            if (existingByKey.TryGetValue((parsed.SectorCode, parsed.Domain), out var sectorWeight))
            {
                sectorWeight.Weight = parsed.Weight;
            }
            else
            {
                context.SectorWeights.Add(parsed.SectorCode is null
                    ? SectorWeight.Default(parsed.Domain, parsed.Weight)
                    : SectorWeight.ForSector(parsed.SectorCode, parsed.Domain, parsed.Weight));
            }
        }
    }
}
