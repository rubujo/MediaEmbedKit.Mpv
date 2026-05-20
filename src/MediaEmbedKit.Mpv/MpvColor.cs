using System;
using System.Globalization;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供建立與正規化 mpv 色彩字串的工具。
/// </summary>
/// <remarks>
/// <para>
/// libmpv 字幕 / OSD 色彩屬性（<c>sub-color</c>、<c>sub-back-color</c>、<c>osd-color</c>
/// 等）接受多種色彩字串格式：
/// </para>
/// <list type="bullet">
/// <item>
/// <c>#RRGGBB</c> 或 <c>#AARRGGBB</c> 十六進位
/// </item>
/// <item>
/// <c>0xRRGGBB</c> 或 <c>0xAARRGGBB</c>
/// </item>
/// <item>
/// CSS 命名色彩（<c>red</c> / <c>cornflowerblue</c> 等）
/// </item>
/// <item>
/// 浮點 RGBA：<c>0.5/0.5/0.5/1.0</c>
/// </item>
/// </list>
/// <para>
/// 本類別只提供生成與基本格式檢查；MpvPlayer 上對應的字幕色彩屬性 setter 不做
/// 自動驗證 — 想要 compile-time typed 安全的呼叫端可呼叫 <see cref="FromArgb"/> /
/// <see cref="FromRgb"/> / <see cref="TryParse"/> 取得正規化字串再賦值。
/// </para>
/// </remarks>
public static class MpvColor
{
    /// <summary>
    /// 從 ARGB 分量建立 <c>#AARRGGBB</c> 格式字串。
    /// </summary>
    /// <param name="alpha">
    /// Alpha 值（0–255）。
    /// </param>
    /// <param name="red">
    /// 紅色值（0–255）。
    /// </param>
    /// <param name="green">
    /// 綠色值（0–255）。
    /// </param>
    /// <param name="blue">
    /// 藍色值（0–255）。
    /// </param>
    /// <returns>
    /// mpv 接受的 <c>#AARRGGBB</c> 格式字串。
    /// </returns>
    public static string FromArgb(byte alpha, byte red, byte green, byte blue)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0:X2}{1:X2}{2:X2}{3:X2}",
            alpha,
            red,
            green,
            blue);
    }

    /// <summary>
    /// 從 RGB 分量建立 <c>#FFRRGGBB</c> 格式字串（alpha 預設 0xFF 不透明）。
    /// </summary>
    /// <param name="red">
    /// 紅色值（0–255）。
    /// </param>
    /// <param name="green">
    /// 綠色值（0–255）。
    /// </param>
    /// <param name="blue">
    /// 藍色值（0–255）。
    /// </param>
    /// <returns>
    /// mpv 接受的 <c>#FFRRGGBB</c> 格式字串。
    /// </returns>
    public static string FromRgb(byte red, byte green, byte blue)
    {
        return FromArgb(0xFF, red, green, blue);
    }

    /// <summary>
    /// 嘗試把任意 mpv 支援的色彩字串正規化為 <c>#AARRGGBB</c> 形式。
    /// 只處理常見的 <c>#RRGGBB</c> / <c>#AARRGGBB</c> / <c>0xRRGGBB</c> / <c>0xAARRGGBB</c>
    /// 十六進位變體；CSS 命名色彩與浮點 RGBA 不在處理範圍（會回傳原字串並回 <see langword="false"/>）。
    /// </summary>
    /// <param name="input">
    /// 輸入色彩字串。
    /// </param>
    /// <param name="normalized">
    /// 解析成功時為 <c>#AARRGGBB</c> 格式字串；失敗時為原字串。
    /// </param>
    /// <returns>
    /// 解析成功時為 <see langword="true"/>。
    /// </returns>
    public static bool TryParse(string? input, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            normalized = input;
            return false;
        }

        string trimmed = input!.Trim();
        string hex;
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            hex = trimmed.Substring(1);
        }
        else if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = trimmed.Substring(2);
        }
        else
        {
            normalized = input;
            return false;
        }

        byte a;
        byte r;
        byte g;
        byte b;
        if (hex.Length == 6)
        {
            a = 0xFF;
            if (!TryParseHexByte(hex, 0, out r)
                || !TryParseHexByte(hex, 2, out g)
                || !TryParseHexByte(hex, 4, out b))
            {
                normalized = input;
                return false;
            }
        }
        else if (hex.Length == 8)
        {
            if (!TryParseHexByte(hex, 0, out a)
                || !TryParseHexByte(hex, 2, out r)
                || !TryParseHexByte(hex, 4, out g)
                || !TryParseHexByte(hex, 6, out b))
            {
                normalized = input;
                return false;
            }
        }
        else
        {
            normalized = input;
            return false;
        }

        normalized = FromArgb(a, r, g, b);
        return true;
    }

    /// <summary>
    /// 從字串指定偏移處解析兩個十六進位字元為一個 byte。
    /// </summary>
    /// <param name="hex">
    /// 不含 <c>#</c> 或 <c>0x</c> 前綴的十六進位字串。
    /// </param>
    /// <param name="offset">
    /// 起始字元偏移。
    /// </param>
    /// <param name="value">
    /// 解析成功時的 byte 值。
    /// </param>
    /// <returns>
    /// 解析成功時為 <see langword="true"/>。
    /// </returns>
    private static bool TryParseHexByte(string hex, int offset, out byte value)
    {
        return byte.TryParse(
            hex.Substring(offset, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }
}
