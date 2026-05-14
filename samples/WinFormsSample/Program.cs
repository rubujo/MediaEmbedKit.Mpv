using System;
using System.Windows.Forms;

namespace MediaEmbedKit.Mpv.Samples.WinForms;

/// <summary>
/// 提供 WinForms 範例應用程式的進入點。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 啟動 WinForms 範例應用程式。
    /// </summary>
    /// <remarks>
    /// <see cref="ApplicationConfiguration.Initialize"/> 是 .NET 6+ source-gen 出來的入口，會依
    /// csproj 內 <c>&lt;ApplicationVisualStyles&gt;</c> / <c>&lt;ApplicationUseCompatibleTextRendering&gt;</c>
    /// / <c>&lt;ApplicationHighDpiMode&gt;</c> 等屬性產出對應呼叫，取代手動 <c>EnableVisualStyles</c> +
    /// <c>SetCompatibleTextRenderingDefault</c> 兩行 bootstrap。
    /// </remarks>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
