namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 定義 Windows libmpv 建置的下載來源提供者。
    /// </summary>
    public enum MpvWindowsBuildProvider
    {
        /// <summary>
        /// 使用 shinchiro/mpv-winbuild-cmake 發行資產。
        /// </summary>
        Shinchiro = 0,
        /// <summary>
        /// 使用 zhongfly/mpv-winbuild 發行資產。
        /// </summary>
        Zhongfly = 1
    }
}
