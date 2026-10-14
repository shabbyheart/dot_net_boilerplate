using System.ComponentModel.DataAnnotations;
using Boilerplate.Application.Features.Invitations;
using Boilerplate.Application.Features.Invitations.CreateInvitation;
using Boilerplate.Application.Features.Tenants.CreateTenant;

namespace Boilerplate.UnitTests.Features.Invitations;

public sealed class InvitationValidationTests
{
    [Theory]
    [InlineData("not-an-address", false)]
    [InlineData("", false)]
    [InlineData("someone@example.com", true)]
    public void Invitations_need_an_email_address(string email, bool valid) =>
        new CreateInvitationValidator().Validate(new CreateInvitationRequest(email, Guid.NewGuid())).IsValid.ShouldBe(valid);

    [Fact]
    public void Invitations_need_a_role() =>
        new CreateInvitationValidator().Validate(new CreateInvitationRequest("someone@example.com", Guid.Empty)).IsValid.ShouldBeFalse();

    [Fact]
    public void Tenants_need_a_name_and_an_administrator_address()
    {
        new CreateTenantValidator().Validate(new CreateTenantRequest("Acme", "admin@example.com")).IsValid.ShouldBeTrue();
        new CreateTenantValidator().Validate(new CreateTenantRequest("", "admin@example.com")).IsValid.ShouldBeFalse();
        new CreateTenantValidator().Validate(new CreateTenantRequest("Acme", "admin")).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("https://app.example/accept", true)]
    [InlineData("/accept", false)]
    [InlineData("ftp://app.example/accept", false)]
    public void Accept_page_is_optional_but_must_be_an_absolute_http_url(string? acceptUrl, bool valid)
    {
        var settings = new InvitationSettings { AcceptUrl = acceptUrl };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true).ShouldBe(valid);
    }
}
