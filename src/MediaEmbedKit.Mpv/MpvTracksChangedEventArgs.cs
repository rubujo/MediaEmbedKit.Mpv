using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv 播放軌清單變更事件資料。
    /// </summary>
    public sealed class MpvTracksChangedEventArgs : MpvEventArgs
    {
        /// <summary>
        /// 初始化 <see cref="MpvTracksChangedEventArgs"/> 類別的新執行個體。
        /// </summary>
        /// <param name="replyUserData">屬性觀察要求使用的回覆資料。</param>
        /// <param name="tracks">最新播放軌清單。</param>
        public MpvTracksChangedEventArgs(ulong replyUserData, IReadOnlyList<MpvTrackInfo> tracks)
            : base(MpvEventId.PropertyChange, 0, replyUserData)
        {
            Tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        }

        /// <summary>
        /// 取得最新播放軌清單。
        /// </summary>
        /// <value>最新播放軌清單。</value>
        public IReadOnlyList<MpvTrackInfo> Tracks { get; private set; }
    }
}
