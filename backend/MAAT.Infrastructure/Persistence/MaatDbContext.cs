using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Persistence;

public class MaatDbContext(DbContextOptions<MaatDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Diagnostic> Diagnostics => Set<Diagnostic>();
    public DbSet<DomainScore> DomainScores => Set<DomainScore>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<SectorWeight> SectorWeights => Set<SectorWeight>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<DiagnosticRecommendation> DiagnosticRecommendations => Set<DiagnosticRecommendation>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<RseIndicators> RseIndicators => Set<RseIndicators>();
    public DbSet<ActionItemProgress> ActionItemProgresses => Set<ActionItemProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaatDbContext).Assembly);
    }
}
