using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using FluentAssertions;

namespace Ceremony.Application.Tests.Believers;

/// <summary>
/// 共用驗證器測試 — 涵蓋 POST + PUT 共用的 normalize/validation 路徑。
/// </summary>
public sealed class BelieverWriteValidatorTests
{
    private static BelieverUpsertRequest ValidReq() => new(
        EmployeeType: 1,
        Name: "Alice",
        MailAddress: "台北市信義區市府路 1 號");

    [Fact]
    public void EmptyName_throws_with_verbatim_message()
    {
        var act = () => BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { Name = "" });
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入姓名");
    }

    [Fact]
    public void EmptyMailAddress_throws_with_verbatim_message()
    {
        var act = () => BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { MailAddress = "  " });
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_REQUIRED" && e.Message == "請輸入寄件地址");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void InvalidEmployeeType_throws(int t)
    {
        var act = () => BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { EmployeeType = t });
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "員工類別錯誤");
    }

    [Fact]
    public void NameOver30Chars_throws_VALIDATION_LENGTH()
    {
        var act = () => BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { Name = new string('A', 31) });
        act.Should().Throw<DomainException>().Where(e => e.ErrorCode == "VALIDATION_LENGTH");
    }

    [Fact]
    public void Phone_fullWidth_converts_to_narrow()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { Phone = "０９１２" });
        result.Phone.Should().Be("0912");
    }

    [Fact]
    public void Phone_mixedFullAndHalf_converts_consistently()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { Phone = "０9１2-3456" });
        result.Phone.Should().Be("0912-3456");
    }

    [Fact]
    public void Zipcode_minusOne_normalizes_to_null()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(ValidReq() with { MailZipcodeId = -1, TextZipcodeId = -1 });
        result.MailZipcodeId.Should().BeNull();
        result.TextZipcodeId.Should().BeNull();
    }

    [Fact]
    public void NullNames_default_to_six_nulls()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(ValidReq());
        result.LivingNames.Should().HaveCount(6).And.AllSatisfy(x => x.Should().BeNull());
        result.DeadNames.Should().HaveCount(6).And.AllSatisfy(x => x.Should().BeNull());
    }

    [Fact]
    public void Names_wrongLength_throws()
    {
        var act = () => BelieverWriteValidator.ValidateAndNormalize(
            ValidReq() with { LivingNames = ["a", "b"] });
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message.Contains("6"));
    }

    [Fact]
    public void Names_emptyStrings_normalized_to_nulls()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(
            ValidReq() with { LivingNames = ["Alice", "  ", "", null, "Bob", ""] });
        result.LivingNames[0].Should().Be("Alice");
        result.LivingNames[1].Should().BeNull();
        result.LivingNames[2].Should().BeNull();
        result.LivingNames[3].Should().BeNull();
        result.LivingNames[4].Should().Be("Bob");
        result.LivingNames[5].Should().BeNull();
    }

    [Fact]
    public void TrimsAllFields()
    {
        var result = BelieverWriteValidator.ValidateAndNormalize(ValidReq() with
        {
            Name = "  Alice  ",
            MailAddress = "  addr  ",
            HallName = "  hn  ",
            TextAddress = "  text  ",
        });
        result.Name.Should().Be("Alice");
        result.MailAddress.Should().Be("addr");
        result.HallName.Should().Be("hn");
        result.TextAddress.Should().Be("text");
    }
}
