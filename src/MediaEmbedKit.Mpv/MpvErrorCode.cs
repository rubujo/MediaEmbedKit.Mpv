namespace MediaEmbedKit.Mpv;

/// <summary>
/// 定義 libmpv 常見錯誤碼。
/// </summary>
public enum MpvErrorCode
{
    /// <summary>
    /// 呼叫已成功完成。
    /// </summary>
    Success = 0,
    /// <summary>
    /// libmpv 事件佇列已滿。
    /// </summary>
    EventQueueFull = -1,
    /// <summary>
    /// libmpv 無法配置必要的記憶體。
    /// </summary>
    NoMemory = -2,
    /// <summary>
    /// libmpv 用戶端尚未初始化。
    /// </summary>
    Uninitialized = -3,
    /// <summary>
    /// 呼叫提供了無效的參數。
    /// </summary>
    InvalidParameter = -4,
    /// <summary>
    /// 指定的選項不存在。
    /// </summary>
    OptionNotFound = -5,
    /// <summary>
    /// 選項值格式不正確。
    /// </summary>
    OptionFormat = -6,
    /// <summary>
    /// 設定選項時發生錯誤。
    /// </summary>
    OptionError = -7,
    /// <summary>
    /// 指定的屬性不存在。
    /// </summary>
    PropertyNotFound = -8,
    /// <summary>
    /// 屬性值格式不正確。
    /// </summary>
    PropertyFormat = -9,
    /// <summary>
    /// 指定的屬性目前無法使用。
    /// </summary>
    PropertyUnavailable = -10,
    /// <summary>
    /// 存取屬性時發生錯誤。
    /// </summary>
    PropertyError = -11,
    /// <summary>
    /// 執行命令時發生錯誤。
    /// </summary>
    Command = -12,
    /// <summary>
    /// 載入播放項目失敗。
    /// </summary>
    LoadingFailed = -13,
    /// <summary>
    /// 音訊輸出初始化失敗。
    /// </summary>
    AudioOutputInitFailed = -14,
    /// <summary>
    /// 視訊輸出初始化失敗。
    /// </summary>
    VideoOutputInitFailed = -15,
    /// <summary>
    /// 沒有可播放的項目。
    /// </summary>
    NothingToPlay = -16,
    /// <summary>
    /// 播放項目格式無法辨識。
    /// </summary>
    UnknownFormat = -17,
    /// <summary>
    /// libmpv 不支援指定的操作或格式。
    /// </summary>
    Unsupported = -18,
    /// <summary>
    /// libmpv 尚未實作指定的操作。
    /// </summary>
    NotImplemented = -19,
    /// <summary>
    /// 發生一般 libmpv 錯誤。
    /// </summary>
    Generic = -20
}
