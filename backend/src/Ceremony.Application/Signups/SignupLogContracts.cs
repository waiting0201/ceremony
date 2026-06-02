namespace Ceremony.Application.Signups;

public sealed record SignupLogItem(
    Guid Id,
    Guid SignupId,
    int Year,
    string? CeremonyCategoryTitle,
    int SignupType,
    string? NumberTitle,
    int? Number,
    string? HallName,
    string? Name,
    string? Phone,
    int? Fee,
    string?[] LivingNames,
    string?[] DeadNames,
    string? MailCity,
    string? MailZone,
    string? MailAddress,
    string? TextCity,
    string? TextZone,
    string? TextAddress,
    string? Remark,
    int? PrepayYear,
    string? PrepayCeremonyCategoryTitle,
    string? Admin,
    DateTime? CreateDate);

public sealed record SignupLogListResponse(IReadOnlyList<SignupLogItem> Items, int Total);

public interface ISignupLogRepository
{
    Task<IReadOnlyList<SignupLogItem>> GetBySignupIdAsync(Guid signupId, CancellationToken ct = default);
}
