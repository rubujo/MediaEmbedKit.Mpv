using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 指定搜尋命令使用的位置基準與精確度。
/// </summary>
[Flags]
public enum MpvSeekMode
{
    /// <summary>
    /// 相對於目前位置搜尋指定秒數。
    /// </summary>
    Relative = 1,

    /// <summary>
    /// 搜尋到媒體的絕對秒數位置。
    /// </summary>
    Absolute = 2,

    /// <summary>
    /// 搜尋到媒體長度的絕對百分比。
    /// </summary>
    AbsolutePercent = 4,

    /// <summary>
    /// 相對於目前位置搜尋媒體長度的百分比。
    /// </summary>
    RelativePercent = 8,

    /// <summary>
    /// 優先搜尋到關鍵影格，速度較快但位置可能不精確。
    /// </summary>
    Keyframes = 16,

    /// <summary>
    /// 執行精確搜尋，可能需要較多解碼工作。
    /// </summary>
    Exact = 32
}

/// <summary>
/// 提供 <see cref="MpvSeekMode"/> 的內部轉換邏輯。
/// </summary>
internal static class MpvSeekModeExtensions
{
    private const MpvSeekMode PositionMask =
        MpvSeekMode.Relative
        | MpvSeekMode.Absolute
        | MpvSeekMode.AbsolutePercent
        | MpvSeekMode.RelativePercent;

    private const MpvSeekMode PrecisionMask = MpvSeekMode.Keyframes | MpvSeekMode.Exact;

    /// <summary>
    /// 將強型別搜尋模式轉換為 mpv 命令旗標。
    /// </summary>
    /// <param name="mode">
    /// 要轉換的搜尋模式。
    /// </param>
    /// <returns>
    /// mpv <c>seek</c> 命令使用的旗標字串。
    /// </returns>
    internal static string ToMpvValue(this MpvSeekMode mode)
    {
        MpvSeekMode position = mode & PositionMask;
        if (position == 0 || !IsSingleFlag(position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "搜尋模式必須指定一個位置基準。");
        }

        MpvSeekMode precision = mode & PrecisionMask;
        if (!IsZeroOrSingleFlag(precision))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "搜尋模式不可同時指定 Exact 與 Keyframes。");
        }

        MpvSeekMode known = PositionMask | PrecisionMask;
        if ((mode & ~known) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "搜尋模式包含不支援的旗標。");
        }

        List<string> values = new List<string>(2)
        {
            position switch
            {
                MpvSeekMode.Relative => "relative",
                MpvSeekMode.Absolute => "absolute",
                MpvSeekMode.AbsolutePercent => "absolute-percent",
                MpvSeekMode.RelativePercent => "relative-percent",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "搜尋位置基準無效。")
            }
        };

        if (precision == MpvSeekMode.Keyframes)
        {
            values.Add("keyframes");
        }
        else if (precision == MpvSeekMode.Exact)
        {
            values.Add("exact");
        }

        return string.Join("+", values);
    }

    /// <summary>
    /// 判斷搜尋模式是否使用百分比數值。
    /// </summary>
    /// <param name="mode">
    /// 要判斷的搜尋模式。
    /// </param>
    /// <returns>
    /// 使用絕對或相對百分比時為 <see langword="true"/>。
    /// </returns>
    internal static bool UsesPercentage(this MpvSeekMode mode)
    {
        MpvSeekMode position = mode & PositionMask;
        return position == MpvSeekMode.AbsolutePercent || position == MpvSeekMode.RelativePercent;
    }

    private static bool IsSingleFlag(MpvSeekMode value)
    {
        int bits = (int)value;
        return (bits & (bits - 1)) == 0;
    }

    private static bool IsZeroOrSingleFlag(MpvSeekMode value)
    {
        return value == 0 || IsSingleFlag(value);
    }
}
