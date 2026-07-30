using System;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供 UI 控制項操作失敗的詳細資料。
/// </summary>
public sealed class MpvControlOperationFailedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化 <see cref="MpvControlOperationFailedEventArgs"/> 類別的新執行個體。
    /// </summary>
    /// <param name="operation">
    /// 失敗的操作種類。
    /// </param>
    /// <param name="exception">
    /// 操作失敗的例外。
    /// </param>
    /// <param name="source">
    /// 操作涉及的媒體來源；不適用時為 <see langword="null"/>。
    /// </param>
    public MpvControlOperationFailedEventArgs(
        MpvControlOperation operation,
        Exception exception,
        string? source = null)
    {
        Operation = operation;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Source = source;
    }

    /// <summary>
    /// 取得失敗的操作種類。
    /// </summary>
    /// <value>
    /// 操作種類。
    /// </value>
    public MpvControlOperation Operation { get; }

    /// <summary>
    /// 取得操作失敗的例外。
    /// </summary>
    /// <value>
    /// 原始例外。
    /// </value>
    public Exception Exception { get; }

    /// <summary>
    /// 取得操作涉及的媒體來源。
    /// </summary>
    /// <value>
    /// 媒體來源；不適用時為 <see langword="null"/>。
    /// </value>
    public string? Source { get; }
}
