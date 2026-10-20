using Boilerplate.Api.Hardening;
using Boilerplate.Application.Features.Accounts.TwoFactor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Two-factor sign-in for the caller's local account (ADR-0031). Needs a signed-in local account; changes also need the
/// current password and, except when turning it on, a current code. Limited like the other account endpoints.
/// </summary>
[ApiController]
[Route("api/v1/account/two-factor")]
[RequireAccountsEnabled]
[EnableRateLimiting(HardeningRegistration.AccountsRateLimitPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TwoFactorController : ControllerBase
{
    /// <summary>Whether two-factor sign-in is on, and how many recovery codes are left.</summary>
    [HttpGet]
    [ProducesResponseType<TwoFactorStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TwoFactorStatusResponse> Get([FromServices] GetTwoFactorStatusHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(cancellationToken);

    /// <summary>Creates a new authenticator key to scan or type into an authenticator app. Two-factor sign-in stays off.</summary>
    [HttpPost("setup")]
    [ProducesResponseType<AuthenticatorSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AuthenticatorSetupResponse> Setup([FromServices] SetupAuthenticatorHandler handler, CancellationToken cancellationToken) =>
        handler.HandleAsync(cancellationToken);

    /// <summary>Turns two-factor sign-in on with the password and a code from the new key; returns recovery codes once.</summary>
    [HttpPost("enable")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RecoveryCodesResponse> Enable(
        EnableTwoFactorRequest request,
        [FromServices] EnableTwoFactorHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Turns two-factor sign-in off with the password and a code or recovery code.</summary>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Disable(
        DisableTwoFactorRequest request,
        [FromServices] DisableTwoFactorHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces every recovery code; returns the new ones once.</summary>
    [HttpPost("recovery-codes")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RecoveryCodesResponse> RenewRecoveryCodes(
        RenewRecoveryCodesRequest request,
        [FromServices] RenewRecoveryCodesHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);
}
