namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 提供範例覆蓋層強調色常數，以 ARGB 整數表示，避免引入特定 UI framework 命名空間。
    /// 其他控制項一律使用 framework 預設外觀以貼近原生樣式。
    /// </summary>
    internal static class SampleTheme
    {
        /// <summary>
        /// 範例 AirSpace 安全覆蓋層的強調色（半透明）。
        /// </summary>
        public const int AccentBadgeArgb = unchecked((int)0xDD0078D4);
        /// <summary>
        /// 範例一般覆蓋層的對比色（半透明）。
        /// </summary>
        public const int ContrastBadgeArgb = unchecked((int)0xDD5C2D91);
        /// <summary>
        /// 不支援透明度時使用的強調色不透明變體。
        /// </summary>
        public const int AccentBadgeOpaqueArgb = unchecked((int)0xFF0078D4);
        /// <summary>
        /// 不支援透明度時使用的對比色不透明變體。
        /// </summary>
        public const int ContrastBadgeOpaqueArgb = unchecked((int)0xFF5C2D91);
        /// <summary>
        /// 覆蓋層 badge 上的文字色彩，固定使用白色以維持強調色背景的對比。
        /// </summary>
        public const int BadgeForegroundArgb = unchecked((int)0xFFFFFFFF);

        /// <summary>
        /// 取得 ARGB 整數的 alpha 分量。
        /// </summary>
        /// <param name="argb">要拆解的 ARGB 整數。</param>
        /// <returns>alpha 分量。</returns>
        public static byte AlphaOf(int argb)
        {
            return (byte)((argb >> 24) & 0xFF);
        }

        /// <summary>
        /// 取得 ARGB 整數的 red 分量。
        /// </summary>
        /// <param name="argb">要拆解的 ARGB 整數。</param>
        /// <returns>red 分量。</returns>
        public static byte RedOf(int argb)
        {
            return (byte)((argb >> 16) & 0xFF);
        }

        /// <summary>
        /// 取得 ARGB 整數的 green 分量。
        /// </summary>
        /// <param name="argb">要拆解的 ARGB 整數。</param>
        /// <returns>green 分量。</returns>
        public static byte GreenOf(int argb)
        {
            return (byte)((argb >> 8) & 0xFF);
        }

        /// <summary>
        /// 取得 ARGB 整數的 blue 分量。
        /// </summary>
        /// <param name="argb">要拆解的 ARGB 整數。</param>
        /// <returns>blue 分量。</returns>
        public static byte BlueOf(int argb)
        {
            return (byte)(argb & 0xFF);
        }
    }
}
