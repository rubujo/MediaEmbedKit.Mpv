namespace MediaEmbedKit.Mpv.Platforms;

/// <summary>
/// 定義專案追蹤的 UI 架構。
/// </summary>
public enum MpvUiFramework
{
    /// <summary>
    /// Windows Forms UI 架構。
    /// </summary>
    WinForms = 0,
    /// <summary>
    /// Windows Presentation Foundation UI 架構。
    /// </summary>
    Wpf = 1,
    /// <summary>
    /// Avalonia UI 架構。
    /// </summary>
    Avalonia = 2,
    /// <summary>
    /// Windows App SDK WinUI 3 UI 架構。
    /// </summary>
    WinUI3 = 3,
    /// <summary>
    /// .NET MAUI UI 架構。
    /// </summary>
    Maui = 4
}
