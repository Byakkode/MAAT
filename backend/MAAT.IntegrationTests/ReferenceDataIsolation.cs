using MAAT.Domain.Enums;
using MAAT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAAT.IntegrationTests;

// Neutralise le référentiel réel (questions.csv/recommendations.csv, ReferenceDataSeeder)
// pour un fixture qui a besoin d'un univers de questions/recommandations entièrement
// contrôlé — comptages ou scores exacts, qui casseraient à chaque évolution du contenu réel
// (docs/specs/referentiel.md). is_active=false, jamais une suppression : les deux tables de
// référence interdisent DELETE (modele-donnees.md, « ne jamais supprimer une Question »).
//
// Questions et Recommendations toujours désactivées ensemble, jamais l'une sans l'autre :
// une Recommendation active dont la question déclencheuse devient inactive (donc jamais
// répondue, puisque GET .../questions ne la retourne plus) fait lever
// MissingTriggerResponseException à la complétion (RecommendationEngine.SelectTriggered).
internal static class ReferenceDataIsolation
{
    public static async Task DeactivateAllAsync(MaatDbContext context)
    {
        await context.Questions.ExecuteUpdateAsync(s => s.SetProperty(q => q.IsActive, false));
        await context.Recommendations.ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
    }

    // Variante pour un fixture qui a délibérément besoin d'un seul domaine réel actif
    // (ReportSectorWeightingFallbackTests) : conserve tout le domaine `keep` tel quel,
    // désactive le reste.
    public static async Task DeactivateExceptDomainAsync(MaatDbContext context, RseDomain keep)
    {
        await context.Questions.Where(q => q.Domain != keep).ExecuteUpdateAsync(s => s.SetProperty(q => q.IsActive, false));
        await context.Recommendations.Where(r => r.Domain != keep).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
    }
}
