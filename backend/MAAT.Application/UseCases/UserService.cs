using MAAT.Application.DTOs;
using MAAT.Application.Interfaces;
using MAAT.Application.Security;
using MAAT.Domain.Entities;

namespace MAAT.Application.UseCases;

public class UserService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ICurrentUserContext currentUser)
{
    // Le flux d'acceptation d'invitation (l'invité choisit son mot de passe) est hors
    // périmètre de la section 4 (rôles et cloisonnement) ; le compte est créé avec un
    // hash aléatoire inutilisable en attendant ce flux.
    public async Task<User> InviteAsync(InviteUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var placeholderHash = passwordHasher.Hash(SecureTokenGenerator.Generate());

        var user = new User(email, placeholderHash, currentUser.CompanyId, request.Role);
        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user;
    }
}
