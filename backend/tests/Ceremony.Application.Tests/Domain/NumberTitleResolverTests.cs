using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

public sealed class NumberTitleResolverTests
{
    [Theory]
    [InlineData(1, "No")]
    [InlineData(2, "寺")]
    [InlineData(3, "觀")]
    [InlineData(4, "普")]
    [InlineData(5, "郵")]
    public void Resolve_validTypes(int type, string expected)
        => NumberTitleResolver.Resolve(type).Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(99)]
    public void Resolve_invalidType_throws(int type)
    {
        var act = () => NumberTitleResolver.Resolve(type);
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "報名類型錯誤");
    }

    [Theory]
    [InlineData(1, "一般")]
    [InlineData(2, "寺方")]
    [InlineData(3, "觀音會")]
    [InlineData(4, "普桌")]
    [InlineData(5, "郵撥")]
    public void SignupTypeName_known(int type, string expected)
        => NumberTitleResolver.SignupTypeName(type).Should().Be(expected);
}
