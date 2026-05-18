using System.Runtime.CompilerServices;

// .Runtime 用 core 的 internal SetRuntimePreparer（透過 MpvAppBuilderRuntimeExtensions
// 擴充方法注入 runtime 安裝邏輯，避免 core 反向依賴 .Runtime）。其餘衍生套件
// (.Externals / .Diagnostics / .Hosting) 經驗證未使用 core internal 成員，不必 IVT。
[assembly: InternalsVisibleTo("MediaEmbedKit.Mpv.Runtime")]
[assembly: InternalsVisibleTo("MediaEmbedKit.Mpv.Tests")]
[assembly: InternalsVisibleTo("MediaEmbedKit.Mpv.IntegrationTests")]
