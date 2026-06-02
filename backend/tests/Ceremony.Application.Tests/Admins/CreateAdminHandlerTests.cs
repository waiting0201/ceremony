using Ceremony.Application.Admins;
using Ceremony.Application.Auth;
using Ceremony.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Admins;

/// <summary>
/// CreateAdminHandler 單元測試 — 對照 [post-admins.md 邊界 case 表](../../../../docs/blueprints/api-endpoints/post-admins.md)。
/// </summary>
public sealed class CreateAdminHandlerTests
{
    private readonly Mock<IAdminRepository> _repo = new();

    private CreateAdminHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task EmptyUsername_throws_VALIDATION_REQUIRED()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new CreateAdminRequest("", "pwd", null)));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
        ex.Message.Should().Be("請輸入帳號");
    }

    [Fact]
    public async Task WhitespaceUsername_is_trimmed_and_throws_VALIDATION_REQUIRED()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new CreateAdminRequest("   ", "pwd", null)));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
    }

    [Fact]
    public async Task EmptyPassword_throws_VALIDATION_REQUIRED()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new CreateAdminRequest("alice", "", null)));
        ex.ErrorCode.Should().Be("VALIDATION_REQUIRED");
        ex.Message.Should().Be("請輸入密碼");
    }

    [Fact]
    public async Task PasswordOver20Chars_throws_VALIDATION_LENGTH()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new CreateAdminRequest("alice", new string('x', 21), null)));
        ex.ErrorCode.Should().Be("VALIDATION_LENGTH");
        ex.Message.Should().Contain("20");
    }

    [Fact]
    public async Task DuplicateUsername_throws_ADMIN_DUPLICATE_USERNAME_with_verbatim_message()
    {
        _repo.Setup(r => r.UsernameExistsAsync("alice", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateSut().HandleAsync(new CreateAdminRequest("alice", "pwd", null)));
        ex.ErrorCode.Should().Be("ADMIN_DUPLICATE_USERNAME");
        ex.Message.Should().Be("帳號重複，請重新確認！");
        _repo.Verify(r => r.InsertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never, "重複時不應呼叫 Insert");
    }

    [Fact]
    public async Task ValidInput_inserts_and_returns_AdminListItem_with_new_id()
    {
        _repo.Setup(r => r.UsernameExistsAsync("alice", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.InsertAsync("alice", "secret", "Alice Wang", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await CreateSut().HandleAsync(new CreateAdminRequest("alice", "secret", "Alice Wang"));
        result.Id.Should().Be(42);
        result.Username.Should().Be("alice");
        result.Name.Should().Be("Alice Wang");
    }

    [Fact]
    public async Task TrimsAllFieldsBeforeProcessing()
    {
        _repo.Setup(r => r.UsernameExistsAsync("alice", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.InsertAsync("alice", "pwd", "Alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await CreateSut().HandleAsync(new CreateAdminRequest("  alice  ", "  pwd  ", "  Alice  "));
        result.Username.Should().Be("alice");
        result.Name.Should().Be("Alice");
    }
}
