using System.Collections.Concurrent;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;

namespace MAAT.IntegrationTests.TestDoubles;

// Remplace StripePaymentGateway dans BillingApiFixture : aucun test d'intégration n'appelle
// Stripe (pas de clé en CI, pas de réseau). Les webhooks sont simulés par signature : chaque
// test enregistre la « signature » qu'il enverra et l'état d'abonnement qu'elle doit
// produire, ce qui évite qu'un test lise l'événement préparé par un autre.
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string CheckoutUrl = "https://checkout.stripe.test/session";
    public const string PortalUrl = "https://billing.stripe.test/portal";

    private readonly ConcurrentDictionary<string, ProviderSubscriptionSnapshot?> _webhooks = new();
    private readonly ConcurrentDictionary<string, ProviderSubscriptionSnapshot> _checkoutSessions = new();
    private readonly ConcurrentDictionary<string, ProviderSubscriptionSnapshot> _subscriptions = new();

    public ConcurrentQueue<CheckoutSessionRequest> CheckoutRequests { get; } = new();

    public ConcurrentQueue<string> PortalCustomers { get; } = new();

    public ConcurrentQueue<string> CanceledSubscriptions { get; } = new();

    // Simule un prestataire indisponible au moment de la résiliation (suppression de compte).
    public bool FailCancellation { get; set; }

    public void RegisterWebhook(string signature, ProviderSubscriptionSnapshot? snapshot) =>
        _webhooks[signature] = snapshot;

    // Session payée, telle que Stripe la décrirait au retour du navigateur.
    public void RegisterPaidCheckoutSession(string sessionId, ProviderSubscriptionSnapshot snapshot) =>
        _checkoutSessions[sessionId] = snapshot;

    // État courant d'un abonnement chez Stripe (après un passage par le portail client).
    public void SetSubscriptionState(ProviderSubscriptionSnapshot snapshot) =>
        _subscriptions[snapshot.SubscriptionId] = snapshot;

    public Task<ProviderSubscriptionSnapshot?> ReadCheckoutSessionAsync(string checkoutSessionId, CancellationToken ct) =>
        Task.FromResult(_checkoutSessions.TryGetValue(checkoutSessionId, out var snapshot) ? snapshot : null);

    public Task<ProviderSubscriptionSnapshot?> ReadSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        Task.FromResult(_subscriptions.TryGetValue(subscriptionId, out var snapshot) ? snapshot : null);

    public Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken ct)
    {
        CheckoutRequests.Enqueue(request);
        return Task.FromResult(CheckoutUrl);
    }

    public Task<string> CreateCustomerPortalSessionAsync(string customerId, string returnPath, CancellationToken ct)
    {
        PortalCustomers.Enqueue(customerId);
        return Task.FromResult(PortalUrl);
    }

    public Task<ProviderSubscriptionSnapshot?> ReadWebhookAsync(string payload, string signatureHeader, CancellationToken ct) =>
        _webhooks.TryGetValue(signatureHeader, out var snapshot)
            ? Task.FromResult(snapshot)
            : throw new InvalidWebhookSignatureException();

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        if (FailCancellation)
        {
            throw new PaymentProviderException("Prestataire indisponible (simulé).");
        }

        CanceledSubscriptions.Enqueue(subscriptionId);
        return Task.CompletedTask;
    }
}
