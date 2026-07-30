using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediaEmbedKit.Mpv.Hosting;

/// <summary>
/// 提供 <see cref="IServiceCollection"/> 註冊 MediaEmbedKit.Mpv 服務的擴充方法。
/// </summary>
public static class MpvServiceCollectionExtensions
{
    /// <summary>
    /// 註冊一個工廠，讓呼叫端可在執行階段非同步建立 <see cref="MpvPlayer"/>。
    /// </summary>
    /// <param name="services">
    /// 要登錄服務的服務集合。
    /// </param>
    /// <param name="configure">
    /// 用來設定 <see cref="MpvAppBuilder"/> 的委派。
    /// </param>
    /// <returns>
    /// 原始的 <see cref="IServiceCollection"/>，方便鏈式呼叫。
    /// </returns>
    /// <remarks>
    /// 此擴充方法適合配合 <c>IHostedService</c> 啟動流程使用：取得
    /// <see cref="IMpvPlayerFactory"/>，於 <c>StartAsync</c> 中非同步建構，
    /// 完成後再以 singleton 或 scoped 形式自行管理生命週期。為維持相容性，
    /// 也會註冊 <see cref="Func{TResult}"/> 形式的轉接器。
    /// </remarks>
    public static IServiceCollection AddMpvPlayerFactory(this IServiceCollection services, Action<MpvAppBuilder> configure)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.TryAddSingleton<IMpvPlayerFactory>(_ =>
        {
            return new MpvPlayerFactory(configure);
        });

        services.TryAddSingleton<Func<System.Threading.Tasks.Task<MpvPlayer>>>(serviceProvider =>
        {
            IMpvPlayerFactory factory = serviceProvider.GetRequiredService<IMpvPlayerFactory>();
            return () => factory.CreateAsync();
        });

        return services;
    }
}
