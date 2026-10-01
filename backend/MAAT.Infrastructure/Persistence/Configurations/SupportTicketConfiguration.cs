using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.GithubIssueNumber).HasColumnName("github_issue_number").IsRequired();
        builder.Property(t => t.GithubIssueUrl).HasColumnName("github_issue_url").HasMaxLength(500).IsRequired();
        builder.Property(t => t.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(5000).IsRequired();
        builder.Property(t => t.TicketType).HasColumnName("ticket_type").HasMaxLength(20).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(t => t.CompanyId);

        // Droit à l'effacement (auth-securite-rgpd.md section 6) : un ticket disparaît avec
        // l'entreprise, et aussi avec le compte qui l'a ouvert, même quand l'entreprise reste.
        // Titre et description sont du texte libre rédigé par cette personne ; les garder sans
        // auteur ne l'anonymiserait qu'en apparence.
        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
