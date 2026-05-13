using Microsoft.Maui.Hosting;

namespace MediaEmbedKit.Mpv.Maui;

/// <summary>
/// 提供 .NET MAUI 應用程式註冊 MediaEmbedKit.Mpv 控制項的擴充方法。
/// </summary>
public static class MpvMauiAppBuilderExtensions
{
    /// <summary>
    /// 將 MediaEmbedKit.Mpv MAUI handler 加入指定的 MAUI 應用程式建置器。
    /// </summary>
    /// <param name="builder">要設定的 MAUI 應用程式建置器。</param>
    /// <returns>原始 MAUI 應用程式建置器。</returns>
    public static MauiAppBuilder UseMediaEmbedKitMpv(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<MpvView, MpvViewHandler>();
        });

        return builder;
    }
}
