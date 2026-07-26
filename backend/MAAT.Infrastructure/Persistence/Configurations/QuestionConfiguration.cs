using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(q => q.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(q => q.Text).HasColumnName("text").IsRequired();
        builder.Property(q => q.HelpText).HasColumnName("help_text");
        builder.Property(q => q.Domain).HasColumnName("domain").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(q => q.Weight).HasColumnName("weight").HasPrecision(4, 2).IsRequired();
        builder.Property(q => q.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(q => q.VsmeRef).HasColumnName("vsme_ref").HasMaxLength(50);
        builder.Property(q => q.IsoRef).HasColumnName("iso_ref").HasMaxLength(50);
        builder.Property(q => q.GriRef).HasColumnName("gri_ref").HasMaxLength(50);
        builder.Property(q => q.EcovadisRef).HasColumnName("ecovadis_ref").HasMaxLength(50);
        builder.Property(q => q.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasAlternateKey(q => q.Code);

        // 3 questions d'exemple (sur 45) — mêmes codes/poids que docs/specs/scoring.md, cas de test 3.
        builder.HasData(
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000001"),
                Code = "ENV-01",
                Text = "Mesurez-vous et suivez-vous vos émissions de gaz à effet de serre (scope 1 et 2) ?",
                HelpText = "Le scope 1 couvre les émissions directes (véhicules, chaudières), le scope 2 les émissions liées à l'électricité achetée.",
                Domain = RseDomain.Environmental,
                Weight = 3.00m,
                DisplayOrder = 1,
                VsmeRef = "B3",
                IsoRef = "6.5.5",
                GriRef = "305-1",
                EcovadisRef = (string?)null,
                IsActive = true,
            },
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000002"),
                Code = "ENV-02",
                Text = "Avez-vous mis en place un plan de réduction de votre consommation d'énergie ?",
                HelpText = "Il peut s'agir d'objectifs chiffrés, d'un suivi des consommations ou d'investissements en efficacité énergétique.",
                Domain = RseDomain.Environmental,
                Weight = 2.00m,
                DisplayOrder = 2,
                VsmeRef = "B4",
                IsoRef = (string?)null,
                GriRef = (string?)null,
                EcovadisRef = (string?)null,
                IsActive = true,
            },
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0004-000000000003"),
                Code = "ENV-03",
                Text = "Triez-vous et valorisez-vous vos déchets d'activité ?",
                HelpText = "Valoriser signifie recycler, réemployer ou faire traiter les déchets par une filière dédiée plutôt que les envoyer en décharge.",
                Domain = RseDomain.Environmental,
                Weight = 1.00m,
                DisplayOrder = 3,
                VsmeRef = "B7",
                IsoRef = (string?)null,
                GriRef = "306-2",
                EcovadisRef = (string?)null,
                IsActive = true,
            }
        );
    }
}
