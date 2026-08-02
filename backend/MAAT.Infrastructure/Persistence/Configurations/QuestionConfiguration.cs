using MAAT.Domain.Entities;
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

        // Pas de HasData ici : le contenu (45 questions visées) vit dans
        // MAAT.Infrastructure/Seed/questions.csv, chargé par ReferenceDataSeeder au
        // démarrage (Development) ou via la commande "seed" — voir
        // docs/specs/modele-donnees.md. Les migrations ne portent que le schéma.
    }
}
