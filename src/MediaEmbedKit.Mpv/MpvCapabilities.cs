using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供目前 libmpv 用戶端的可用功能快照與便利查詢方法。
/// </summary>
public sealed class MpvCapabilities
{
    /// <summary>
    /// 初始化 <see cref="MpvCapabilities"/> 類別的新執行個體。
    /// </summary>
    /// <param name="clientApiVersion">
    /// 由 libmpv 回報的 client API 版本。
    /// </param>
    /// <param name="mpvVersion">
    /// 由 libmpv 回報的 mpv 版本字串。
    /// </param>
    /// <param name="mpvConfiguration">
    /// 由 libmpv 回報的 mpv 編譯設定字串。
    /// </param>
    /// <param name="protocols">
    /// libmpv 目前支援的通訊協定名稱集合。
    /// </param>
    /// <param name="decoders">
    /// libmpv 目前支援的解碼器資訊集合。
    /// </param>
    /// <param name="demuxers">
    /// libmpv 目前支援的 demuxer 名稱集合。
    /// </param>
    internal MpvCapabilities(
        Version clientApiVersion,
        string mpvVersion,
        string mpvConfiguration,
        IReadOnlyList<string> protocols,
        IReadOnlyList<MpvDecoderInfo> decoders,
        IReadOnlyList<string> demuxers)
    {
        ClientApiVersion = clientApiVersion ?? throw new ArgumentNullException(nameof(clientApiVersion));
        MpvVersion = mpvVersion ?? string.Empty;
        MpvConfiguration = mpvConfiguration ?? string.Empty;
        Protocols = protocols ?? Array.Empty<string>();
        Decoders = decoders ?? Array.Empty<MpvDecoderInfo>();
        Demuxers = demuxers ?? Array.Empty<string>();
    }

    /// <summary>
    /// 取得 libmpv 回報的 client API 版本。
    /// </summary>
    /// <value>
    /// 解析自 <see cref="MpvPlayer.ClientApiVersion"/> 的 major.minor 版本。
    /// </value>
    public Version ClientApiVersion { get; }

    /// <summary>
    /// 取得 libmpv 回報的 mpv 版本字串。
    /// </summary>
    /// <value>
    /// 例如 <c>mpv 0.41.0</c> 或包含 提供者 git build 標記的版本文字。
    /// </value>
    public string MpvVersion { get; }

    /// <summary>
    /// 取得 libmpv 回報的 mpv 編譯設定字串。
    /// </summary>
    /// <value>
    /// 來自 <c>mpv-configuration</c> 屬性的編譯選項字串。
    /// </value>
    public string MpvConfiguration { get; }

    /// <summary>
    /// 取得 libmpv 目前支援的通訊協定名稱集合。
    /// </summary>
    /// <value>
    /// 例如 <c>file</c>、<c>http</c>、<c>https</c>、<c>ytdl</c>。
    /// </value>
    public IReadOnlyList<string> Protocols { get; }

    /// <summary>
    /// 取得 libmpv 目前支援的解碼器資訊集合。
    /// </summary>
    /// <value>
    /// 對應 <c>decoder-list</c> 屬性的解碼器明細。
    /// </value>
    public IReadOnlyList<MpvDecoderInfo> Decoders { get; }

    /// <summary>
    /// 取得 libmpv 目前支援的 demuxer 名稱集合。
    /// </summary>
    /// <value>
    /// 例如 <c>mp4</c>、<c>matroska</c>、<c>mpegts</c>。
    /// </value>
    public IReadOnlyList<string> Demuxers { get; }

    /// <summary>
    /// 判斷 libmpv 是否支援指定的通訊協定。
    /// </summary>
    /// <param name="scheme">
    /// 要查詢的協定名稱，例如 <c>https</c>。
    /// </param>
    /// <returns>
    /// 支援時為 <see langword="true"/>。
    /// </returns>
    public bool SupportsProtocol(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return false;
        }

        for (int index = 0; index < Protocols.Count; index++)
        {
            if (string.Equals(Protocols[index], scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判斷 libmpv 是否包含指定名稱的 demuxer。
    /// </summary>
    /// <param name="name">
    /// 要查詢的 demuxer 名稱。
    /// </param>
    /// <returns>
    /// 包含時為 <see langword="true"/>。
    /// </returns>
    public bool ContainsDemuxer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int index = 0; index < Demuxers.Count; index++)
        {
            if (string.Equals(Demuxers[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判斷 libmpv 是否包含指定名稱的解碼器。
    /// </summary>
    /// <param name="codec">
    /// 要查詢的解碼器 codec 名稱，例如 <c>h264</c>。
    /// </param>
    /// <returns>
    /// 包含時為 <see langword="true"/>。
    /// </returns>
    public bool ContainsDecoder(string codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
        {
            return false;
        }

        for (int index = 0; index < Decoders.Count; index++)
        {
            MpvDecoderInfo decoder = Decoders[index];
            if (string.Equals(decoder.Codec, codec, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
