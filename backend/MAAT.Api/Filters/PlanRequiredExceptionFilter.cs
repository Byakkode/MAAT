using MAAT.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MAAT.Api.Filters;

// docs/specs/abonnement.md, section 8 : une seule traduction du refus d'offre, plutôt qu'un
// catch recopié dans chaque action concernée. Le corps distingue ce refus d'un manque de rôle
// (403 nu de [Authorize(Roles = ...)]).
public sealed class PlanRequiredExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PlanRequiredException ex)
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            code = "plan_required",
            requiredPlan = ex.RequiredPlan.ToString(),
            message = ex.Message,
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
        context.ExceptionHandled = true;
    }
}
