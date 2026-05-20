using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv <c>audio-params</c> 屬性的音訊參數。
/// </summary>
public sealed class MpvAudioParameters
{
    /// <summary>
    /// 初始化 <see cref="MpvAudioParameters"/> 類別的新執行個體。
    /// </summary>
    /// <param name="format">
    /// 音訊取樣格式。
    /// </param>
    /// <param name="sampleRate">
    /// 取樣率。
    /// </param>
    /// <param name="channels">
    /// 聲道配置。
    /// </param>
    /// <param name="channelCount">
    /// 聲道數量。
    /// </param>
    /// <param name="hardwareChannels">
    /// 硬體聲道配置。
    /// </param>
    /// <param name="rawNode">
    /// 原始節點資料。
    /// </param>
    internal MpvAudioParameters(string? format, long? sampleRate, string? channels, long? channelCount, string? hardwareChannels, MpvNode rawNode)
    {
        Format = format;
        SampleRate = sampleRate;
        Channels = channels;
        ChannelCount = channelCount;
        HardwareChannels = hardwareChannels;
        RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
    }

    /// <summary>
    /// 取得音訊取樣格式。
    /// </summary>
    /// <value>
    /// 音訊取樣格式；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Format { get; private set; }

    /// <summary>
    /// 取得取樣率。
    /// </summary>
    /// <value>
    /// 取樣率；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? SampleRate { get; private set; }

    /// <summary>
    /// 取得聲道配置。
    /// </summary>
    /// <value>
    /// 聲道配置；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? Channels { get; private set; }

    /// <summary>
    /// 取得聲道數量。
    /// </summary>
    /// <value>
    /// 聲道數量；沒有資料時為 <see langword="null"/>。
    /// </value>
    public long? ChannelCount { get; private set; }

    /// <summary>
    /// 取得硬體聲道配置。
    /// </summary>
    /// <value>
    /// 硬體聲道配置；沒有資料時為 <see langword="null"/>。
    /// </value>
    public string? HardwareChannels { get; private set; }

    /// <summary>
    /// 取得原始節點資料。
    /// </summary>
    /// <value>
    /// 來自 mpv 的原始音訊參數節點。
    /// </value>
    public MpvNode RawNode { get; private set; }

    /// <summary>
    /// 從 mpv 節點建立音訊參數。
    /// </summary>
    /// <param name="node">
    /// 代表音訊參數的節點。
    /// </param>
    /// <returns>
    /// 音訊參數。
    /// </returns>
    internal static MpvAudioParameters FromNode(MpvNode node)
    {
        IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
        return new MpvAudioParameters(
            MpvNodeReader.GetString(map, "format"),
            MpvNodeReader.GetInt64(map, "samplerate"),
            MpvNodeReader.GetString(map, "channels"),
            MpvNodeReader.GetInt64(map, "channel-count"),
            MpvNodeReader.GetString(map, "hr-channels"),
            node);
    }
}
