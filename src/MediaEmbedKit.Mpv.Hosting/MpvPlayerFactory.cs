using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Hosting;

/// <summary>
/// 依指定的 <see cref="MpvAppBuilder"/> 設定建立播放器。
/// </summary>
public sealed class MpvPlayerFactory : IMpvPlayerFactory
{
    private readonly Action<MpvAppBuilder> _configure;

    /// <summary>
    /// 初始化 <see cref="MpvPlayerFactory"/> 類別的新執行個體。
    /// </summary>
    /// <param name="configure">
    /// 每次建立播放器時套用的 builder 設定。
    /// </param>
    public MpvPlayerFactory(Action<MpvAppBuilder> configure)
    {
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    /// <inheritdoc/>
    public Task<MpvPlayer> CreateAsync(CancellationToken cancellationToken = default)
    {
        MpvAppBuilder builder = new MpvAppBuilder();
        _configure(builder);
        return builder.BuildAsync(cancellationToken);
    }
}
