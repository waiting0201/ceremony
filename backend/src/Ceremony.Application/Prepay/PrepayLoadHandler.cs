using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;
using Ceremony.Domain.Services;

namespace Ceremony.Application.Prepay;

/// <summary>
/// 預繳載入。把 sourceYear/sourceCeremony 的預繳資料載入到 targetYear/targetCeremony。
/// </summary>
/// <remarks>
/// Legacy: LoadPrepayForm.cs:45-824 (btnConfirm_Click 780-line switch)
/// Blueprint: docs/blueprints/api-endpoints/post-prepay-load.md
/// Coverage:  docs/blueprints/legacy-coverage/load-prepay-form.md (rows 2, 3)
/// 6 cases 重構為 data-driven strategy table (Domain.Services.PrepayGroups)。
/// </remarks>
public sealed class PrepayLoadHandler(IPrepayRepository repo)
{
    public async Task<PrepayLoadResponse> HandleAsync(
        PrepayLoadRequest req,
        CallerContext caller,
        CancellationToken ct = default)
    {
        // 1. 驗證
        if (req.TargetYear <= 0)
            throw new DomainException("VALIDATION_REQUIRED", "請選擇年份");
        if (req.TargetCeremonyId == Guid.Empty)
            throw new DomainException("VALIDATION_REQUIRED", "請選擇法會");

        var group = PrepayGroups.Resolve(req.BelieverGroup);

        // 2. 查目標法會 Title + Sort
        var targetCategory = await repo.GetCeremonyCategoryAsync(req.TargetCeremonyId, ct)
            ?? throw new DomainException("CATEGORY_NOT_FOUND", "找不到目標法會");
        var targetTitle = targetCategory.Title;
        var targetSort = targetCategory.Sort;

        // 3. 查源資料（已 join Believer + PrepayCeremonyCategorys；ORDER BY IsFixedNumber DESC, Number）
        var sources = await repo.GetPrepaySourcesAsync(
            req.SourceYear,
            req.SourceCeremonyId,
            group.SignupType,
            group.EmployeeType,
            req.TargetYear,
            targetSort,
            ct);

        // 4. 建候選（欄位映射 + PrepayYear 結轉判斷；尚未配 Number）。
        //    分兩批：固定編號（保留原號、升冪）、非固定編號（升冪）。
        var fixedCandidates = sources
            .Where(s => s.IsFixedNumber)
            .OrderBy(s => s.Number)
            .Select(s => BuildCandidate(s, req, group, caller, targetTitle, targetSort))
            .ToList();

        var nonFixedCandidates = sources
            .Where(s => !s.IsFixedNumber)
            .OrderBy(s => s.Number)
            .Select(s => BuildCandidate(s, req, group, caller, targetTitle, targetSort))
            .ToList();

        // 5. 配號 + insert 全在單一 transaction 內完成（上鎖讀 MAX、idempotency dedup、配號、寫入）。
        return await repo.InsertPrepayBatchAsync(
            req.TargetYear,
            req.TargetCeremonyId,
            group.SignupType,
            fixedCandidates,
            nonFixedCandidates,
            ct);
    }

    /// <summary>
    /// 把源資料轉成待載入候選（SignupWriteModel + SignupLogWriteModel）。Number 由 repo 在交易內配號。
    /// </summary>
    private static PrepayCandidate BuildCandidate(
        PrepaySourceRow s,
        PrepayLoadRequest req,
        PrepayGroup group,
        CallerContext caller,
        string targetCeremonyTitle,
        int targetSort)
    {
        // PrepayYear 結轉條件（對齊舊 line 113-120, 187-194）：
        // 仍在未來 (PrepayYear > targetYear)，或同年但更後的 ceremony (PrepayYear == targetYear AND prepay.Sort > target.Sort)
        var carryForward =
            s.PrepayYear > req.TargetYear
            || (s.PrepayYear == req.TargetYear
                && s.PrepayCeremonyCategoryId.HasValue
                && (s.PrepayCeremonySort ?? 0) > targetSort);

        var newSignupId = Guid.NewGuid();
        var numberTitle = NumberTitleResolver.Resolve(group.SignupType);
        var createDate = DateTime.UtcNow;

        var signup = new SignupWriteModel(
            SignupId: newSignupId,
            Year: req.TargetYear,
            CeremonyCategoryId: req.TargetCeremonyId,
            SignupType: group.SignupType,
            BelieverId: s.BelieverId,
            NumberTitle: numberTitle,
            Fee: s.Fee,
            // 對齊舊 LoadPrepayForm：預繳建立的 Signup 不帶 Name/Phone（留 null），列印時姓名從 Believer 取。
            Name: null!,
            Phone: null,
            LivingNames: s.LivingNames,
            DeadNames: s.DeadNames,
            MailZipcodeId: s.MailZipcodeId,
            MailAddress: s.MailAddress,
            TextZipcodeId: s.TextZipcodeId,
            TextAddress: s.TextAddress,
            Remark: s.Remark,
            PrepayYear: carryForward ? s.PrepayYear : null,
            PrepayCeremonyCategoryId: carryForward ? s.PrepayCeremonyCategoryId : null,
            AdminId: caller.AdminId,
            CreateDate: createDate);

        var log = new SignupLogWriteModel(
            SignupLogId: Guid.NewGuid(),
            SignupId: newSignupId,
            Year: req.TargetYear,
            CeremonyCategoryTitle: targetCeremonyTitle,
            SignupType: group.SignupType,
            HallName: null,
            // SignupLog 是新版補強（舊系統載入預繳不寫 log），且 dbo.SignupLogs.Name 為 NOT NULL，
            // 不能沿用 Signup 的 null——比照 POST /signups 的 log 語意寫入信眾姓名快照。
            Name: s.BelieverName,
            Phone: null,
            NumberTitle: numberTitle,
            Fee: s.Fee,
            LivingNames: s.LivingNames,
            DeadNames: s.DeadNames,
            MailCity: null,
            MailZone: null,
            MailAddress: s.MailAddress,
            TextCity: null,
            TextZone: null,
            TextAddress: s.TextAddress,
            Remark: s.Remark,
            PrepayYear: carryForward ? s.PrepayYear : null,
            PrepayCeremonyCategoryTitle: carryForward ? s.PrepayCeremonyTitle : null,
            Admin: caller.AdminName,
            CreateDate: createDate);

        return new PrepayCandidate(
            BelieverId: s.BelieverId,
            IsFixedNumber: s.IsFixedNumber,
            PreservedNumber: s.IsFixedNumber ? s.Number : null,
            CarriedForward: carryForward,
            Signup: signup,
            Log: log);
    }
}
