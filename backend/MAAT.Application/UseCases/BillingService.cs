using MAAT.Application.DTOs;
using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Services;

namespace MAAT.Application.UseCases;

// docs/specs/abonnement.md, section 2 : actions d'un utilisateur connecté sur l'abonnement de
// son entreprise. L'entreprise vient toujours du jeton (ICurrentUserContext), jamais du client.
// Une offre payante n'est jamais activée sur la parole du navigateur : ConfirmCheckoutAsync et
// RefreshFromProviderAsync relisent l'état chez le prestataire, comme le webhook.
public class BillingService(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    IPaymentGateway paymentGateway,
    SubscriptionSynchronizer synchronizer,
    ICurrentUserContext currentUser,
    IUnitOfWork unitOfWork)
{
    // Chemins de retour depuis les pages hébergées par le prestataire, côté application web.
    public const string CheckoutSuccessPath = "/abonnement/confirmation";
    public const string CheckoutCancelPath = "/abonnement?paiement=annule";
    public const string PortalReturnPath = "/compte";

    public async Task<SubscriptionView> GetCurrentAsync(CancellationToken ct) =>
        ToView(await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct));

    public async Task<SubscriptionView> ChooseStarterAsync(CancellationToken ct)
    {
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (subscription is null)
        {
            subscription = Subscription.StartStarter(currentUser.CompanyId);
            await subscriptionRepository.AddAsync(subscription, ct);
        }
        else
        {
            subscription.SwitchToStarter();
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ToView(subscription);
    }

    public async Task<RedirectUrlResult> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken ct)
    {
        SubscriptionPlanCatalog.EnsurePurchasable(request.Plan);

        // Un seul abonnement payant par entreprise : changer d'offre passe par le portail,
        // qui modifie l'abonnement existant au lieu d'en facturer un second.
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (subscription is { HasPaidProviderSubscription: true })
        {
            throw new PaidSubscriptionActiveException();
        }

        var user = await userRepository.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new InvalidOperationException("Utilisateur du principal authentifié introuvable.");

        var url = await paymentGateway.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(
                currentUser.CompanyId,
                user.Email,
                subscription?.StripeCustomerId,
                request.Plan,
                request.BillingPeriod,
                CheckoutSuccessPath,
                CheckoutCancelPath),
            ct);

        return new RedirectUrlResult(url);
    }

    // Retour de la page de paiement : active l'offre sans attendre le webhook, qui peut arriver
    // plus tard ou pas du tout (poste de développement sans « stripe listen »). La session est
    // relue chez le prestataire : un identifiant inventé, non payé ou appartenant à une autre
    // entreprise n'active rien.
    public async Task<SubscriptionView> ConfirmCheckoutAsync(string checkoutSessionId, CancellationToken ct)
    {
        var snapshot = await paymentGateway.ReadCheckoutSessionAsync(checkoutSessionId, ct);
        if (snapshot is not null)
        {
            if (snapshot.CompanyId != currentUser.CompanyId)
            {
                throw new CheckoutSessionNotFoundException();
            }

            await synchronizer.ApplyAsync(snapshot, ct);
        }

        return await GetCurrentAsync(ct);
    }

    // Retour du portail client (changement d'offre, résiliation) : même principe que
    // ConfirmCheckoutAsync, pour que l'espace du compte affiche aussitôt ce qui vient d'être fait.
    public async Task<SubscriptionView> RefreshFromProviderAsync(CancellationToken ct)
    {
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (subscription?.StripeSubscriptionId is { } providerSubscriptionId)
        {
            var snapshot = await paymentGateway.ReadSubscriptionAsync(providerSubscriptionId, ct);
            if (snapshot is not null && snapshot.CompanyId == currentUser.CompanyId)
            {
                await synchronizer.ApplyAsync(snapshot, ct);
            }
        }

        return await GetCurrentAsync(ct);
    }

    public async Task<RedirectUrlResult> OpenCustomerPortalAsync(CancellationToken ct)
    {
        var subscription = await subscriptionRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (subscription?.StripeCustomerId is null)
        {
            throw new NoBillingAccountException();
        }

        var url = await paymentGateway.CreateCustomerPortalSessionAsync(subscription.StripeCustomerId, PortalReturnPath, ct);
        return new RedirectUrlResult(url);
    }

    private static SubscriptionView ToView(Subscription? subscription) =>
        subscription is null
            ? new SubscriptionView(null, null, null, false)
            : new SubscriptionView(subscription.Plan, subscription.BillingPeriod, subscription.Status, subscription.StripeCustomerId is not null);
}
