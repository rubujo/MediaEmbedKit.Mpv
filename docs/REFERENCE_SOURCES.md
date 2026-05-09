# 參考來源

本檔記錄目前專案規範採用的主要官方來源。最後查核日期：2026-05-09。

## libmpv 與 mpv

- mpv manual stable，libmpv embedding 與 render API：https://mpv.io/manual/stable/#embedding-into-other-programs-libmpv
- mpv GitHub 專案：https://github.com/mpv-player/mpv
- mpv GitHub releases，最新 stable 標籤 v0.41.0：https://github.com/mpv-player/mpv/releases
- mpv 安裝頁：https://mpv.io/installation/
- mpv v0.41.0 `client.h`、`render.h`、`render_gl.h`：https://github.com/mpv-player/mpv/tree/v0.41.0/include/mpv
- mpv `DOCS/interface-changes.rst`：https://github.com/mpv-player/mpv/blob/master/DOCS/interface-changes.rst

## .NET 與 UI 平台

- .NET Standard 與 .NET Framework 支援建議：https://learn.microsoft.com/dotnet/standard/net-standard
- Visual Studio 編碼與換行：https://learn.microsoft.com/visualstudio/ide/encodings-and-line-breaks
- Visual Studio 以指定編碼儲存檔案：https://learn.microsoft.com/visualstudio/ide/how-to-save-and-open-files-with-encoding
- MSVC 原始碼字元集與 BOM 判斷：https://learn.microsoft.com/cpp/build/reference/validate-charset-validate-for-compatible-characters
- .NET MAUI 支援平台：https://learn.microsoft.com/dotnet/maui/supported-platforms
- .NET MAUI handlers：https://learn.microsoft.com/dotnet/maui/user-interface/handlers/
- .NET MAUI handler 自訂與 `PlatformView`：https://learn.microsoft.com/dotnet/maui/user-interface/handlers/customize
- .NET MAUI `DesignMode.IsDesignModeEnabled`：https://learn.microsoft.com/dotnet/api/microsoft.maui.controls.designmode.isdesignmodeenabled
- WinForms design-time 支援：https://learn.microsoft.com/dotnet/desktop/winforms/controls-design/designer-overview
- WinUI 3 HWND 互通：https://learn.microsoft.com/windows/apps/develop/ui/retrieve-hwnd
- WinUI 3 視窗與實體像素座標：https://learn.microsoft.com/windows/apps/develop/ui/windowing-overview
- WinUI 3 `DesktopWindowXamlSource.Initialize`：https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.hosting.desktopwindowxamlsource.initialize
- WinUI 3 `DesktopWindowXamlSource.SiteBridge`：https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.hosting.desktopwindowxamlsource.sitebridge
- WinUI 3 `DesignMode`：https://learn.microsoft.com/uwp/api/windows.applicationmodel.designmode
- Win32 子視窗與裁切規則：https://learn.microsoft.com/windows/win32/winmsg/window-features
- Win32 `CreateWindowExW`：https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-createwindowexw
- Win32 `SetWindowPos`：https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos
- WPF 與 Win32 互通：https://learn.microsoft.com/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation
- WPF `DesignerProperties`：https://learn.microsoft.com/dotnet/api/system.componentmodel.designerproperties
- WPF Technology Regions 與 AirSpace：https://learn.microsoft.com/dotnet/desktop/wpf/advanced/technology-regions-overview
- Avalonia IDE 支援：https://docs.avaloniaui.net/tools/ide/
- Avalonia Visual Studio extension：https://docs.avaloniaui.net/tools/visual-studio-extension
- Avalonia `Design` helper：https://docs.avaloniaui.net/api/avalonia/controls/design
- Avalonia 支援平台：https://docs.avaloniaui.net/docs/overview/supported-platforms
- Avalonia `TopLevel` 與 `RenderScaling`：https://docs.avaloniaui.net/docs/fundamentals/top-level
- Avalonia `OpenGlControlBase`：https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase
- Avalonia native platform interop 與 `NativeControlHost` 限制：https://docs.avaloniaui.net/docs/app-development/native-interop
- Avalonia Windows HWND 原生控制項嵌入：https://docs.avaloniaui.net/docs/platform-specific-guides/windows

## 執行階段工具

- shinchiro Windows mpv build：https://github.com/shinchiro/mpv-winbuild-cmake
- shinchiro 最新已對齊 release `20260421`：https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260421
- zhongfly Windows mpv build：https://github.com/zhongfly/mpv-winbuild
- zhongfly 最新已對齊 release `2026-05-08-e0eb42c303`：https://github.com/zhongfly/mpv-winbuild/releases/tag/2026-05-08-e0eb42c303
- yt-dlp：https://github.com/yt-dlp/yt-dlp
- yt-dlp release files：https://github.com/yt-dlp/yt-dlp/blob/master/README.md#release-files
- yt-dlp EJS 指南：https://github.com/yt-dlp/yt-dlp/wiki/EJS
- yt-dlp 外部 JavaScript runtime 公告：https://github.com/yt-dlp/yt-dlp/issues/15012
- yt-dlp 輸出與進度選項：https://github.com/yt-dlp/yt-dlp/blob/master/README.md#verbosity-and-simulation-options
- mpv `log-message` 與 ytdl hook user-data：https://mpv.io/manual/stable/#command-interface-log-message
- Deno 安裝文件：https://docs.deno.com/runtime/getting_started/installation/
- Deno 下載 tuple：https://dl.deno.land/
- Deno releases：https://github.com/denoland/deno/releases

## AI Agent 與 Skill

- Codex `AGENTS.md` 文件：https://developers.openai.com/codex/guides/agents-md
- Codex Agent Skills 文件：https://developers.openai.com/codex/skills
- AGENTS.md 開放格式：https://github.com/agentsmd/agents.md
- Claude Code Skills 文件：https://docs.claude.com/en/docs/claude-code/skills
- Gemini CLI `GEMINI.md` 文件：https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/gemini-md.md
- GitHub Copilot CLI custom instructions：https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions
- Microsoft Agent Skills 文件：https://learn.microsoft.com/agent-framework/agents/skills
- 慣例式提交 1.0.0 繁體中文規範：https://www.conventionalcommits.org/zh-hant/v1.0.0/
