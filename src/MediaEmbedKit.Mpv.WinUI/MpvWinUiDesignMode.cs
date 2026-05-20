using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;

namespace MediaEmbedKit.Mpv.WinUI;

/// <summary>
/// 提供 WinUI 控制項在設計階段使用的共用協助工具。
/// </summary>
internal static class MpvWinUiDesignMode
{
    /// <summary>
    /// 取得目前是否在 WinUI 設計工具中執行。
    /// </summary>
    /// <value>
    /// 控制項位於設計階段時為 <see langword="true"/>。
    /// </value>
    internal static bool IsEnabled
    {
        get { return DesignMode.DesignModeEnabled || DesignMode.DesignMode2Enabled; }
    }

    /// <summary>
    /// 建立設計階段替代預覽元素。
    /// </summary>
    /// <param name="caption">
    /// 要顯示在預覽元素中央的文字。
    /// </param>
    /// <returns>
    /// 可放入 WinUI 視覺樹的替代預覽元素。
    /// </returns>
    internal static UIElement CreatePlaceholder(string caption)
    {
        TextBlock textBlock = new TextBlock
        {
            Text = caption,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        Border border = new Border
        {
            Background = new SolidColorBrush(Colors.Black),
            Child = textBlock
        };

        return border;
    }
}
