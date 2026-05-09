namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 定義專案執行階段 catalog 可辨識的原生 libmpv 平台。
    /// </summary>
    public enum MpvNativeRuntimePlatform
    {
        /// <summary>
        /// Windows 桌面平台。
        /// </summary>
        Windows = 0,
        /// <summary>
        /// 目前 catalog 尚未辨識的平台。
        /// </summary>
        Unknown = 1
    }
}
