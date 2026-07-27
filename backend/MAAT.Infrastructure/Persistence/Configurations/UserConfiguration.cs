using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsRequired();
        builder.Property(u => u.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.EmailVerified).HasColumnName("email_verified").IsRequired();
        builder.Property(u => u.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(u => u.LastLogin).HasColumnName("last_login");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasMany<RefreshToken>().WithOne().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<EmailVerificationToken>().WithOne().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
