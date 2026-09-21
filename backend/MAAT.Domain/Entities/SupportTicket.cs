namespace MAAT.Domain.Entities;

public class SupportTicket
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public int GithubIssueNumber { get; private set; }
    public string GithubIssueUrl { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string TicketType { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private SupportTicket() { }

    public SupportTicket(
        Guid companyId, Guid userId,
        int githubIssueNumber, string githubIssueUrl,
        string title, string description, string ticketType)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        GithubIssueNumber = githubIssueNumber;
        GithubIssueUrl = githubIssueUrl;
        Title = title;
        Description = description;
        TicketType = ticketType;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
