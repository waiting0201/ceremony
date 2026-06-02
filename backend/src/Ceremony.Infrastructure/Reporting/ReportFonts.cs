using QuestPDF.Drawing;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 列印字型註冊：把作業系統的「標楷體」字型檔以**自訂家族名 BiauKai** 註冊進 QuestPDF，
/// 確保所有 renderer 的 <c>FontFamily("BiauKai")</c> 一定解析到真正的標楷體，
/// 不會 silently fallback 到系統 CJK 字型（如 macOS PingFang / Windows 微軟正黑），
/// 否則字寬不同會導致直書換行、文字尺寸與位置全跑掉（薦牌/文牒尤其明顯）。
/// </summary>
/// <remarks>
/// 為何不直接用 <c>FontFamily("BiauKai")</c> 讓系統解析：
/// macOS BiauKai.ttc 的內部家族名是「標楷體-繁 / BiauKaiTC」，並非 "BiauKai"，
/// 故 SkiaSharp 找不到 → fallback PingFang TC（pdffonts 實測）。改用
/// <see cref="FontManager.RegisterFontWithCustomName"/> 以串流註冊，名稱由我們指定。
///
/// 字型來源（依序，找到第一個可讀的就用）：
///   1. 環境變數 CEREMONY_KAI_FONT（部署可明確指定，最高優先）
///   2. Windows：C:\Windows\Fonts\kaiu.ttf（DFKai-SB 標楷體，內建）
///   3. macOS：BiauKai.ttc（on-demand asset，glob AssetsV2）/ Supplemental
///   4. Linux：TW-Kai（全字庫正楷體，開源可再散布）常見安裝路徑
/// 找不到 → 印警告（不 silently fallback，對齊 renderer「禁止 fallback」條款）。
///
/// 部署備註：Windows 內建 kaiu.ttf 即可；若走容器/Linux sidecar 需安裝 TW-Kai
/// 或以 CEREMONY_KAI_FONT 指向打包的字型檔（見 docs/blueprints/printing-reports.md 字型段）。
/// </remarks>
public static class ReportFonts
{
    /// <summary>所有 renderer 共用的 QuestPDF 家族名。</summary>
    public const string Family = "BiauKai";

    private static readonly object _lock = new();
    private static bool _registered;

    /// <summary>解析到的字型檔路徑（null = 未找到，已 fallback）。供 SkiaImageHelpers 共用。</summary>
    public static string? ResolvedPath { get; private set; }

    /// <summary>冪等：重複呼叫只註冊一次。在 AddCeremonyInfrastructure 啟動時呼叫。</summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (_lock)
        {
            if (_registered) return;
            _registered = true;

            var path = ResolveKaiFontPath();
            if (path is null)
            {
                Console.Error.WriteLine(
                    "[ReportFonts] 警告：找不到標楷體字型檔，列印文字將 fallback，尺寸/位置/字體會不正確。" +
                    "請於部署機安裝標楷體（Windows kaiu.ttf / Linux TW-Kai）或設定環境變數 CEREMONY_KAI_FONT 指向字型檔。");
                return;
            }

            try
            {
                using var fs = File.OpenRead(path);
                FontManager.RegisterFontWithCustomName(Family, fs);
                ResolvedPath = path;
                Console.WriteLine($"[ReportFonts] 已註冊標楷體 '{Family}' ← {path}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ReportFonts] 註冊標楷體失敗 ({path}): {ex.Message}");
            }
        }
    }

    private static string? ResolveKaiFontPath()
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        var env = Environment.GetEnvironmentVariable("CEREMONY_KAI_FONT");
        if (!string.IsNullOrWhiteSpace(env)) yield return env;

        // Windows 內建 DFKai-SB（標楷體）
        yield return @"C:\Windows\Fonts\kaiu.ttf";

        // macOS BiauKai（標楷體）— on-demand asset，路徑含 hash，需 glob
        foreach (var p in SafeEnumerate("/System/Library/AssetsV2/com_apple_MobileAsset_Font8", "BiauKai.ttc"))
            yield return p;
        yield return "/System/Library/Fonts/Supplemental/BiauKai.ttc";

        // Linux：TW-Kai（全字庫正楷體，開源）/ 任何 kai 字型
        foreach (var p in SafeEnumerate("/usr/share/fonts", "TW-Kai*.ttf")) yield return p;
        foreach (var p in SafeEnumerate("/usr/local/share/fonts", "TW-Kai*.ttf")) yield return p;
        foreach (var p in SafeEnumerate("/usr/share/fonts", "*[Kk]ai*.tt?")) yield return p;
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern)
    {
        if (!Directory.Exists(root)) return [];
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
        }
        catch
        {
            return [];
        }
    }
}
