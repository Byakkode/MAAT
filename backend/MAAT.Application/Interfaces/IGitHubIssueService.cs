namespace MAAT.Application.Interfaces;

public interface IGitHubIssueService
{
    Task<GitHubIssueCreated> CreateIssueAsync(
        string title,
        string ticketType,
        string description,
        IReadOnlyList<(string FileName, string ContentType, byte[] Data)> attachments,
        CancellationToken ct = default);

    Task<GitHubIssueState?> GetIssueStateAsync(int issueNumber, CancellationToken ct = default);

    Task<IReadOnlyList<GitHubComment>> GetIssueCommentsAsync(int issueNumber, CancellationToken ct = default);
}
