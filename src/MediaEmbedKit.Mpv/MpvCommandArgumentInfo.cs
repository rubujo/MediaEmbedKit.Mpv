namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 mpv 命令清單中的單一引數描述。
/// </summary>
public sealed class MpvCommandArgumentInfo
{
    /// <summary>
    /// 初始化 <see cref="MpvCommandArgumentInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">命令引數名稱。</param>
    /// <param name="type">命令引數類型文字。</param>
    /// <param name="optional">命令引數是否為選擇性引數。</param>
    internal MpvCommandArgumentInfo(string name, string type, bool optional)
    {
        Name = name;
        Type = type;
        Optional = optional;
    }

    /// <summary>
    /// 取得命令引數名稱。
    /// </summary>
    /// <value>命令引數名稱。</value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得命令引數類型文字。
    /// </summary>
    /// <value>mpv 回報的引數類型文字。</value>
    public string Type { get; private set; }

    /// <summary>
    /// 取得命令引數是否為選擇性引數。
    /// </summary>
    /// <value>引數為選擇性時為 <see langword="true"/>。</value>
    public bool Optional { get; private set; }
}
