using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Reports;

/// <summary>
/// 產生報名資料卡 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:188-240 (tsmiPrintDataCard_Click) + :956-1050 (PrintDataCard helper)
/// Blueprint: docs/blueprints/printing-reports.md + printing-reports-positions.md §1
/// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (rows 9, 28)
/// </remarks>
public sealed class GenerateDataCardHandler(
    ISignupRepository signupRepo,
    IReportRenderer renderer)
{
    public async Task<(byte[] Pdf, string FileName)> HandleAsync(Guid signupId, bool debugOverlay = false, CancellationToken ct = default)
    {
        var s = await signupRepo.GetByIdAsync(signupId, ct)
            ?? throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");

        return (renderer.RenderDataCard(ReportModelBuilders.DataCard(s), debugOverlay),
                $"datacard-{s.Year}-{s.NumberTitle}-{s.Number}.pdf");
    }
}
