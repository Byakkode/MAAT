using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class ActionItemProgressConfiguration : IEntityTypeConfiguration<ActionItemProgress>
{
    public void Configure(EntityTypeBuilder<ActionItemProgress> builder)
    {
        builder.ToTable("action_item_progress");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DiagnosticId).HasColumnName("diagnostic_id").IsRequired();
        builder.Property(x => x.RecommendationCode)
            .HasColumnName("recommendation_code")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.AssignedTo).HasColumnName("assigned_to").HasMaxLength(200);
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Un seul enregistrement par (diagnostic, code).
        builder.HasIndex(x => new { x.DiagnosticId, x.RecommendationCode }).IsUnique();

        // Supprimé avec le diagnostic, donc avec l'entreprise : responsable et notes sont du
        // texte saisi par l'entreprise (droit à l'effacement, auth-securite-rgpd.md section 6).
        builder.HasOne<Diagnostic>().WithMany().HasForeignKey(x => x.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
    }
}
