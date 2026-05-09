using System;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv 記憶體截圖結果。
    /// </summary>
    public sealed class MpvScreenshot
    {
        /// <summary>
        /// 初始化 <see cref="MpvScreenshot"/> 類別的新執行個體。
        /// </summary>
        /// <param name="width">截圖寬度。</param>
        /// <param name="height">截圖高度。</param>
        /// <param name="stride">截圖每列位元組距離。</param>
        /// <param name="format">截圖像素格式文字。</param>
        /// <param name="data">截圖像素資料。</param>
        /// <param name="rawNode">原始節點資料。</param>
        internal MpvScreenshot(int width, int height, int stride, string format, byte[] data, MpvNode rawNode)
        {
            Width = width;
            Height = height;
            Stride = stride;
            Format = format;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
        }

        /// <summary>
        /// 取得截圖寬度。
        /// </summary>
        /// <value>截圖寬度，以像素為單位。</value>
        public int Width { get; private set; }

        /// <summary>
        /// 取得截圖高度。
        /// </summary>
        /// <value>截圖高度，以像素為單位。</value>
        public int Height { get; private set; }

        /// <summary>
        /// 取得截圖每列位元組距離。
        /// </summary>
        /// <value>從一列像素移動到下一列像素所需的位元組數。</value>
        public int Stride { get; private set; }

        /// <summary>
        /// 取得截圖像素格式文字。
        /// </summary>
        /// <value>mpv 回報的像素格式文字。</value>
        public string Format { get; private set; }

        /// <summary>
        /// 取得截圖像素資料。
        /// </summary>
        /// <value>截圖像素資料副本。</value>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 取得原始節點資料。
        /// </summary>
        /// <value>來自 mpv 的原始截圖節點。</value>
        public MpvNode RawNode { get; private set; }

        /// <summary>
        /// 從 mpv 節點建立記憶體截圖結果。
        /// </summary>
        /// <param name="node">代表截圖結果的節點。</param>
        /// <returns>記憶體截圖結果。</returns>
        internal static MpvScreenshot FromNode(MpvNode node)
        {
            return new MpvScreenshot(
                checked((int)node.GetValueOrNone("w").AsInt64()),
                checked((int)node.GetValueOrNone("h").AsInt64()),
                checked((int)node.GetValueOrNone("stride").AsInt64()),
                node.GetValueOrNone("format").AsString() ?? string.Empty,
                node.GetValueOrNone("data").Value as byte[] ?? Array.Empty<byte>(),
                node);
        }
    }
}
