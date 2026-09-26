namespace MAAT.Infrastructure.Billing;

// docs/specs/abonnement.md, section 7. Les deux secrets viennent des user-secrets en
// développement et des variables d'environnement Stripe__SecretKey / Stripe__WebhookSecret
// en production, jamais d'un fichier commité.
public sealed class StripeOptions
{
    public const string Section = "Stripe";

    public string? SecretKey { get; init; }

    public string? WebhookSecret { get; init; }

    // URL publique de l'application web, vers laquelle Stripe renvoie après le paiement ou
    // la sortie du portail client.
    public string ReturnBaseUrl { get; init; } = "http://localhost:5173";
}
