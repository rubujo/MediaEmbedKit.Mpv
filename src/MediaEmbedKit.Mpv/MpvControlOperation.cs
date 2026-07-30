namespace MediaEmbedKit.Mpv;

/// <summary>
/// 識別 UI 控制項執行失敗的操作種類。
/// </summary>
public enum MpvControlOperation
{
    /// <summary>
    /// 建立或初始化播放器。
    /// </summary>
    Initialize,

    /// <summary>
    /// 載入媒體來源。
    /// </summary>
    Load,

    /// <summary>
    /// 執行播放控制命令。
    /// </summary>
    Command,

    /// <summary>
    /// 搜尋播放位置。
    /// </summary>
    Seek,

    /// <summary>
    /// 寫入播放器屬性。
    /// </summary>
    PropertyWrite,

    /// <summary>
    /// 建立或連接 UI 後端。
    /// </summary>
    Backend
}
