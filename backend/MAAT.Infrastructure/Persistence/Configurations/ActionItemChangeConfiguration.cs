using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class ActionItemChangeConfiguration : IEntityTypeConfiguration<ActionItemChange>
{
    public void Configure(EntityTypeBuilder<ActionItemChange> builder)
    {
        builder.ToTable("action_item_changes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.DiagnosticId).HasColumnName("diagnostic_id").IsRequired();
        // Même longueur que action_item_progress.recommendation_code.
        builder.Property(c => c.RecommendationCode).HasColumnName("recommendation_code").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Field).HasColumnName("field").HasConversion<string>().HasMaxLength(20).IsRequired();
        // Responsable : 200 caractères au plus, comme action_item_progress.assigned_to ;
        // statut et date sont plus courts.
        builder.Property(c => c.OldValue).HasColumnName("old_value").HasMaxLength(200);
        builder.Property(c => c.NewValue).HasColumnName("new_value").HasMaxLength(200);
        builder.Property(c => c.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(c => c.ChangedAt).HasColumnName("changed_at").IsRequired();

        // Lecture de l'historique d'une action, du plus récent au plus ancien.
        builder.HasIndex(c => new { c.DiagnosticId, c.RecommendationCode, c.ChangedAt });

        // docs/specs/recommandations.md, section 4 bis : l'historique part avec le diagnostic
        // (donc avec l'entreprise, auth-securite-rgpd.md section 6). Un compte supprimé seul
        // laisse ses lignes, devenues anonymes : l'historique de l'entreprise reste lisible
        // sans conserver l'identité de quelqu'un qui a exercé son droit à l'effacement.
        builder.HasOne<Diagnostic>().WithMany().HasForeignKey(c => c.DiagnosticId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
