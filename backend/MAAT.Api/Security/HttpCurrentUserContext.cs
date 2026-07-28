using MAAT.Application.Interfaces;
using MAAT.Domain.Enums;

namespace MAAT.Api.Security;

public class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public Guid UserId => Guid.Parse(RequireClaim("sub"));

    public Guid CompanyId => Guid.Parse(RequireClaim("company_id"));

    public UserRole Role => Enum.Parse<UserRole>(RequireClaim("role"));

    private string RequireClaim(string claimType)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("Aucune requête HTTP en cours : ICurrentUserContext n'est utilisable que derrière [Authorize].");

        return principal.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"Claim '{claimType}' absente du principal authentifié.");
    }
}
