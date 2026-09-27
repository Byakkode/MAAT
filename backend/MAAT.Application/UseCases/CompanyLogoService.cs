using MAAT.Application.Exceptions;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using MAAT.Domain.Enums;

namespace MAAT.Application.UseCases;

// docs/specs/rapport-pdf.md, section 7 : logo de l'entreprise sur la page de garde du
// rapport. Toujours l'entreprise du principal authentifié. Le rôle Admin est exigé par le
// contrôleur pour l'envoi et la suppression ; l'offre (Essential et plus) est vérifiée ici,
// comme pour tout droit d'offre (abonnement.md, section 8).
public class CompanyLogoService(
    ICompanyLogoRepository logoRepository,
    ILogoImageProcessor imageProcessor,
    CurrentPlanService currentPlan,
    ICurrentUserContext currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    // Taille du fichier reçu, avant normalisation. Un logo tient en quelques dizaines de Ko ;
    // 2 Mo laisse passer un export haute définition sans ouvrir la porte à un fichier qui
    // occuperait la mémoire du serveur au décodage.
    public const int MaxUploadBytes = 2 * 1024 * 1024;

    // Lecture ouverte quelle que soit l'offre : une entreprise revenue sur Starter retrouve le
    // logo qu'elle avait envoyé (rien n'est effacé au changement d'offre, abonnement.md
    // section 8), même s'il n'apparaît plus sur le rapport.
    public async Task<byte[]?> GetAsync(CancellationToken ct) =>
        (await logoRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct))?.PngContent;

    public async Task UploadAsync(byte[] uploaded, CancellationToken ct)
    {
        await currentPlan.EnsureAsync(e => e.CanCustomizeReportLogo, SubscriptionPlan.Essential, ct);

        if (uploaded.Length == 0)
        {
            throw new InvalidLogoImageException("Le fichier envoyé est vide.");
        }

        if (uploaded.Length > MaxUploadBytes)
        {
            throw new InvalidLogoImageException("Le logo ne doit pas dépasser 2 Mo.");
        }

        var png = imageProcessor.NormalizeToPng(uploaded);
        var now = timeProvider.GetUtcNow();

        var existing = await logoRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (existing is null)
        {
            await logoRepository.AddAsync(new CompanyLogo(currentUser.CompanyId, png, now), ct);
        }
        else
        {
            existing.Replace(png, now);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    // Pas de contrôle d'offre : retirer son logo reste possible après un retour sur Starter.
    // Idempotent : supprimer un logo absent n'est pas une erreur.
    public async Task DeleteAsync(CancellationToken ct)
    {
        var existing = await logoRepository.FindByCompanyIdAsync(currentUser.CompanyId, ct);
        if (existing is null)
        {
            return;
        }

        logoRepository.Remove(existing);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
