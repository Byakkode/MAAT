using MAAT.Domain.Entities;
using MAAT.Domain.Enums;
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

        builder.HasData(
            // Pondération par défaut — repli quand le code NAF de l'entreprise n'est couvert par aucun secteur (modele-donnees.md).
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000001"), SectorCode = (string?)null, IsDefault = true, Domain = RseDomain.Environmental, Weight = 0.200m },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000002"), SectorCode = (string?)null, IsDefault = true, Domain = RseDomain.Social, Weight = 0.200m },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000003"), SectorCode = (string?)null, IsDefault = true, Domain = RseDomain.Ethics, Weight = 0.200m },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000004"), SectorCode = (string?)null, IsDefault = true, Domain = RseDomain.Procurement, Weight = 0.200m },
            new { Id = Guid.Parse("00000000-0000-0000-0001-000000000005"), SectorCode = (string?)null, IsDefault = true, Domain = RseDomain.Governance, Weight = 0.200m },

            // 4941A — Transports routiers de fret interurbains (docs/specs/scoring.md, cas de test 4).
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000001"), SectorCode = "4941A", IsDefault = false, Domain = RseDomain.Environmental, Weight = 0.400m },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000002"), SectorCode = "4941A", IsDefault = false, Domain = RseDomain.Social, Weight = 0.200m },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000003"), SectorCode = "4941A", IsDefault = false, Domain = RseDomain.Ethics, Weight = 0.150m },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000004"), SectorCode = "4941A", IsDefault = false, Domain = RseDomain.Procurement, Weight = 0.150m },
            new { Id = Guid.Parse("00000000-0000-0000-0002-000000000005"), SectorCode = "4941A", IsDefault = false, Domain = RseDomain.Governance, Weight = 0.100m },

            // 6202A — Conseil en systèmes et logiciels informatiques (docs/specs/scoring.md, cas de test 4).
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000001"), SectorCode = "6202A", IsDefault = false, Domain = RseDomain.Environmental, Weight = 0.100m },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000002"), SectorCode = "6202A", IsDefault = false, Domain = RseDomain.Social, Weight = 0.300m },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000003"), SectorCode = "6202A", IsDefault = false, Domain = RseDomain.Ethics, Weight = 0.250m },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000004"), SectorCode = "6202A", IsDefault = false, Domain = RseDomain.Procurement, Weight = 0.150m },
            new { Id = Guid.Parse("00000000-0000-0000-0003-000000000005"), SectorCode = "6202A", IsDefault = false, Domain = RseDomain.Governance, Weight = 0.200m }
        );
    }
}
