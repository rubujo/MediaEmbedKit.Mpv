using System;

namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 表示建立 OpenGL render API 內容時使用的 DRM 顯示參數。
/// </summary>
public sealed class MpvOpenGlDrmDisplayOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvOpenGlDrmDisplayOptions"/> 類別的新執行個體。
    /// </summary>
    /// <param name="fileDescriptor">
    /// DRM 檔案描述元；無效時可使用 -1。
    /// </param>
    /// <param name="crtcId">
    /// 目前使用的 CRTC 識別碼。
    /// </param>
    /// <param name="connectorId">
    /// 目前使用的連接器識別碼。
    /// </param>
    public MpvOpenGlDrmDisplayOptions(int fileDescriptor, int crtcId, int connectorId)
    {
        FileDescriptor = fileDescriptor;
        CrtcId = crtcId;
        ConnectorId = connectorId;
        RenderFileDescriptor = -1;
    }

    /// <summary>
    /// 取得 DRM 檔案描述元。
    /// </summary>
    /// <value>
    /// DRM 檔案描述元；無效時可使用 -1。
    /// </value>
    public int FileDescriptor { get; private set; }

    /// <summary>
    /// 取得目前使用的 CRTC 識別碼。
    /// </summary>
    /// <value>
    /// CRTC 識別碼。
    /// </value>
    public int CrtcId { get; private set; }

    /// <summary>
    /// 取得目前使用的連接器識別碼。
    /// </summary>
    /// <value>
    /// 連接器識別碼。
    /// </value>
    public int ConnectorId { get; private set; }

    /// <summary>
    /// 取得或設定指向 DRM atomic request 指標的指標。
    /// </summary>
    /// <value>
    /// DRM atomic request 指標的指標；不使用時為 <see cref="IntPtr.Zero"/>。
    /// </value>
    public IntPtr AtomicRequestPointer { get; set; }

    /// <summary>
    /// 取得或設定 DRM render node 檔案描述元。
    /// </summary>
    /// <value>
    /// DRM render node 檔案描述元；無效時可使用 -1。
    /// </value>
    public int RenderFileDescriptor { get; set; }
}
