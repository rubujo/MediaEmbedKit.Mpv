using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using MediaEmbedKit.Mpv.Externals;

namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 紀錄 <c>runtime/libmpv-2.dll</c> 上次安裝的來源元資料，供
/// <see cref="MpvWindowsBuildDownloader.DownloadAndExtractLatestLibMpvAsync"/>
/// 做 idempotency skip 比對。
/// </summary>
/// <remarks>
/// libmpv-2.dll 沒有內建可程式化讀取的版本字串（FileVersionInfo 的版本與
/// shinchiro / zhongfly release tag 不直接對應），所以 helper 在每次成功安裝後
/// 把 release 元資料寫到同目錄的 <c>libmpv-2.dll.version.json</c> sidecar，下次
/// 安裝時讀此檔比對上游當前 release。匹配（同 provider + 同 releaseTag + 同
/// assetName）→ 跳過下載 + 解壓。
/// </remarks>
internal sealed class LibMpvVersionMarker
{
    /// <summary>Marker 檔的副檔名後綴。</summary>
    public const string FileExtension = ".version.json";

    /// <summary>當前 marker schema 版本；未來欄位演進可用此辨識。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Marker schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>產生此 marker 的 provider（<see cref="MpvWindowsBuildProvider"/> 的字串形式）。</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>GitHub release tag 名稱。</summary>
    [JsonPropertyName("releaseTag")]
    public string ReleaseTag { get; set; } = string.Empty;

    /// <summary>下載 / 解壓的 asset 檔名（含 libmpv-dev / -lgpl / arch 等變體區分）。</summary>
    [JsonPropertyName("assetName")]
    public string AssetName { get; set; } = string.Empty;

    /// <summary>從 marker 檔讀取；檔案不存在 / 解析失敗 / schema 版本不認識皆回傳 null。</summary>
    /// <param name="markerPath">marker 檔完整路徑（含 <see cref="FileExtension"/> 後綴）。</param>
    /// <returns>讀到且 schema 認得時為 <see cref="LibMpvVersionMarker"/> 實例；否則為 <see langword="null"/>。</returns>
    public static LibMpvVersionMarker? TryRead(string markerPath)
    {
        if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(markerPath);
            LibMpvVersionMarker? marker = JsonSerializer.Deserialize(json, LibMpvVersionMarkerJsonContext.Default.LibMpvVersionMarker);
            if (marker == null || marker.SchemaVersion != CurrentSchemaVersion)
            {
                return null;
            }

            return marker;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 寫入 marker 檔（atomic）：先寫到 <c>{markerPath}.tmp</c>，成功後 rename 取代目標檔。
    /// 避免系統當機 / 程序強制中斷期間留下半寫入 JSON。寫入失敗不擲例外
    /// （marker 是 best-effort 快取，缺失只是下次重抓）。
    /// </summary>
    /// <param name="markerPath">marker 檔完整路徑。</param>
    /// <param name="provider">本次安裝來源 provider。</param>
    /// <param name="releaseTag">本次安裝的 release tag。</param>
    /// <param name="assetName">本次安裝的 asset 檔名。</param>
    public static void Write(string markerPath, MpvWindowsBuildProvider provider, string releaseTag, string assetName)
    {
        LibMpvVersionMarker marker = new LibMpvVersionMarker
        {
            SchemaVersion = CurrentSchemaVersion,
            Provider = provider.ToString(),
            ReleaseTag = releaseTag ?? string.Empty,
            AssetName = assetName ?? string.Empty,
        };

        string tempPath = markerPath + ".tmp";
        try
        {
            string json = JsonSerializer.Serialize(marker, LibMpvVersionMarkerJsonContext.Default.LibMpvVersionMarker);
            File.WriteAllText(tempPath, json);

            // File.Move(overwrite) 在 .NET Core 3+ atomic；netstandard2.0 / net472 需先刪舊檔
            // 後 Move（短時間窗口下不 atomic，但只造成 marker 缺失，下次重抓一次自動修正）。
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
            File.Move(tempPath, markerPath, overwrite: true);
#else
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }

            File.Move(tempPath, markerPath);
#endif
        }
        catch (IOException)
        {
            TryDeleteTempMarker(tempPath);
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTempMarker(tempPath);
        }
    }

    /// <summary>清理 atomic write 失敗時殘留的 .tmp 檔，失敗也吞掉。</summary>
    private static void TryDeleteTempMarker(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// <see cref="LibMpvVersionMarker"/> 的 source-generated JSON context（trim / AOT 友善）。
/// </summary>
[JsonSerializable(typeof(LibMpvVersionMarker))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class LibMpvVersionMarkerJsonContext : JsonSerializerContext
{
}
