using System;
using System.Collections.Generic;
using MediaEmbedKit.Mpv.Internal;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv <c>video-params</c> 屬性的視訊參數。
    /// </summary>
    public sealed class MpvVideoParameters
    {
        /// <summary>
        /// 初始化 <see cref="MpvVideoParameters"/> 類別的新執行個體。
        /// </summary>
        /// <param name="pixelFormat">像素格式。</param>
        /// <param name="hardwarePixelFormat">硬體像素格式。</param>
        /// <param name="width">編碼寬度。</param>
        /// <param name="height">編碼高度。</param>
        /// <param name="displayWidth">顯示寬度。</param>
        /// <param name="displayHeight">顯示高度。</param>
        /// <param name="aspectRatio">顯示外觀比例。</param>
        /// <param name="pixelAspectRatio">像素外觀比例。</param>
        /// <param name="colorMatrix">色彩矩陣。</param>
        /// <param name="colorLevels">色彩範圍。</param>
        /// <param name="colorPrimaries">色彩原色。</param>
        /// <param name="gamma">轉換函式。</param>
        /// <param name="chromaLocation">色度位置。</param>
        /// <param name="rotation">旋轉角度。</param>
        /// <param name="stereoIn">立體視訊輸入模式。</param>
        /// <param name="averageBitsPerPixel">平均每像素位元數。</param>
        /// <param name="rawNode">原始節點資料。</param>
        internal MpvVideoParameters(
            string? pixelFormat,
            string? hardwarePixelFormat,
            long? width,
            long? height,
            long? displayWidth,
            long? displayHeight,
            double? aspectRatio,
            double? pixelAspectRatio,
            string? colorMatrix,
            string? colorLevels,
            string? colorPrimaries,
            string? gamma,
            string? chromaLocation,
            long? rotation,
            string? stereoIn,
            long? averageBitsPerPixel,
            MpvNode rawNode)
        {
            PixelFormat = pixelFormat;
            HardwarePixelFormat = hardwarePixelFormat;
            Width = width;
            Height = height;
            DisplayWidth = displayWidth;
            DisplayHeight = displayHeight;
            AspectRatio = aspectRatio;
            PixelAspectRatio = pixelAspectRatio;
            ColorMatrix = colorMatrix;
            ColorLevels = colorLevels;
            ColorPrimaries = colorPrimaries;
            Gamma = gamma;
            ChromaLocation = chromaLocation;
            Rotation = rotation;
            StereoIn = stereoIn;
            AverageBitsPerPixel = averageBitsPerPixel;
            RawNode = rawNode ?? throw new ArgumentNullException(nameof(rawNode));
        }

        /// <summary>
        /// 取得像素格式。
        /// </summary>
        /// <value>像素格式；沒有資料時為 <see langword="null"/>。</value>
        public string? PixelFormat { get; private set; }

        /// <summary>
        /// 取得硬體像素格式。
        /// </summary>
        /// <value>硬體像素格式；沒有資料時為 <see langword="null"/>。</value>
        public string? HardwarePixelFormat { get; private set; }

        /// <summary>
        /// 取得編碼寬度。
        /// </summary>
        /// <value>編碼寬度；沒有資料時為 <see langword="null"/>。</value>
        public long? Width { get; private set; }

        /// <summary>
        /// 取得編碼高度。
        /// </summary>
        /// <value>編碼高度；沒有資料時為 <see langword="null"/>。</value>
        public long? Height { get; private set; }

        /// <summary>
        /// 取得顯示寬度。
        /// </summary>
        /// <value>顯示寬度；沒有資料時為 <see langword="null"/>。</value>
        public long? DisplayWidth { get; private set; }

        /// <summary>
        /// 取得顯示高度。
        /// </summary>
        /// <value>顯示高度；沒有資料時為 <see langword="null"/>。</value>
        public long? DisplayHeight { get; private set; }

        /// <summary>
        /// 取得顯示外觀比例。
        /// </summary>
        /// <value>顯示外觀比例；沒有資料時為 <see langword="null"/>。</value>
        public double? AspectRatio { get; private set; }

        /// <summary>
        /// 取得像素外觀比例。
        /// </summary>
        /// <value>像素外觀比例；沒有資料時為 <see langword="null"/>。</value>
        public double? PixelAspectRatio { get; private set; }

        /// <summary>
        /// 取得色彩矩陣。
        /// </summary>
        /// <value>色彩矩陣；沒有資料時為 <see langword="null"/>。</value>
        public string? ColorMatrix { get; private set; }

        /// <summary>
        /// 取得色彩範圍。
        /// </summary>
        /// <value>色彩範圍；沒有資料時為 <see langword="null"/>。</value>
        public string? ColorLevels { get; private set; }

        /// <summary>
        /// 取得色彩原色。
        /// </summary>
        /// <value>色彩原色；沒有資料時為 <see langword="null"/>。</value>
        public string? ColorPrimaries { get; private set; }

        /// <summary>
        /// 取得轉換函式。
        /// </summary>
        /// <value>轉換函式；沒有資料時為 <see langword="null"/>。</value>
        public string? Gamma { get; private set; }

        /// <summary>
        /// 取得色度位置。
        /// </summary>
        /// <value>色度位置；沒有資料時為 <see langword="null"/>。</value>
        public string? ChromaLocation { get; private set; }

        /// <summary>
        /// 取得旋轉角度。
        /// </summary>
        /// <value>旋轉角度；沒有資料時為 <see langword="null"/>。</value>
        public long? Rotation { get; private set; }

        /// <summary>
        /// 取得立體視訊輸入模式。
        /// </summary>
        /// <value>立體視訊輸入模式；沒有資料時為 <see langword="null"/>。</value>
        public string? StereoIn { get; private set; }

        /// <summary>
        /// 取得平均每像素位元數。
        /// </summary>
        /// <value>平均每像素位元數；沒有資料時為 <see langword="null"/>。</value>
        public long? AverageBitsPerPixel { get; private set; }

        /// <summary>
        /// 取得原始節點資料。
        /// </summary>
        /// <value>來自 mpv 的原始視訊參數節點。</value>
        public MpvNode RawNode { get; private set; }

        /// <summary>
        /// 從 mpv 節點建立視訊參數。
        /// </summary>
        /// <param name="node">代表視訊參數的節點。</param>
        /// <returns>視訊參數。</returns>
        internal static MpvVideoParameters FromNode(MpvNode node)
        {
            IReadOnlyDictionary<string, MpvNode> map = node.AsMap();
            return new MpvVideoParameters(
                MpvNodeReader.GetString(map, "pixelformat"),
                MpvNodeReader.GetString(map, "hw-pixelformat"),
                MpvNodeReader.GetInt64(map, "w"),
                MpvNodeReader.GetInt64(map, "h"),
                MpvNodeReader.GetInt64(map, "dw"),
                MpvNodeReader.GetInt64(map, "dh"),
                MpvNodeReader.GetDouble(map, "aspect"),
                MpvNodeReader.GetDouble(map, "par"),
                MpvNodeReader.GetString(map, "colormatrix"),
                MpvNodeReader.GetString(map, "colorlevels"),
                MpvNodeReader.GetString(map, "primaries"),
                MpvNodeReader.GetString(map, "gamma"),
                MpvNodeReader.GetString(map, "chroma-location"),
                MpvNodeReader.GetInt64(map, "rotate"),
                MpvNodeReader.GetString(map, "stereo-in"),
                MpvNodeReader.GetInt64(map, "average-bpp"),
                node);
        }
    }
}
