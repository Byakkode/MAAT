using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class SectorWeightConfiguration : IEntityTypeConfiguration<SectorWeight>
{
    public void Configure(EntityTypeBuilder<SectorWeight> builder)
    {
        builder.ToTable("sector_weights");

        builder.HasKey(sw => sw.Id);
        builder.Property(sw => sw.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(sw => sw.SectorCode).HasColumnName("sector_code").HasMaxLength(6);
        builder.Property(sw => sw.IsDefault).HasColumnName("is_default").IsRequired().HasDefaultValue(false);
        builder.Property(sw => sw.Domain).HasColumnName("domain").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(sw => sw.Weight).HasColumnName("weight").HasPrecision(4, 3).IsRequired();

        builder.HasIndex(sw => new { sw.SectorCode, sw.Domain }).IsUnique();

        // Un seul jeu de pondération par défaut : au plus une ligne par domaine avec is_default = true.
        builder.HasIndex(sw => sw.Domain)
            .IsUnique()
            .HasDatabaseName("IX_sector_weights_default_domain")
            .HasFilter("is_default");

        // Pas de HasData ici : le contenu (pondération par défaut + un jeu par secteur NAF
        // couvert) vit dans MAAT.Infrastructure/Seed/sector-weights.csv — voir
        // QuestionConfiguration et docs/specs/modele-donnees.md.
    }
}
