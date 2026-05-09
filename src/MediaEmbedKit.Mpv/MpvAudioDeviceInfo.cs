using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv <c>audio-device-list</c> 屬性中的單一音訊裝置。
    /// </summary>
    public sealed class MpvAudioDeviceInfo
    {
        /// <summary>
        /// 初始化 <see cref="MpvAudioDeviceInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="name">音訊裝置名稱。</param>
        /// <param name="description">音訊裝置描述。</param>
        /// <param name="rawNode">原始節點資料。</param>
        internal MpvAudioDeviceInfo(string name, string? description, MpvNode rawNode)
        {
            Name = name;
            Description = description;
            RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
        }

        /// <summary>
        /// 取得音訊裝置名稱。
        /// </summary>
        /// <value>可傳給 mpv <c>audio-device</c> 屬性的裝置名稱。</value>
        public string Name { get; private set; }

        /// <summary>
        /// 取得音訊裝置描述。
        /// </summary>
        /// <value>裝置描述；沒有資料時為 <see langword="null"/>。</value>
        public string? Description { get; private set; }

        /// <summary>
        /// 取得原始節點資料。
        /// </summary>
        /// <value>來自 mpv 的原始音訊裝置節點。</value>
        public MpvNode RawNode { get; private set; }

        /// <summary>
        /// 從 mpv 節點建立音訊裝置資訊。
        /// </summary>
        /// <param name="node">代表單一音訊裝置的節點。</param>
        /// <returns>音訊裝置資訊。</returns>
        internal static MpvAudioDeviceInfo FromNode(MpvNode node)
        {
            IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
            return new MpvAudioDeviceInfo(
                MpvNodeReader.GetString(map, "name") ?? string.Empty,
                MpvNodeReader.GetString(map, "description"),
                node);
        }
    }
}
