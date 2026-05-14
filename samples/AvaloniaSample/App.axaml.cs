using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MediaEmbedKit.Mpv.Samples.Avalonia;

/// <summary>
/// 表示 Avalonia 範例應用程式。樣式定義在 <c>App.axaml</c>。
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// 載入 <c>App.axaml</c> 中宣告的樣式與資源。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 在 Avalonia framework 初始化完成後建立主要視窗。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
