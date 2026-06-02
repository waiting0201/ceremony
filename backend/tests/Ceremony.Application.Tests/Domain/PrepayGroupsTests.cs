using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

public sealed class PrepayGroupsTests
{
    [Fact]
    public void All_contains_exactly_6_groups()
        => PrepayGroups.All.Should().HaveCount(6);

    [Theory]
    [InlineData(1, "非員工一般", 1, 1)]
    [InlineData(2, "地藏殿員工一般", 1, 3)]
    [InlineData(3, "寺方", 2, null)]
    [InlineData(4, "觀音會", 3, null)]
    [InlineData(5, "大殿員工郵撥", 5, 2)]
    [InlineData(6, "非員工郵撥", 5, 1)]
    public void Resolve_validCode_returns_expected(int code, string name, int signupType, int? employeeType)
    {
        var g = PrepayGroups.Resolve(code);
        g.Name.Should().Be(name);
        g.SignupType.Should().Be(signupType);
        g.EmployeeType.Should().Be(employeeType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Resolve_invalidCode_throws(int code)
    {
        var act = () => PrepayGroups.Resolve(code);
        act.Should().Throw<DomainException>()
            .Where(e => e.ErrorCode == "VALIDATION_INVALID" && e.Message == "無效的信眾類別");
    }
}
