using Boilerplate.Application.Features.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Answers 404 unless <c>Accounts:Enabled</c> is set (ADR-0025), so a deployment with an external identity provider
/// exposes no local sign-in. Runs before model binding and validation.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class RequireAccountsEnabledAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!context.HttpContext.RequestServices.GetRequiredService<AccountSettings>().Enabled)
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
