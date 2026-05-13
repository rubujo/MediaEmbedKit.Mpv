using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace MediaEmbedKit.Mpv.Samples.Avalonia
{
    /// <summary>
    /// 表示 Avalonia 範例應用程式。
    /// </summary>
    public sealed class App : Application
    {
        /// <summary>
        /// 初始化 Avalonia 範例應用程式的樣式。
        /// </summary>
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
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
}
