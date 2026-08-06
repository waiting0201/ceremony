using Ceremony.Application.Reports;
using Ceremony.Application.Signups;
using FluentAssertions;
using Moq;

namespace Ceremony.Application.Tests.Reports;

/// <summary>
/// 薦牌「現場對位校正版」（debugGrid）的接線鎖。
/// </summary>
/// <remarks>
/// 2026-08-06 客訴「四位往生者的字壓到預印的靈位」：三種排法（1 位／2 位／3+ 矩陣）的可用高
/// 各自帶常數，且都是從樣板照片量的——四位那組算起來離「靈」字上緣還有 0.27cm 餘裕卻仍壓字，
/// 代表**量測基準本身**要用實體校正版反推。校正版是量測工具，必須能在現場的 Windows 機器上印，
/// 所以 debugGrid **刻意不做 Development 阻擋**（與只給開發用的 debugOverlay 不同）。
/// 見 docs/blueprints/printing-reports.md「現場對位校正版」。
/// </remarks>
public sealed class GenerateTabletHandlerTests
{
    private static readonly Guid SignupId = Guid.NewGuid();

    private static SignupListItem Signup() => new(
        Id: SignupId,
        Year: 115,
        CeremonyCategoryId: Guid.NewGuid(),
        CeremonyTitle: "春季",
        SignupType: 1,
        NumberTitle: "郵",
        Number: 27,
        Fee: 600,
        Employee: null,
        BelieverId: null,
        Name: "黃耀章",
        HallName: "甲堂",
        Phone: "0912345678",
        IsFixedNumber: false,
        LivingNames: ["子甲", null, null, null, null, null],
        DeadNames: ["陳大明", "陳林罔市", "陳阿水", "陳美玉", null, null],
        MailCity: "台北市", MailZone: "信義區", MailZipcode: "110", MailAddress: "市府路 1 號",
        TextCity: "台北市", TextZone: "信義區", TextZipcode: "110", TextAddress: "市府路 1 號",
        PrepayYear: null, PrepayCeremonyCategoryId: null, PrepayCeremonyTitle: null,
        Remark: null, AdminName: "Administrator", CreateDate: DateTime.UtcNow);

    private static (GenerateTabletHandler Handler, Mock<IReportRenderer> Renderer) Build()
    {
        var repo = new Mock<ISignupRepository>();
        repo.Setup(r => r.GetByIdAsync(SignupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Signup());
        var renderer = new Mock<IReportRenderer>();
        renderer.Setup(r => r.RenderTablet(It.IsAny<TabletModel>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns([1, 2, 3]);
        return (new GenerateTabletHandler(repo.Object, renderer.Object), renderer);
    }

    [Fact]
    public async Task DebugGrid_is_forwarded_to_renderer_and_marks_the_file_name()
    {
        var (handler, renderer) = Build();

        var (_, fileName) = await handler.HandleAsync(SignupId, debugOverlay: false, debugGrid: true);

        renderer.Verify(r => r.RenderTablet(It.IsAny<TabletModel>(), false, true), Times.Once);
        // 現場會把校正版與正式版兩張並排比對，檔名是唯一的辨識依據（檢視器視窗標題只有檔名）
        fileName.Should().Be("tablet-115-郵-27-calibration.pdf");
    }

    [Fact]
    public async Task Normal_print_stays_unmarked_and_grid_free()
    {
        var (handler, renderer) = Build();

        var (_, fileName) = await handler.HandleAsync(SignupId);

        renderer.Verify(r => r.RenderTablet(It.IsAny<TabletModel>(), false, false), Times.Once);
        fileName.Should().Be("tablet-115-郵-27.pdf");
    }
}
