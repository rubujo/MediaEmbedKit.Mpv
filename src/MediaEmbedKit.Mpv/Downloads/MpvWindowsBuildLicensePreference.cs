namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 定義 Windows libmpv 建置下載時的授權偏好。
/// </summary>
public enum MpvWindowsBuildLicensePreference
{
    /// <summary>
    /// 不依授權文字篩選發行資產。
    /// </summary>
    Any = 0,
    /// <summary>
    /// 優先選擇名稱標示為 LGPL 的發行資產。
    /// </summary>
    PreferLgpl = 1,
    /// <summary>
    /// 只接受名稱標示為 LGPL 的發行資產。
    /// </summary>
    RequireLgpl = 2,
    /// <summary>
    /// 優先選擇名稱未標示為 LGPL 的發行資產。
    /// </summary>
    PreferNonLgpl = 3,
    /// <summary>
    /// 只接受名稱未標示為 LGPL 的發行資產。
    /// </summary>
    RequireNonLgpl = 4
}
