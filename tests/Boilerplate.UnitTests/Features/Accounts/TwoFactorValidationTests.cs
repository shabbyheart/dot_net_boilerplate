using Boilerplate.Application.Features.Accounts;
using Boilerplate.Application.Features.Accounts.Login;
using Boilerplate.Application.Features.Accounts.TwoFactor;

namespace Boilerplate.UnitTests.Features.Accounts;

public sealed class TwoFactorValidationTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public void Recovery_codes_are_distinct_groups_of_base32_characters()
    {
        var codes = RecoveryCodes.Create();

        codes.Count.ShouldBe(RecoveryCodes.Count);
        codes.Distinct().Count().ShouldBe(RecoveryCodes.Count);
        codes.ShouldAllBe(code => System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z2-7]{4}(-[A-Z2-7]{4}){3}$"));
    }

    [Fact]
    public void Recovery_code_hashes_ignore_case_spaces_and_dashes()
    {
        var hash = RecoveryCodes.Hash("ABCD-EFGH-IJKL-MNOP");

        RecoveryCodes.Hash("abcd efgh ijkl mnop").ShouldBe(hash);
        RecoveryCodes.Hash("ABCDEFGHIJKLMNOP").ShouldBe(hash);
        RecoveryCodes.Hash("ABCD-EFGH-IJKL-MNOQ").ShouldNotBe(hash);
        hash.ShouldNotContain("ABCD");
    }

    [Fact]
    public void Second_factor_prefers_the_code_and_hashes_recovery_codes()
    {
        SecondFactor.From(null, null).ShouldBeNull();
        SecondFactor.From("123456", null).ShouldBe(new SecondFactor("123456", null));
        SecondFactor.From(null, "abcd-efgh-ijkl-mnop").ShouldBe(new SecondFactor(null, RecoveryCodes.Hash("ABCD-EFGH-IJKL-MNOP")));
    }

    [Theory]
    [InlineData("123456", null, true)]
    [InlineData("123 456", null, true)]
    [InlineData(null, "ABCD-EFGH-IJKL-MNOP", true)]
    [InlineData("12a456", null, false)]
    [InlineData("123456", "ABCD-EFGH-IJKL-MNOP", false)]
    public void Login_accepts_at_most_one_well_formed_second_factor(string? code, string? recoveryCode, bool valid) =>
        new LoginValidator().Validate(new LoginRequest("a@b.example", Password, null, code, recoveryCode)).IsValid.ShouldBe(valid);

    [Theory]
    [InlineData("123456", null, true)]
    [InlineData(null, "ABCD-EFGH-IJKL-MNOP", true)]
    [InlineData(null, null, false)]
    [InlineData("123456", "ABCD-EFGH-IJKL-MNOP", false)]
    public void Disabling_needs_exactly_one_second_factor(string? code, string? recoveryCode, bool valid) =>
        new DisableTwoFactorValidator().Validate(new DisableTwoFactorRequest(Password, code, recoveryCode)).IsValid.ShouldBe(valid);

    [Fact]
    public void Enabling_needs_a_password_and_a_code()
    {
        new EnableTwoFactorValidator().Validate(new EnableTwoFactorRequest(Password, "123456")).IsValid.ShouldBeTrue();
        new EnableTwoFactorValidator().Validate(new EnableTwoFactorRequest("", "123456")).IsValid.ShouldBeFalse();
        new EnableTwoFactorValidator().Validate(new EnableTwoFactorRequest(Password, "")).IsValid.ShouldBeFalse();
    }
}
