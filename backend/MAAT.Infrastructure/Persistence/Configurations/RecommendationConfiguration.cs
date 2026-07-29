using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Domain).HasColumnName("domain").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.ActionText).HasColumnName("action_text").IsRequired();
        builder.Property(r => r.DetailText).HasColumnName("detail_text");
        builder.Property(r => r.ImpactPoints).HasColumnName("impact_points").HasPrecision(4, 2).IsRequired();
        builder.Property(r => r.EffortLevel).HasColumnName("effort_level").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.TriggerQuestionCode).HasColumnName("trigger_question_code").HasMaxLength(20).IsRequired();
        builder.Property(r => r.TriggerMaxValue).HasColumnName("trigger_max_value").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(r => r.Code).IsUnique();

        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(r => r.TriggerQuestionCode)
            .HasPrincipalKey(q => q.Code)
            .OnDelete(DeleteBehavior.Restrict);

        // 3 recommandations d'exemple (sur 150+ visées, docs/specs/recommandations.md
        // section 6) — une par question d'exemple de QuestionConfiguration, même principe
        // que ses 3 questions sur 45. impact_points calibré sous le gain maximal théorique
        // de sa question déclencheuse : (5 − trigger_max_value) × w / Σ(5w du domaine) × 100,
        // avec Σ(5w) = 5×(3+2+1) = 30 pour le domaine Environnemental de ce seed.
        builder.HasData(
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0005-000000000001"),
                Code = "REC-ENV-01",
                Domain = RseDomain.Environmental,
                ActionText = "Mettre en place un suivi mensuel de vos émissions de gaz à effet de serre (scope 1 et 2).",
                DetailText = "Un tableur suffit pour démarrer : consommations de carburant, d'électricité et de gaz, converties en équivalent CO2 via les facteurs d'émission de la Base Empreinte de l'ADEME. Le dispositif Diag Décarbon'Action de l'ADEME peut financer un accompagnement.",
                ImpactPoints = 15.00m, // borne théorique (5-2)×3/30×100 = 30
                EffortLevel = EffortLevel.Medium,
                TriggerQuestionCode = "ENV-01",
                TriggerMaxValue = 2,
                IsActive = true,
            },
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0005-000000000002"),
                Code = "REC-ENV-02",
                Domain = RseDomain.Environmental,
                ActionText = "Fixer un objectif chiffré de réduction de votre consommation d'énergie sur trois ans.",
                DetailText = "Un objectif simple (ex. -10 % sur trois ans) suffit pour démarrer une démarche de suivi. Bpifrance et les CCI proposent des diagnostics énergétiques subventionnés pour les PME.",
                ImpactPoints = 10.00m, // borne théorique (5-2)×2/30×100 = 20
                EffortLevel = EffortLevel.Low,
                TriggerQuestionCode = "ENV-02",
                TriggerMaxValue = 2,
                IsActive = true,
            },
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0005-000000000003"),
                Code = "REC-ENV-03",
                Domain = RseDomain.Environmental,
                ActionText = "Mettre en place le tri sélectif des déchets d'activité avec un prestataire agréé.",
                DetailText = "Commencer par les flux les plus simples (papier, carton, emballages). France Num référence des prestataires locaux de collecte et de valorisation.",
                ImpactPoints = 5.00m, // borne théorique (5-2)×1/30×100 = 10
                EffortLevel = EffortLevel.Low,
                TriggerQuestionCode = "ENV-03",
                TriggerMaxValue = 2,
                IsActive = true,
            });
    }
}
