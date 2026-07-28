using MAAT.Application.DTOs;
using MAAT.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAAT.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(UserService userService) : ControllerBase
{
    // TODO(section 4 / avant mise en production) : cet endpoint crée un compte avec un
    // hash de mot de passe aléatoire inutilisable (UserService.InviteAsync) et aucun
    // flux d'acceptation ne permet à l'invité de définir un mot de passe. Le compte créé
    // est donc inutilisable tel quel. Ne pas exposer cet endpoint en production tant que
    // ce flux n'est pas implémenté — voir docs/specs/auth-securite-rgpd.md, section 4.
    [HttpPost("invite")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken ct)
    {
        var user = await userService.InviteAsync(request, ct);

        return StatusCode(StatusCodes.Status201Created, new
        {
            id = user.Id,
            email = user.Email,
            role = user.Role.ToString(),
        });
    }
}
