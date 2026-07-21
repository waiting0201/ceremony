using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Signups;

/// <summary>
/// 新增報名（含 UPDLOCK 編號分配 + SignupLog 同步寫入）。
/// </summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:151-362 (btnConfirm_Click)
/// Blueprint: docs/blueprints/api-endpoints/post-signups.md
/// Coverage:  docs/blueprints/legacy-coverage/new-signup-form.md (rows 6, 14-18, 25)
/// </remarks>
public sealed class CreateSignupHandler(
    ISignupRepository signupRepo,
    IBelieverRepository believerRepo)
{
    public async Task<SignupListItem> HandleAsync(CreateSignupRequest req, CallerContext caller, CancellationToken ct = default)
    {
        // 驗證 + normalize
        var numberTitle = NumberTitleResolver.Resolve(req.SignupType);
        var name = Trim(req.Name) ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入姓名");
        // 地址非必填（2026-07-21 使用者指定）：空白即存空字串，不再擋下。
        var mailAddress = Trim(req.MailAddress) ?? string.Empty;

        int? explicitNumber = null;
        if (req.KeepNumber)
        {
            if (req.CustomNumber is not { } cn || cn <= 0)
                throw new DomainException("VALIDATION_REQUIRED", "請輸入編號");

            if (await signupRepo.NumberExistsAsync(req.Year, req.CeremonyCategoryId, req.SignupType, cn, ct))
            {
                var typeName = NumberTitleResolver.SignupTypeName(req.SignupType);
                var title = await signupRepo.GetCeremonyCategoryTitleAsync(req.CeremonyCategoryId, ct);
                throw new DomainException("SIGNUP_NUMBER_CONFLICT", $"{req.Year} {title} {typeName} 編號重複，請重新確認！");
            }
            explicitNumber = cn;
        }

        // 驗證 BelieverID 存在（取得 Name 同時驗證）
        var believerName = await believerRepo.GetNameAsync(req.BelieverId, ct);
        if (believerName is null)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");

        // 取得 CeremonyCategory title（給 SignupLog 快照用）
        var ceremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(req.CeremonyCategoryId, ct);

        // PrepayCeremonyCategory title（optional）
        string? prepayCeremonyTitle = null;
        if (req.PrepayCeremonyCategoryId is { } pc)
            prepayCeremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(pc, ct);

        // TextAddress fallback：空時 copy MailAddress；同理 TextZipcodeId
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
            MailCity: null,   // City/Zone 快照原舊系統取自 dropdown text，新版若需要可從 Zipcodes 查
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

        await signupRepo.InsertWithLogAsync(signupModel, logModel, explicitNumber, ct);

        // 回讀寫入結果（含 SignupView 的計算欄位）
        var created = await signupRepo.GetByIdAsync(signupId, ct);
        return created ?? throw new DomainException("INTERNAL_ERROR", "新增後無法讀回該筆報名");
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
            // 不 trim 開頭/結尾空白：使用者用開頭全形空格（U+3000）等把名字往下推作直書排版，必須保留
            // （render 路徑不 trim，GroupFontPt 也照原樣算列）。僅「純空白/空字串」視為無名字 → null
            // （與 PrintTemplateSelector.IsPresent 的 IsNullOrWhiteSpace 一致）。
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
