using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv <c>chapter-list</c> 屬性回報的單一章節項目。建立物件不會與
/// <see cref="MpvPlayer"/> 維持連結 — 取自 snapshot 的不可變值物件。
/// </summary>
public readonly struct MpvChapter : IEquatable<MpvChapter>
{
    /// <summary>
    /// 初始化 <see cref="MpvChapter"/> 結構的新執行個體。
    /// </summary>
    /// <param name="index">章節在播放列表中的 0-based 索引。</param>
    /// <param name="time">章節起始時間。</param>
    /// <param name="title">章節標題；mpv 沒有提供時為 <see langword="null"/>。</param>
    public MpvChapter(int index, TimeSpan time, string? title)
    {
        Index = index;
        Time = time;
        Title = title;
    }

    /// <summary>
    /// 取得章節在 <c>chapter-list</c> 陣列中的 0-based 索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得章節起始時間。
    /// </summary>
    public TimeSpan Time { get; }

    /// <summary>
    /// 取得章節標題；mpv 媒體未提供時為 <see langword="null"/>。
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// 判斷兩個 <see cref="MpvChapter"/> 值是否相等。
    /// </summary>
    /// <param name="other">要比較的另一個章節值。</param>
    /// <returns>兩值在 Index / Time / Title 三個欄位皆相等時為 <see langword="true"/>。</returns>
    public bool Equals(MpvChapter other)
    {
        return Index == other.Index
            && Time == other.Time
            && string.Equals(Title, other.Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判斷指定物件是否為相等的 <see cref="MpvChapter"/>。
    /// </summary>
    /// <param name="obj">要比較的物件。</param>
    /// <returns>物件是相等的 <see cref="MpvChapter"/> 時為 <see langword="true"/>。</returns>
    public override bool Equals(object? obj)
    {
        return obj is MpvChapter other && Equals(other);
    }

    /// <summary>
    /// 取得本章節值的雜湊碼。
    /// </summary>
    /// <returns>本章節值的雜湊碼。</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Index;
            hash = (hash * 397) ^ Time.GetHashCode();
            hash = (hash * 397) ^ (Title != null ? StringComparer.Ordinal.GetHashCode(Title) : 0);
            return hash;
        }
    }

    /// <summary>
    /// 判斷兩個 <see cref="MpvChapter"/> 值是否相等。
    /// </summary>
    /// <param name="left">第一個章節值。</param>
    /// <param name="right">第二個章節值。</param>
    /// <returns>兩值相等時為 <see langword="true"/>。</returns>
    public static bool operator ==(MpvChapter left, MpvChapter right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 判斷兩個 <see cref="MpvChapter"/> 值是否不相等。
    /// </summary>
    /// <param name="left">第一個章節值。</param>
    /// <param name="right">第二個章節值。</param>
    /// <returns>兩值不相等時為 <see langword="true"/>。</returns>
    public static bool operator !=(MpvChapter left, MpvChapter right)
    {
        return !left.Equals(right);
    }
}
