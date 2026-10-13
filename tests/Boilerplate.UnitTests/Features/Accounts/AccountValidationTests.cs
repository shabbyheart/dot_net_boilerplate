using System.ComponentModel.DataAnnotations;
using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.Register;

namespace Boilerplate.UnitTests.Features.Accounts;

public sealed class AccountValidationTests
{
    [Theory]
    [InlineData("eleven char")]
    [InlineData("")]
    public void Register_requires_at_least_twelve_characters(string password) =>
        new RegisterValidator().Validate(new RegisterRequest("user@example.com", password)).IsValid.ShouldBeFalse();

    [Fact]
    public void Register_accepts_a_long_passphrase_without_composition_rules() =>
        new RegisterValidator().Validate(new RegisterRequest("user@example.com", "all lowercase words")).IsValid.ShouldBeTrue();

    [Fact]
    public void Register_bounds_the_password_length() =>
        new RegisterValidator()
            .Validate(new RegisterRequest("user@example.com", new string('p', AccountRules.PasswordMaxLength + 1)))
            .IsValid.ShouldBeFalse();

    [Fact]
    public void Login_does_not_apply_the_new_password_rules() =>
        new LoginValidator().Validate(new LoginRequest("user@example.com", "short", TenantId: null)).IsValid.ShouldBeTrue();

    [Fact]
    public void Settings_need_absolute_links_only_when_accounts_are_enabled()
    {
        var disabled = new AccountSettings();
        var enabledWithoutLinks = new AccountSettings { Enabled = true, PasswordResetUrl = "/relative" };
        var enabled = new AccountSettings
        {
            Enabled = true,
            EmailConfirmationUrl = "https://app.example/confirm",
            PasswordResetUrl = "https://app.example/reset",
        };

        Errors(disabled).ShouldBeEmpty();
        Errors(enabledWithoutLinks).ShouldBe([nameof(AccountSettings.EmailConfirmationUrl), nameof(AccountSettings.PasswordResetUrl)]);
        Errors(enabled).ShouldBeEmpty();
    }

    private static string[] Errors(AccountSettings settings)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);
        return [.. results.SelectMany(result => result.MemberNames)];
    }
}
