using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ceremony.Application.Auth;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Auth;

public sealed class LogoutHandlerTests
{
    private readonly Mock<IJwtBlacklist> _blacklist = new();

    private LogoutHandler Sut() => new(_blacklist.Object);

    private static ClaimsPrincipal MakeUser(string? jti, long? expUnix)
    {
        var claims = new List<Claim>();
        if (jti is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        if (expUnix is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Exp, expUnix.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    [Fact]
    public void Missing_jti_does_nothing()
    {
        Sut().Handle(MakeUser(jti: null, expUnix: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()));
        _blacklist.Verify(b => b.Revoke(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void Expired_token_is_not_blacklisted()
    {
        Sut().Handle(MakeUser(jti: "abc", expUnix: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()));
        _blacklist.Verify(b => b.Revoke(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void Valid_token_is_revoked_with_remaining_ttl()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
        TimeSpan? capturedTtl = null;
        _blacklist.Setup(b => b.Revoke("abc", It.IsAny<TimeSpan>()))
                  .Callback<string, TimeSpan>((_, ttl) => capturedTtl = ttl);

        Sut().Handle(MakeUser(jti: "abc", expUnix: exp));

        _blacklist.Verify(b => b.Revoke("abc", It.IsAny<TimeSpan>()), Times.Once);
        capturedTtl.Should().NotBeNull();
        capturedTtl!.Value.TotalSeconds.Should().BeInRange(19 * 60, 21 * 60);
    }

    [Fact]
    public void Missing_exp_falls_back_to_default_ttl()
    {
        TimeSpan? capturedTtl = null;
        _blacklist.Setup(b => b.Revoke("abc", It.IsAny<TimeSpan>()))
                  .Callback<string, TimeSpan>((_, ttl) => capturedTtl = ttl);

        Sut().Handle(MakeUser(jti: "abc", expUnix: null));

        _blacklist.Verify(b => b.Revoke("abc", It.IsAny<TimeSpan>()), Times.Once);
        capturedTtl.Should().Be(TimeSpan.FromMinutes(600));
    }
}

public sealed class MemoryJwtBlacklistTests
{
    [Fact]
    public void IsRevoked_returns_true_after_Revoke()
    {
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var sut = new MemoryJwtBlacklist(cache);

        sut.IsRevoked("abc").Should().BeFalse();
        sut.Revoke("abc", TimeSpan.FromMinutes(10));
        sut.IsRevoked("abc").Should().BeTrue();
    }

    [Fact]
    public void Empty_jti_is_not_stored()
    {
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var sut = new MemoryJwtBlacklist(cache);

        sut.Revoke("", TimeSpan.FromMinutes(10));
        sut.IsRevoked("").Should().BeFalse();
    }

    [Fact]
    public void Zero_or_negative_ttl_is_no_op()
    {
        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var sut = new MemoryJwtBlacklist(cache);

        sut.Revoke("abc", TimeSpan.Zero);
        sut.IsRevoked("abc").Should().BeFalse();
        sut.Revoke("abc", TimeSpan.FromSeconds(-5));
        sut.IsRevoked("abc").Should().BeFalse();
    }
}
