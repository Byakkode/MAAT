using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Siret).HasColumnName("siret").HasMaxLength(14);
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.SectorCode).HasColumnName("sector_code").HasMaxLength(6).IsRequired();
        builder.Property(c => c.SizeRange).HasColumnName("size_range").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Region).HasColumnName("region").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(c => c.Siret).IsUnique();

        builder.HasMany<User>().WithOne().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<Diagnostic>().WithOne().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
