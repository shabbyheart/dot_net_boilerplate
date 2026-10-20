using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Configurations;

/// <summary>
/// Part of Identity's schema. Holds authenticator keys and hashed recovery codes for two-factor sign-in (ADR-0031).
/// E-mailed tokens are <c>one_time_tokens</c>, refresh tokens are <c>refresh_tokens</c> (ADR-0025).
/// </summary>
internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Property(token => token.Name).HasMaxLength(128);
        builder.Property(token => token.Value).HasColumnType("text");
    }
}
