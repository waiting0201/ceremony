using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Signups;

/// <summary>
/// 插入報名於指定編號，並把該群組 (Year, CeremonyCategoryID, SignupType) 內 Number &gt;= 指定編號的
/// 既有報名整批 +1 順移。對應「報名維護列表右鍵 → 在此前插入」。
/// </summary>
/// <remarks>
/// Legacy: 無對應（舊 NewSignupForm 只能自動 MAX+1 或手動指定空號，指定已佔用號會被擋）。新版增強。
/// Blueprint: docs/blueprints/api-endpoints/post-signups-insert-shift.md
/// 欄位 normalize/建 model 與 CreateSignupHandler 一致，差別：編號必填、不做重複檢查、走 InsertWithShiftAsync。
/// </remarks>
public sealed class InsertShiftSignupHandler(
    ISignupRepository signupRepo,
    IBelieverRepository believerRepo)
{
    public async Task<SignupListItem> HandleAsync(CreateSignupRequest req, CallerContext caller, CancellationToken ct = default)
    {
        // 插入位置編號：必填、> 0（沿用 keepNumber 路徑的 CustomNumber 當插入位置）。刻意不做重複檢查。
        if (req.CustomNumber is not { } insertNumber || insertNumber <= 0)
            throw new DomainException("VALIDATION_REQUIRED", "請輸入編號");

        var numberTitle = NumberTitleResolver.Resolve(req.SignupType);
        var name = Trim(req.Name) ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入姓名");
        var mailAddress = Trim(req.MailAddress) ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入寄件地址");

        // 驗證 BelieverID 存在
        var believerName = await believerRepo.GetNameAsync(req.BelieverId, ct);
        if (believerName is null)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");

        // CeremonyCategory title（給 SignupLog 快照；同時驗證法會存在）
        var ceremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(req.CeremonyCategoryId, ct)
            ?? throw new DomainException("CATEGORY_NOT_FOUND", "找不到法會");

        string? prepayCeremonyTitle = null;
        if (req.PrepayCeremonyCategoryId is { } pc)
            prepayCeremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(pc, ct);

        var textAddress = string.IsNullOrWhiteSpace(req.TextAddress) ? mailAddress : req.TextAddress.Trim();
        var textZipcodeId = req.TextZipcodeId ?? (string.IsNullOrWhiteSpace(req.TextAddress) ? req.MailZipcodeId : null);

        var phone = ToNarrow(Trim(req.Phone));
        var hallName = Trim(req.HallName);
        var remark = Trim(req.Remark);
        var livingNames = NormalizeNames(req.LivingNames);
        var deadNames = NormalizeNames(req.DeadNames);

        var signupId = Guid.NewGuid();
        var createDate = DateTime.UtcNow;

        var signupModel = new SignupWriteModel(
            SignupId: signupId,
            Year: req.Year,
            CeremonyCategoryId: req.CeremonyCategoryId,
            SignupType: req.SignupType,
            BelieverId: req.BelieverId,
            NumberTitle: numberTitle,
            Fee: req.Fee,
            Name: name,
            Phone: phone,
            LivingNames: livingNames,
            DeadNames: deadNames,
            MailZipcodeId: NormalizeZipcode(req.MailZipcodeId),
            MailAddress: mailAddress,
            TextZipcodeId: NormalizeZipcode(textZipcodeId),
            TextAddress: textAddress,
            Remark: remark,
            PrepayYear: req.PrepayYear,
            PrepayCeremonyCategoryId: req.PrepayCeremonyCategoryId,
            AdminId: caller.AdminId,
            CreateDate: createDate);

        var logModel = new SignupLogWriteModel(
            SignupLogId: Guid.NewGuid(),
            SignupId: signupId,
            Year: req.Year,
            CeremonyCategoryTitle: ceremonyTitle,
            SignupType: req.SignupType,
            HallName: hallName,
            Name: name,
            Phone: phone,
            NumberTitle: numberTitle,
            Fee: req.Fee,
            LivingNames: livingNames,
            DeadNames: deadNames,
            MailCity: null,
            MailZone: null,
            MailAddress: mailAddress,
            TextCity: null,
            TextZone: null,
            TextAddress: textAddress,
            Remark: remark,
            PrepayYear: req.PrepayYear,
            PrepayCeremonyCategoryTitle: prepayCeremonyTitle,
            Admin: caller.AdminName,
            CreateDate: createDate);

        await signupRepo.InsertWithShiftAsync(signupModel, logModel, insertNumber, ct);

        var created = await signupRepo.GetByIdAsync(signupId, ct);
        return created ?? throw new DomainException("INTERNAL_ERROR", "插入後無法讀回該筆報名");
    }

    private static string? Trim(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? NormalizeZipcode(int? value)
        => value is null or -1 ? null : value;

    private static string?[] NormalizeNames(IReadOnlyList<string?>? names)
    {
        names ??= [null, null, null, null, null, null];
        if (names.Count != 6)
            throw new DomainException("VALIDATION_INVALID", "名單必須為 6 個元素");
        var result = new string?[6];
        for (var i = 0; i < 6; i++)
        {
            var v = names[i];
            result[i] = string.IsNullOrWhiteSpace(v) ? null : v;
        }
        return result;
    }

    private static string? ToNarrow(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return new string(s.Select(c => c switch
        {
            >= '！' and <= '～' => (char)(c - 0xFEE0),
            '　' => ' ',
            _ => c,
        }).ToArray());
    }
}
