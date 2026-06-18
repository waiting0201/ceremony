using Ceremony.Application.Auth;
using Ceremony.Domain.Entities;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;

namespace Ceremony.Application.Tests.Auth;

/// <summary>
/// LoginHandler 單元測試 — 對照 [post-auth-login.md 邊界 case 表](../../../../docs/blueprints/api-endpoints/post-auth-login.md)
/// 與 [login-form.md row 2-3](../../../../docs/blueprints/legacy-coverage/login-form.md)。
/// </summary>
public sealed class LoginHandlerTests
{
    private readonly Mock<IAdminRepository> _repo = new();
    private readonly AuthOptions _authOpts = new()
    {
        SuperAdminEnabled = true,
        SuperAdminUsername = "sa@system.local",
        SuperAdminPassword = "Admin@123",
        FailedLoginThreshold = 5,
        FailedLoginLockMinutes = 15,
    };
    private readonly JwtOptions _jwtOpts = new()
    {
        Issuer = "test",
        Audience = "test",
        SigningKey = new string('K', 64),
        AccessTokenMinutes = 30,
    };

    private LoginHandler CreateSut()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var jwt = new JwtTokenService(Options.Create(_jwtOpts));
        var failures = new LoginFailureTracker(cache);
        return new LoginHandler(_repo.Object, jwt, failures, Options.Create(_authOpts));
    }

    [Fact]
    public async Task EmptyUsername_throws_VALIDATION_REQUIRED_with_verbatim_message()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("", "x")));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
        ex.Message.Should().Be("請輸入帳號！");
    }

    [Fact]
    public async Task EmptyPassword_throws_VALIDATION_REQUIRED_with_verbatim_message()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("alice", "")));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
        ex.Message.Should().Be("請輸入密碼！");
    }

    [Fact]
    public async Task UnknownUser_throws_AUTH_INVALID_CREDENTIALS()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Admin?)null);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("alice", "x")));
        ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        ex.Message.Should().Be("帳號或密碼錯誤！");
    }

    [Fact]
    public async Task DisabledUser_throws_AUTH_INVALID_CREDENTIALS_without_leaking_existence()
    {
        _repo.Setup(r => r.GetByUsernameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 1, Username = "bob", Password = "x", IsEnabled = false });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("bob", "x")));
        ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task SuperAdmin_returns_token_with_adminId_0()
    {
        var result = await CreateSut().HandleAsync(new LoginRequest("sa@system.local", "Admin@123"));
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.User.Id.Should().Be(0);
        result.User.Username.Should().Be("sa@system.local");
        result.User.Name.Should().Be("Administrator");
        _repo.Verify(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "backdoor 不應該打 DB");
    }

    [Fact]
    public async Task SuperAdmin_disabled_falls_through_to_DB_and_fails()
    {
        _authOpts.SuperAdminEnabled = false;
        _repo.Setup(r => r.GetByUsernameAsync("sa@system.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Admin?)null);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("sa@system.local", "Admin@123")));
        ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task WrongPassword_for_valid_user_throws_AUTH_INVALID_CREDENTIALS()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 1, Username = "alice", Password = "correct", IsEnabled = true });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("alice", "wrong")));
        ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task CorrectCredentials_returns_token_with_db_admin_info()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 7, Username = "alice", Password = "secret", Name = "Alice Wang", IsEnabled = true });

        var result = await CreateSut().HandleAsync(new LoginRequest("alice", "secret"));
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.User.Id.Should().Be(7);
        result.User.Username.Should().Be("alice");
        result.User.Name.Should().Be("Alice Wang");
    }

    [Fact]
    public async Task LockoutAfterThreshold_throws_AUTH_ACCOUNT_LOCKED_with_minutes_in_message()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 1, Username = "alice", Password = "correct", IsEnabled = true });

        var sut = CreateSut();
        // 4 個失敗仍是 AUTH_INVALID_CREDENTIALS
        for (var i = 0; i < 4; i++)
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                sut.HandleAsync(new LoginRequest("alice", "wrong")));
            ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        }

        // 第 5 次（達門檻）變 LOCKED
        var locked = await Assert.ThrowsAsync<DomainException>(() =>
            sut.HandleAsync(new LoginRequest("alice", "wrong")));
        locked.ErrorCode.Should().Be("AUTH_ACCOUNT_LOCKED");
        locked.Message.Should().Contain("15 分鐘");
    }

    [Fact]
    public async Task SuccessfulLogin_resets_failure_counter()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 1, Username = "alice", Password = "correct", IsEnabled = true });

        var sut = CreateSut();
        // 累積 3 次失敗
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<DomainException>(() =>
                sut.HandleAsync(new LoginRequest("alice", "wrong")));
        }

        // 成功一次 → counter 應重置
        await sut.HandleAsync(new LoginRequest("alice", "correct"));

        // 再失敗 4 次，應仍是 AUTH_INVALID_CREDENTIALS（沒有被前面的 3 次累加）
        for (var i = 0; i < 4; i++)
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                sut.HandleAsync(new LoginRequest("alice", "wrong")));
            ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
        }
    }

    [Fact]
    public async Task PasswordComparison_is_case_sensitive()
    {
        _repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Admin { AdminId = 1, Username = "alice", Password = "Secret", IsEnabled = true });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new LoginRequest("alice", "secret")));
        ex.ErrorCode.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
