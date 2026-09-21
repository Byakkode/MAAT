using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MAAT.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAAT.Infrastructure.GitHub;

public sealed class GitHubIssueService(
    HttpClient httpClient,
    IOptions<GitHubOptions> options,
    ILogger<GitHubIssueService> logger) : IGitHubIssueService
{
    private static readonly Dictionary<string, string[]> TicketLabels = new()
    {
        ["bug"]      = ["bug"],
        ["database"] = ["bug", "database"],
        ["feature"]  = ["enhancement"],
        ["question"] = ["question"],
    };

    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".log", ".json", ".csv", ".xml", ".yaml", ".yml" };

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    public async Task<GitHubIssueCreated> CreateIssueAsync(
        string title,
        string ticketType,
        string description,
        IReadOnlyList<(string FileName, string ContentType, byte[] Data)> attachments,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrEmpty(opts.Token))
            throw new InvalidOperationException("GitHub:Token n'est pas configuré.");

        var bodyBuilder = new StringBuilder(description.Trim());

        foreach (var (fileName, _, data) in attachments)
        {
            if (!TextExtensions.Contains(Path.GetExtension(fileName))) continue;

            var text = Encoding.UTF8.GetString(data);
            const int maxChars = 5000;
            var truncated = text.Length > maxChars;
            if (truncated) text = text[..maxChars];

            bodyBuilder
                .AppendLine().AppendLine()
                .AppendLine($"<details><summary>📎 {fileName}{(truncated ? " (tronqué à 5 000 car.)" : "")}</summary>")
                .AppendLine()
                .AppendLine("```")
                .Append(text)
                .AppendLine()
                .AppendLine("```")
                .AppendLine()
                .AppendLine("</details>");
        }

        var imageFiles = attachments
            .Where(a => ImageExtensions.Contains(Path.GetExtension(a.FileName)))
            .ToArray();

        var labels = TicketLabels.TryGetValue(ticketType, out var l) ? l : [];

        using var createReq = BuildApiRequest(HttpMethod.Post, $"repos/{opts.Owner}/{opts.Repo}/issues", opts.Token);
        createReq.Content = JsonContent.Create(new { title, body = bodyBuilder.ToString(), labels });

        var createResp = await httpClient.SendAsync(createReq, ct);
        createResp.EnsureSuccessStatusCode();

        using var issueDoc = await createResp.Content.ReadFromJsonAsync<JsonDocument>(ct)
            ?? throw new InvalidOperationException("Réponse GitHub vide à la création de l'issue.");

        var issueNumber = issueDoc.RootElement.GetProperty("number").GetInt32();
        var issueUrl = issueDoc.RootElement.GetProperty("html_url").GetString()
            ?? throw new InvalidOperationException("URL html_url manquante dans la réponse GitHub.");

        if (imageFiles.Length > 0)
        {
            var updatedBody = new StringBuilder(bodyBuilder.ToString());
            var anyUploaded = false;

            foreach (var (fileName, contentType, data) in imageFiles)
            {
                var assetUrl = await TryUploadImageAsync(opts, issueNumber, fileName, contentType, data, ct);
                if (assetUrl is null) continue;

                updatedBody.AppendLine().Append($"![{fileName}]({assetUrl})");
                anyUploaded = true;
            }

            if (anyUploaded)
            {
                using var patchReq = BuildApiRequest(
                    HttpMethod.Patch,
                    $"repos/{opts.Owner}/{opts.Repo}/issues/{issueNumber}",
                    opts.Token);
                patchReq.Content = JsonContent.Create(new { body = updatedBody.ToString() });

                var patchResp = await httpClient.SendAsync(patchReq, ct);
                if (!patchResp.IsSuccessStatusCode)
                    logger.LogWarning("Mise à jour des images de l'issue #{Number} échouée : {Status}.", issueNumber, patchResp.StatusCode);
            }
        }

        return new GitHubIssueCreated(issueNumber, issueUrl);
    }

    public async Task<GitHubIssueState?> GetIssueStateAsync(int issueNumber, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrEmpty(opts.Token)) return null;

        try
        {
            using var req = BuildApiRequest(HttpMethod.Get, $"repos/{opts.Owner}/{opts.Repo}/issues/{issueNumber}", opts.Token);
            var resp = await httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (doc is null) return null;

            var state = doc.RootElement.GetProperty("state").GetString() ?? "open";
            var commentsCount = doc.RootElement.GetProperty("comments").GetInt32();
            return new GitHubIssueState(state, commentsCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de récupérer l'état de l'issue #{Number}.", issueNumber);
            return null;
        }
    }

    public async Task<IReadOnlyList<GitHubComment>> GetIssueCommentsAsync(int issueNumber, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrEmpty(opts.Token)) return [];

        try
        {
            using var req = BuildApiRequest(HttpMethod.Get, $"repos/{opts.Owner}/{opts.Repo}/issues/{issueNumber}/comments", opts.Token);
            var resp = await httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return [];

            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (doc is null) return [];

            var comments = new List<GitHubComment>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var login = element.GetProperty("user").GetProperty("login").GetString() ?? "inconnu";
                var body = element.GetProperty("body").GetString() ?? "";
                var createdAt = element.GetProperty("created_at").GetDateTimeOffset();
                comments.Add(new GitHubComment(login, body, createdAt));
            }
            return comments;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de récupérer les commentaires de l'issue #{Number}.", issueNumber);
            return [];
        }
    }

    // Endpoint utilisé par l'interface web GitHub pour les dépôts d'images dans les issues.
    // Non officiel mais stable depuis 2023 ; l'échec est silencieux — l'issue est créée
    // sans l'image plutôt que bloquée.
    private async Task<string?> TryUploadImageAsync(
        GitHubOptions opts, int issueNumber,
        string fileName, string contentType, byte[] data, CancellationToken ct)
    {
        try
        {
            var uploadUri = $"https://uploads.github.com/repos/{opts.Owner}/{opts.Repo}/issues/{issueNumber}/assets?name={Uri.EscapeDataString(fileName)}";
            using var req = new HttpRequestMessage(HttpMethod.Post, uploadUri)
            {
                Content = new ByteArrayContent(data),
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {opts.Token}");
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            req.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);

            var resp = await httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Upload image {FileName} refusé par GitHub ({Status}).", fileName, resp.StatusCode);
                return null;
            }

            using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return doc?.RootElement.TryGetProperty("url", out var urlProp) == true
                ? urlProp.GetString()
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Upload image {FileName} échoué.", fileName);
            return null;
        }
    }

    private static HttpRequestMessage BuildApiRequest(HttpMethod method, string relativeUri, string token)
    {
        var req = new HttpRequestMessage(method, relativeUri);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }
}
