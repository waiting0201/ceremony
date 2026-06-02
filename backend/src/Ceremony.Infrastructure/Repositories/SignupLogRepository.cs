using Ceremony.Application.Signups;
using Ceremony.Infrastructure.Persistence;
using Dapper;

namespace Ceremony.Infrastructure.Repositories;

/// <summary>
/// Dapper-based 報名變更紀錄存取。
/// </summary>
/// <remarks>
/// Legacy: SignupLogForm.cs (LoadSignupLog) + NewSignupForm.cs:309-348 (insert in btnConfirm)
/// </remarks>
public sealed class SignupLogRepository(IDbConnectionFactory factory) : ISignupLogRepository
{
    public async Task<IReadOnlyList<SignupLogItem>> GetBySignupIdAsync(Guid signupId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT SignupLogID, SignupID, Year, CeremonyCategoryTitle, SignupType,
                   HallName, Name, Phone, NumberTitle, Number, Fee,
                   LivingNameOne, LivingNameTwo, LivingNameThree, LivingNameFour, LivingNameFive, LivingNameSix,
                   DeadNameOne,   DeadNameTwo,   DeadNameThree,   DeadNameFour,   DeadNameFive,   DeadNameSix,
                   MailCity, MailZone, MailAddress,
                   TextCity, TextZone, TextAddress,
                   Remark, PrepayYear, PrepayCeremonyCategoryTitle,
                   Admin, Createdate
            FROM dbo.SignupLogs
            WHERE SignupID = @SignupId
            ORDER BY Createdate DESC
            """;

        await using var conn = await factory.CreateOpenAsync(ct);
        var rows = await conn.QueryAsync(new CommandDefinition(sql, new { SignupId = signupId }, cancellationToken: ct));

        var list = new List<SignupLogItem>();
        foreach (var r in rows)
        {
            var d = (IDictionary<string, object?>)r;
            list.Add(new SignupLogItem(
                Id: (Guid)d["SignupLogID"]!,
                SignupId: (Guid)d["SignupID"]!,
                Year: (int)d["Year"]!,
                CeremonyCategoryTitle: d["CeremonyCategoryTitle"] as string,
                SignupType: (int)d["SignupType"]!,
                NumberTitle: d["NumberTitle"] as string,
                Number: d["Number"] as int?,
                HallName: d["HallName"] as string,
                Name: d["Name"] as string,
                Phone: d["Phone"] as string,
                Fee: d["Fee"] as int?,
                LivingNames:
                [
                    d["LivingNameOne"] as string, d["LivingNameTwo"] as string, d["LivingNameThree"] as string,
                    d["LivingNameFour"] as string, d["LivingNameFive"] as string, d["LivingNameSix"] as string,
                ],
                DeadNames:
                [
                    d["DeadNameOne"] as string, d["DeadNameTwo"] as string, d["DeadNameThree"] as string,
                    d["DeadNameFour"] as string, d["DeadNameFive"] as string, d["DeadNameSix"] as string,
                ],
                MailCity: d["MailCity"] as string,
                MailZone: d["MailZone"] as string,
                MailAddress: d["MailAddress"] as string,
                TextCity: d["TextCity"] as string,
                TextZone: d["TextZone"] as string,
                TextAddress: d["TextAddress"] as string,
                Remark: d["Remark"] as string,
                PrepayYear: d["PrepayYear"] as int?,
                PrepayCeremonyCategoryTitle: d["PrepayCeremonyCategoryTitle"] as string,
                Admin: d["Admin"] as string,
                CreateDate: d["Createdate"] as DateTime?));
        }
        return list;
    }
}
