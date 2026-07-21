using System.Runtime.CompilerServices;
using Ceremony.Domain.Exceptions;

[assembly: InternalsVisibleTo("Ceremony.Application.Tests")]

namespace Ceremony.Application.Believers;

/// <summary>
/// 共用 normalize + 驗證邏輯（POST + PUT 共用）。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:101-152 (validation) + :128 (Strings.StrConv Narrow)
/// </remarks>
internal static class BelieverWriteValidator
{
    public static BelieverWriteModel ValidateAndNormalize(BelieverUpsertRequest req)
    {
        var name = req.Name?.Trim() ?? string.Empty;
        var mailAddress = req.MailAddress?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入姓名");
        // 地址非必填（2026-07-21 使用者指定）：空白即存空字串，不再擋下（未選信眾自動建立時亦適用）。
        if (req.EmployeeType is not (1 or 2 or 3))
            throw new DomainException("VALIDATION_INVALID", "員工類別錯誤");
        if (name.Length > 30)
            throw new DomainException("VALIDATION_LENGTH", "姓名最多 30 個字");
        if (mailAddress.Length > 250)
            throw new DomainException("VALIDATION_LENGTH", "寄件地址最多 250 個字");

        var hallName = NormalizeOrNull(req.HallName, maxLength: 10, fieldName: "堂號");
        var phone = ToNarrow(NormalizeOrNull(req.Phone, maxLength: 30, fieldName: "電話"));
        var textAddress = NormalizeOrNull(req.TextAddress, maxLength: 250, fieldName: "文牒地址");

        var living = NormalizeNames(req.LivingNames, "陽上");
        var dead = NormalizeNames(req.DeadNames, "往生");

        return new BelieverWriteModel(
            EmployeeType: req.EmployeeType,
            Name: name,
            MailAddress: mailAddress,
            HallName: hallName,
            Phone: phone,
            IsFixedNumber: req.IsFixedNumber,
            MailZipcodeId: NormalizeZipcode(req.MailZipcodeId),
            TextZipcodeId: NormalizeZipcode(req.TextZipcodeId),
            TextAddress: textAddress,
            LivingNames: living,
            DeadNames: dead);
    }

    private static string?[] NormalizeNames(IReadOnlyList<string?>? names, string label)
    {
        names ??= [null, null, null, null, null, null];
        if (names.Count != 6)
            throw new DomainException("VALIDATION_INVALID", $"{label}名單必須為 6 個元素");

        var result = new string?[6];
        for (var i = 0; i < 6; i++)
        {
            // 不 trim 開頭/結尾：保留刻意排版空格（如開頭全形空格把名字往下推作直書排版）。
            // 僅純空白 → null；長度驗證用實際儲存值（含空格，對齊 DB 欄位上限）。詳見 docs/gotchas.md「姓名中間空格」。
            var v = names[i];
            if (string.IsNullOrWhiteSpace(v))
            {
                result[i] = null;
            }
            else
            {
                if (v.Length > 30)
                    throw new DomainException("VALIDATION_LENGTH", $"{label}{i + 1} 最多 30 個字");
                result[i] = v;
            }
        }
        return result;
    }

    private static string? NormalizeOrNull(string? value, int maxLength, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new DomainException("VALIDATION_LENGTH", $"{fieldName}最多 {maxLength} 個字");
        return trimmed;
    }

    private static int? NormalizeZipcode(int? value) =>
        value is null or -1 ? null : value;  // 舊系統用 -1 代表「未選」

    /// <summary>
    /// 全形 → 半形（對應舊 <c>Strings.StrConv(text, VbStrConv.Narrow)</c>）。
    /// 處理 U+FF01–U+FF5E (ASCII 全形) 與 U+3000 (全形空白)。
    /// </summary>
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
