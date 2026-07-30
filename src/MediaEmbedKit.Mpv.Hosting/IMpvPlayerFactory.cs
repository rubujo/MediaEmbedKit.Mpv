using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Hosting;

/// <summary>
/// 提供可取消的非同步播放器建立入口。
/// </summary>
public interface IMpvPlayerFactory
{
    /// <summary>
    /// 建立並初始化新的 <see cref="MpvPlayer"/>。
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消建立流程的語彙基元。
    /// </param>
    /// <returns>
    /// 已初始化的播放器。
    /// </returns>
    Task<MpvPlayer> CreateAsync(CancellationToken cancellationToken = default);
}
