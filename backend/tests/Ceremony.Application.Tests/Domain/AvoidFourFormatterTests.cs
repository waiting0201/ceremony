using Ceremony.Domain.Services;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

public sealed class AvoidFourFormatterTests
{
    [Theory]
    [InlineData(4, "3-1")]
    [InlineData(14, "13-1")]
    [InlineData(24, "23-1")]
    [InlineData(104, "103-1")]
    [InlineData(1234, "1233-1")]
    public void IndividualDigitFour_replaced(int n, string expected)
        => AvoidFourFormatter.Format(n).Should().Be(expected);

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(3, "3")]
    [InlineData(5, "5")]
    [InlineData(10, "10")]
    [InlineData(13, "13")]
    [InlineData(40, "40")]   // 個位是 0，不轉
    [InlineData(44, "43-1")] // 個位 4 轉
    [InlineData(444, "443-1")]
    public void NonFourLastDigit_unchanged(int n, string expected)
        => AvoidFourFormatter.Format(n).Should().Be(expected);

    [Fact]
    public void Negative_returns_as_is()
        => AvoidFourFormatter.Format(-4).Should().Be("-4");
}
