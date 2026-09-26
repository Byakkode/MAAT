using MAAT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAAT.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(s => s.Plan).HasColumnName("plan").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.BillingPeriod).HasColumnName("billing_period").HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.StripeCustomerId).HasColumnName("stripe_customer_id").HasMaxLength(255);
        builder.Property(s => s.StripeSubscriptionId).HasColumnName("stripe_subscription_id").HasMaxLength(255);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Ignore(s => s.HasPaidProviderSubscription);

        // Un abonnement par entreprise (docs/specs/abonnement.md, section 3), supprimé avec
        // elle (droit à l'effacement, auth-securite-rgpd.md section 6).
        builder.HasIndex(s => s.CompanyId).IsUnique();
        builder.HasIndex(s => s.StripeSubscriptionId).IsUnique();
        builder.HasOne<Company>().WithOne().HasForeignKey<Subscription>(s => s.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
