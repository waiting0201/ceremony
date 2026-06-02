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

        // 3. 起始號
        var maxNumber = await repo.GetMaxNumberAsync(req.TargetYear, req.TargetCeremonyId, group.SignupType, ct);
        var nextNo = maxNumber + 1;

        // 4. 查源資料（已 join Believer + PrepayCeremonyCategorys；ORDER BY IsFixedNumber DESC, Number）
        var sources = await repo.GetPrepaySourcesAsync(
            req.SourceYear,
            req.SourceCeremonyId,
            group.SignupType,
            group.EmployeeType,
            req.TargetYear,
            targetSort,
            ct);

        // 5. 兩階段配號 + idempotency dedup
        var batch = new List<(SignupWriteModel, SignupLogWriteModel, int)>();
        var gaps = new List<int>();
        var fixedLoaded = 0;
        var nonFixedLoaded = 0;
        var carriedForwardPrepay = 0;
        var skipped = 0;

        var fixedSources = sources.Where(s => s.IsFixedNumber).OrderBy(s => s.Number).ToList();
        var nonFixedSources = sources.Where(s => !s.IsFixedNumber).OrderBy(s => s.Number).ToList();

        // 第一批：fixed (preserve number)
        foreach (var s in fixedSources)
        {
            if (await repo.SignupExistsAsync(req.TargetYear, req.TargetCeremonyId, group.SignupType, s.BelieverId, ct))
            {
                skipped++;
                continue;
            }
            var (signup, log, didCarry) = BuildModels(s, req, group, caller, targetTitle, targetSort, preservedNumber: s.Number);
            var actualNumber = s.Number ?? nextNo;
            batch.Add((signup, log, actualNumber));
            fixedLoaded++;
            if (didCarry) carriedForwardPrepay++;

            // 收集 gaps（對齊舊 line 125-136）
            if (s.Number is { } n)
            {
                if (nextNo != n)
                {
                    for (var x = nextNo; x < n; x++) gaps.Add(x);
                }
                nextNo = Math.Max(nextNo, n + 1);
            }
        }

        // 第二批：non-fixed (allocate from gaps then nextNo)
        var gapIdx = 0;
        foreach (var s in nonFixedSources)
        {
            if (await repo.SignupExistsAsync(req.TargetYear, req.TargetCeremonyId, group.SignupType, s.BelieverId, ct))
            {
                skipped++;
                continue;
            }
            int number;
            if (gapIdx < gaps.Count)
            {
                number = gaps[gapIdx];
                gapIdx++;
            }
            else
            {
                number = nextNo;
                nextNo++;
            }
            var (signup, log, didCarry) = BuildModels(s, req, group, caller, targetTitle, targetSort, preservedNumber: null);
            batch.Add((signup, log, number));
            nonFixedLoaded++;
            if (didCarry) carriedForwardPrepay++;
        }

        // 6. 批次寫入（一個 transaction）
        if (batch.Count > 0)
            await repo.InsertBatchAsync(batch, ct);

        return new PrepayLoadResponse(
            Loaded: fixedLoaded + nonFixedLoaded,
            Skipped: skipped,
            Details: new PrepayLoadDetails(
                FixedLoaded: fixedLoaded,
                NonFixedLoaded: nonFixedLoaded,
                CarriedForwardPrepay: carriedForwardPrepay,
                FilledGaps: gaps.Take(gapIdx).ToList()));
    }

    /// <summary>把源資料轉成 SignupWriteModel + SignupLogWriteModel。Number 由批次 insert 端套上。</summary>
    private static (SignupWriteModel Signup, SignupLogWriteModel Log, bool CarriedForward) BuildModels(
        PrepaySourceRow s,
        PrepayLoadRequest req,
        PrepayGroup group,
        CallerContext caller,
        string targetCeremonyTitle,
        int targetSort,
        int? preservedNumber)
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
            Name: s.Name ?? string.Empty,
            Phone: s.Phone,
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
            Name: s.Name ?? string.Empty,
            Phone: s.Phone,
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

        return (signup, log, carryForward);
    }
}
