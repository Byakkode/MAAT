namespace MAAT.Application.Interfaces;

public sealed record GitHubIssueCreated(int Number, string HtmlUrl);

public sealed record GitHubIssueState(string State, int CommentsCount);

public sealed record GitHubComment(string AuthorLogin, string Body, DateTimeOffset CreatedAt);
