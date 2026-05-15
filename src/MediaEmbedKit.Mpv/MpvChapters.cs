using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 libmpv 章節（chapter-list / chapter / chapters）的 typed 包裝。
/// 透過 <see cref="MpvPlayer.Chapters"/> 取得實體。每次存取 <see cref="Items"/> /
/// <see cref="Count"/> / <see cref="CurrentIndex"/> 都向 libmpv 即時 fetch（snapshot
/// 語意，不快取）。
/// </summary>
public sealed class MpvChapters
{
    /// <summary>
    /// 與本 sub-object 關聯的播放器。
    /// </summary>
    private readonly MpvPlayer _player;

    /// <summary>
    /// 初始化 <see cref="MpvChapters"/> 類別的新執行個體；由 <see cref="MpvPlayer"/>
    /// lazy-init 時呼叫，不對外公開。
    /// </summary>
    /// <param name="player">與本 sub-object 關聯的播放器。</param>
    internal MpvChapters(MpvPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    /// <summary>
    /// 取得當前媒體的章節快照清單；每次存取都向 libmpv 重新讀取 <c>chapter-list</c>。
    /// 未載入媒體或媒體無章節資訊時為空清單。
    /// </summary>
    public IReadOnlyList<MpvChapter> Items
    {
        get
        {
            if (!_player.TryGetPropertyNode("chapter-list", out MpvNode node))
            {
                return Array.Empty<MpvChapter>();
            }

            IReadOnlyList<MpvNode> entries = node.AsArray();
            if (entries.Count == 0)
            {
                return Array.Empty<MpvChapter>();
            }

            MpvChapter[] result = new MpvChapter[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                MpvNode entry = entries[index];
                string? title = entry.GetValueOrNone("title").AsString();
                double seconds = entry.GetValueOrNone("time").AsDouble();
                result[index] = new MpvChapter(index, TimeSpan.FromSeconds(seconds), title);
            }

            return result;
        }
    }

    /// <summary>
    /// 取得當前媒體的章節總數；未載入媒體或無章節時為 0。
    /// </summary>
    public int Count
    {
        get
        {
            if (!_player.TryGetPropertyInt64("chapters", out long count))
            {
                return 0;
            }

            return count < 0 ? 0 : (int)count;
        }
    }

    /// <summary>
    /// 取得當前章節的 0-based 索引；未載入媒體或無章節時為 <see langword="null"/>。
    /// libmpv 對「無章節」回傳 -1，本屬性轉換為 <see langword="null"/>。
    /// </summary>
    public int? CurrentIndex
    {
        get
        {
            if (!_player.TryGetPropertyInt64("chapter", out long value) || value < 0)
            {
                return null;
            }

            return (int)value;
        }
    }

    /// <summary>
    /// 取得當前章節資訊；無章節或載入後尚未確定當前章節時為 <see langword="null"/>。
    /// </summary>
    public MpvChapter? Current
    {
        get
        {
            int? currentIndex = CurrentIndex;
            if (!currentIndex.HasValue)
            {
                return null;
            }

            IReadOnlyList<MpvChapter> items = Items;
            if (currentIndex.Value < 0 || currentIndex.Value >= items.Count)
            {
                return null;
            }

            return items[currentIndex.Value];
        }
    }

    /// <summary>
    /// 跳到指定 0-based 索引的章節（寫入 mpv <c>chapter</c> 屬性）。
    /// </summary>
    /// <param name="index">目標章節索引；libmpv 會拒絕超出 <see cref="Count"/> 範圍的值。</param>
    /// <exception cref="MpvException">索引無效或當前媒體無章節時擲回。</exception>
    public void SeekTo(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "索引不可為負值。");
        }

        _player.SetPropertyInt64("chapter", index);
    }

    /// <summary>
    /// 跳到下一章節（mpv 命令 <c>add chapter 1</c>）。媒體未載入或無章節時 silently
    /// no-op 對齊 LibVLCSharp <c>NextChapter</c> 行為；已在最後一章時 mpv 視為結束播放。
    /// </summary>
    public void Next()
    {
        if (CurrentIndex == null)
        {
            return;
        }

        _player.Command("add", "chapter", "1");
    }

    /// <summary>
    /// 跳到前一章節（mpv 命令 <c>add chapter -1</c>）。媒體未載入或無章節時 silently
    /// no-op；已在第一章時 mpv 維持當前章節。
    /// </summary>
    public void Previous()
    {
        if (CurrentIndex == null)
        {
            return;
        }

        _player.Command("add", "chapter", "-1");
    }
}
