using Boilerplate.Api.Hardening;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Accounts.ConfirmEmail;
using Boilerplate.Application.Features.Accounts.ForgotPassword;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.Logout;
using Boilerplate.Application.Features.Accounts.Refresh;
using Boilerplate.Application.Features.Accounts.Register;
using Boilerplate.Application.Features.Accounts.ResetPassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Boilerplate.Api.Controllers;

/// <summary>
/// Local accounts (ADR-0025): registration, e-mail confirmation, sign-in, token refresh and password reset.
/// Anonymous by nature; limited per client IP on top of the general rate limit. 404 unless accounts are enabled.
/// </summary>
[ApiController]
[Route("api/v1/account")]
[AllowAnonymous]
[RequireAccountsEnabled]
[EnableRateLimiting(HardeningRegistration.AccountsRateLimitPolicy)]
public sealed class AccountController : ControllerBase
{
    /// <summary>Creates an account and e-mails a confirmation link. Same response for addresses that already have one.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AcceptedResult> Register(
        RegisterRequest request,
        [FromServices] RegisterHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return Accepted();
    }

    /// <summary>Confirms the e-mail address with the token from the confirmation link.</summary>
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<NoContentResult> ConfirmEmail(
        ConfirmEmailRequest request,
        [FromServices] ConfirmEmailHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Signs in. With a tenant id, the access token acts for that tenant (the user must hold a role in it).</summary>
    [HttpPost("login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<TokenResponse> Login(
        LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Exchanges a refresh token for a new token pair. Each refresh token works once.</summary>
    [HttpPost("refresh")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<TokenResponse> Refresh(
        RefreshTokenRequest request,
        [FromServices] RefreshHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, cancellationToken);

    /// <summary>Ends the session of the refresh token. Access tokens stay valid until they expire.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<NoContentResult> Logout(
        RefreshTokenRequest request,
        [FromServices] LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>E-mails a password reset link if the address has a confirmed account. Same response either way.</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AcceptedResult> ForgotPassword(
        ForgotPasswordRequest request,
        [FromServices] ForgotPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return Accepted();
    }

    /// <summary>Sets a new password with the token from the reset link and signs out every session.</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<NoContentResult> ResetPassword(
        ResetPasswordRequest request,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request, cancellationToken);
        return NoContent();
    }
}
