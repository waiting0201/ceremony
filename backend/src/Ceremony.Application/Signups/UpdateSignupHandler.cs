using System.Security.Cryptography;
using Ceremony.Application.Believers;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Signups;

/// <summary>
/// 編輯既有報名（全欄位覆寫 + 同步 SignupLog + 部分 Believer 欄位更新）。
/// </summary>
/// <remarks>
/// Legacy: EditSignupForm.cs:186-368 (btnConfirm_Click)
/// Blueprint: docs/blueprints/api-endpoints/put-signup.md
/// Coverage:  docs/blueprints/legacy-coverage/edit-signup-form.md (rows 9-13)
/// </remarks>
public sealed class UpdateSignupHandler(
    ISignupRepository signupRepo,
    IBelieverRepository believerRepo)
{
    public async Task<SignupListItem> HandleAsync(Guid signupId, CreateSignupRequest req, CallerContext caller, CancellationToken ct = default)
    {
        // 驗證
        var numberTitle = NumberTitleResolver.Resolve(req.SignupType);
        var name = Trim(req.Name) ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入姓名");
        var mailAddress = Trim(req.MailAddress) ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入寄件地址");
        if (req.Year <= 0)
            throw new DomainException("VALIDATION_REQUIRED", "請輸入年份");
        if (req.CeremonyCategoryId == Guid.Empty)
            throw new DomainException("VALIDATION_REQUIRED", "請選擇法會");

        // 編輯時 number 必填且必過重複檢查（排除自己）
        var number = req.CustomNumber
            ?? throw new DomainException("VALIDATION_REQUIRED", "請輸入編號");
        if (number <= 0)
            throw new DomainException("VALIDATION_REQUIRED", "請輸入編號");

        if (await signupRepo.NumberExistsExcludingAsync(req.Year, req.CeremonyCategoryId, req.SignupType, number, signupId, ct))
        {
            throw new DomainException("SIGNUP_NUMBER_CONFLICT", $"{req.Year}年編號{number}重複，請重新確認！");
        }

        // 信眾必須存在
        var believerName = await believerRepo.GetNameAsync(req.BelieverId, ct);
        if (believerName is null)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");

        var ceremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(req.CeremonyCategoryId, ct);
        string? prepayCeremonyTitle = null;
        if (req.PrepayCeremonyCategoryId is { } pc)
            prepayCeremonyTitle = await signupRepo.GetCeremonyCategoryTitleAsync(pc, ct);

        var phone = ToNarrow(Trim(req.Phone));
        var hallName = Trim(req.HallName);
        var remark = Trim(req.Remark);
        var textAddress = string.IsNullOrWhiteSpace(req.TextAddress) ? mailAddress : req.TextAddress.Trim();
        var textZipcodeId = req.TextZipcodeId ?? (string.IsNullOrWhiteSpace(req.TextAddress) ? req.MailZipcodeId : null);
        var livingNames = NormalizeNames(req.LivingNames);
        var deadNames = NormalizeNames(req.DeadNames);
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

        var updated = await signupRepo.UpdateWithLogAsync(
            signupModel, logModel, number,
            hallNameForBeliever: hallName,
            employeeTypeForBeliever: null,        // EmployeeType 不在 request；用 null 保留既有
            isFixedNumberForBeliever: null,       // IsFixedNumber 不在 request；用 null 保留既有
            ct);

        if (!updated)
            throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return await signupRepo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("INTERNAL_ERROR", "更新後無法讀回該筆報名");
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static int? NormalizeZipcode(int? v) => v is null or -1 ? null : v;

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
