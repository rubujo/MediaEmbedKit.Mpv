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
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
