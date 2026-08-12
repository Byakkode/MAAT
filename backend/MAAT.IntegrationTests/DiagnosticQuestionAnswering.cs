using System.Net.Http.Json;
using System.Text.Json;

namespace MAAT.IntegrationTests;

// Toutes les fixtures qui complètent un diagnostic répondent ici aux questions réellement
// actives (GET /api/diagnostics/{id}/questions), jamais à une liste de codes codée en dur :
// le référentiel évolue (docs/specs/referentiel.md), et une liste figée casse dès qu'une
// question est ajoutée, retirée ou renumérotée.
internal static class DiagnosticQuestionAnswering
{
    // overrides : valeur précise pour des codes nommés (un test qui vérifie le comportement
    // d'une question donnée a le droit de la nommer). defaultValue : valeur des questions non
    // nommées, ni testées ni pertinentes pour l'assertion — 5 par défaut, qui ne déclenche
    // aucune recommandation réelle (trigger_max_value = 3 pour les 45 recommandations du
    // seed, vérifié). skip : codes délibérément non répondus, pour un test qui vérifie
    // qu'une complétion incomplète échoue — jamais un oubli silencieux, toujours nommé par
    // l'appelant.
    public static async Task AnswerActiveQuestionsAsync(
        HttpClient client,
        string accessToken,
        Guid diagnosticId,
        IReadOnlyDictionary<string, int>? overrides = null,
        int defaultValue = 5,
        IReadOnlySet<string>? skip = null)
    {
        foreach (var code in await GetActiveQuestionCodesAsync(client, accessToken, diagnosticId))
        {
            if (skip is not null && skip.Contains(code))
            {
                continue;
            }

            var value = overrides is not null && overrides.TryGetValue(code, out var overrideValue) ? overrideValue : defaultValue;

            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/diagnostics/{diagnosticId}/responses/{code}");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = JsonContent.Create(new { value });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Échec de réponse à {code} : {response.StatusCode}");
            }
        }
    }

    public static async Task<IReadOnlyList<string>> GetActiveQuestionCodesAsync(HttpClient client, string accessToken, Guid diagnosticId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/diagnostics/{diagnosticId}/questions");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var questions = await response.Content.ReadFromJsonAsync<JsonElement>();
        return questions.EnumerateArray().Select(q => q.GetProperty("code").GetString()!).ToList();
    }
}
