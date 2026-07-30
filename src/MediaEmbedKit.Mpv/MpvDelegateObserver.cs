using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 將 callback 轉接為 <see cref="IObserver{T}"/>。
/// </summary>
/// <typeparam name="T">
/// 觀察值型別。
/// </typeparam>
internal sealed class MpvDelegateObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception>? _onError;
    private readonly Action? _onCompleted;

    /// <summary>
    /// 初始化 <see cref="MpvDelegateObserver{T}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="onNext">
    /// 收到屬性值時執行的 callback。
    /// </param>
    /// <param name="onError">
    /// 觀察失敗時執行的 callback。
    /// </param>
    /// <param name="onCompleted">
    /// 觀察完成時執行的 callback。
    /// </param>
    internal MpvDelegateObserver(
        Action<T> onNext,
        Action<Exception>? onError,
        Action? onCompleted)
    {
        _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onError = onError;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// 通知觀察已完成。
    /// </summary>
    public void OnCompleted()
    {
        _onCompleted?.Invoke();
    }

    /// <summary>
    /// 通知觀察發生錯誤。
    /// </summary>
    /// <param name="error">
    /// 觀察期間發生的錯誤。
    /// </param>
    public void OnError(Exception error)
    {
        if (_onError != null)
        {
            _onError(error);
            return;
        }

        throw error;
    }

    /// <summary>
    /// 通知最新的屬性值。
    /// </summary>
    /// <param name="value">
    /// 最新的屬性值。
    /// </param>
    public void OnNext(T value)
    {
        _onNext(value);
    }
}
