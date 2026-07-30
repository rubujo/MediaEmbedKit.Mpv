# 參考來源

本文件列出專案規範採用的主要來源。最後查核日期：2026-07-30。

## libmpv 與 mpv

- mpv manual stable：https://mpv.io/manual/stable/#embedding-into-other-programs-libmpv
- mpv manual stable（properties：`playlist-pos`、`chapter`）：https://mpv.io/manual/stable/
- mpv GitHub：https://github.com/mpv-player/mpv
- mpv releases：https://github.com/mpv-player/mpv/releases
- mpv 安裝頁：https://mpv.io/installation/
- mpv v0.41.0 headers：https://github.com/mpv-player/mpv/tree/v0.41.0/include/mpv
- mpv interface changes：https://github.com/mpv-player/mpv/blob/master/DOCS/interface-changes.rst

## .NET 與 UI

- .NET Standard：https://learn.microsoft.com/dotnet/standard/net-standard
- Visual Studio 編碼與換行：https://learn.microsoft.com/visualstudio/ide/encodings-and-line-breaks
- WinForms overview：https://learn.microsoft.com/dotnet/desktop/winforms/overview/
- WinForms simple data binding：https://learn.microsoft.com/dotnet/desktop/winforms/data/how-to-create-control-binding
- WinForms `Control.DataBindings`：https://learn.microsoft.com/dotnet/api/system.windows.forms.control.databindings
- `INotifyPropertyChanged`：https://learn.microsoft.com/dotnet/api/system.componentmodel.inotifypropertychanged
- .NET MAUI 支援平台：https://learn.microsoft.com/dotnet/maui/supported-platforms
- .NET MAUI handlers：https://learn.microsoft.com/dotnet/maui/user-interface/handlers/
- WinForms design-time：https://learn.microsoft.com/dotnet/desktop/winforms/controls-design/designer-overview
- WinUI HWND 互通：https://learn.microsoft.com/windows/apps/develop/ui/retrieve-hwnd
- Windows App SDK 部署：https://learn.microsoft.com/windows/apps/package-and-deploy/deploy-overview
- WPF 與 Win32 互通：https://learn.microsoft.com/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation
- WPF AirSpace：https://learn.microsoft.com/dotnet/desktop/wpf/advanced/technology-regions-overview
- Avalonia IDE：https://docs.avaloniaui.net/tools/ide/
- Avalonia OpenGL：https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase
- Avalonia native interop：https://docs.avaloniaui.net/docs/app-development/native-interop

## 執行階段工具

- shinchiro mpv build：https://github.com/shinchiro/mpv-winbuild-cmake
- zhongfly mpv build：https://github.com/zhongfly/mpv-winbuild
- yt-dlp：https://github.com/yt-dlp/yt-dlp
- yt-dlp release files：https://github.com/yt-dlp/yt-dlp/blob/master/README.md#release-files
- yt-dlp EJS：https://github.com/yt-dlp/yt-dlp/wiki/EJS
- yt-dlp 外部 JavaScript 執行階段：https://github.com/yt-dlp/yt-dlp/issues/15012
- yt-dlp FFmpeg-Builds：https://github.com/yt-dlp/FFmpeg-Builds
- Deno 安裝：https://docs.deno.com/runtime/getting_started/installation/
- Deno upgrade checksum：https://docs.deno.com/runtime/reference/cli/upgrade/#checksum-verification
- Deno 2.7 release note：https://deno.com/blog/v2.7
- Deno releases：https://github.com/denoland/deno/releases
- Chrome VersionHistory API：https://developer.chrome.com/docs/web-platform/versionhistory/guide
- GitHub Releases 資產 digest：https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/
- GitHub Releases REST API：https://docs.github.com/en/rest/releases/releases

## 編碼 與編碼器

- mpv encoding 文件：https://github.com/mpv-player/mpv/blob/master/DOCS/encoding.rst
- mpv encode 選項：https://github.com/mpv-player/mpv/blob/master/DOCS/man/encode.rst
- mpv EDL v0：https://github.com/mpv-player/mpv/blob/master/DOCS/edl-mpv.rst
- FFmpeg ffmpeg-codecs：https://ffmpeg.org/ffmpeg-codecs.html
- FFmpeg libsvtav1 two-pass commit `5ba2525`（2026-02-25）：https://github.com/FFmpeg/FFmpeg/commit/5ba2525c7affc29cbd99e6266946b382d3fffe8b
- SVT-AV1 releases：https://gitlab.com/AOMediaCodec/SVT-AV1/-/releases

## CI/CD 與供應鏈

- GitHub Actions dependency caching：https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching
- GitHub Actions cache REST API：https://docs.github.com/en/rest/actions/cache
- GitHub Actions secure use：https://docs.github.com/en/actions/security-for-github-actions/security-guides/security-hardening-for-github-actions
- GitHub Actions artifact attestations：https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations
- actions/cache：https://github.com/actions/cache
- actions/attest：https://github.com/actions/attest
- actions/attest-build-provenance：https://github.com/actions/attest-build-provenance

## AI Agent 與提交規範

- Codex `AGENTS.md`：https://developers.openai.com/codex/guides/agents-md
- OpenAI GPT-5.6 提示最佳實務：https://developers.openai.com/api/docs/guides/model-guidance?model=gpt-5.6#prompting-best-practices
- Codex Agent Skills：https://developers.openai.com/codex/skills
- AGENTS.md 開放格式：https://agents.md/
- Agent Skills 規格：https://agentskills.io/specification
- Claude Code memory：https://code.claude.com/docs/en/memory
- Claude Code skills：https://docs.claude.com/en/docs/claude-code/skills
- GitHub Copilot CLI repository instructions：https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions
- GitHub Copilot CLI skills：https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills
- Google Antigravity CLI migration：https://antigravity.google/docs/gcli-migration
- Google Antigravity Agent Skills：https://antigravity.google/docs/skills
- Google Antigravity changelog：https://antigravity.google/changelog
- 慣例式提交 1.0.0：https://www.conventionalcommits.org/zh-hant/v1.0.0/
