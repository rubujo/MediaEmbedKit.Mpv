namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 定義重新掃描外部檔案後的選軌行為。
    /// </summary>
    public enum MpvExternalFilesRescanMode
    {
        /// <summary>
        /// 重新依 mpv 偏好選擇外部音訊與字幕。
        /// </summary>
        Reselect = 0,
        /// <summary>
        /// 保留目前已選取的播放軌。
        /// </summary>
        KeepSelection = 1
    }
}
