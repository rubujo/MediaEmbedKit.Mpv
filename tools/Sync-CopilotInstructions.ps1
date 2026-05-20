#requires -Version 7.0
<#
.SYNOPSIS
從 AGENTS.md 同步內容到 .github/copilot-instructions.md。

.DESCRIPTION
GitHub Copilot 與 Copilot CLI 不支援 `@import` 機制，因此必須把 `AGENTS.md`
內容內嵌到 `.github/copilot-instructions.md`。本腳本維持兩個檔案同步，
避免人工漂移。

呼叫端：
- 修改 AGENTS.md 後，請執行本腳本。
- CI 可在 PR 檢查中執行本腳本 `-Check` 模式以驗證同步狀態。

.PARAMETER Check
僅驗證 copilot-instructions.md 與 AGENTS.md 是否已同步；不修改檔案。
未同步時以非零代碼結束。
#>
[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$agentsPath = Join-Path $repoRoot 'AGENTS.md'
$copilotPath = Join-Path $repoRoot '.github/copilot-instructions.md'

if (-not (Test-Path -LiteralPath $agentsPath)) {
    throw "AGENTS.md 不存在：$agentsPath"
}

$agentsContent = Get-Content -LiteralPath $agentsPath -Raw

$header = @"
<!--
此檔案由 tools/Sync-CopilotInstructions.ps1 從 AGENTS.md 自動同步。
請勿手動編輯；要更動規則請改 AGENTS.md，再執行：
  pwsh tools/Sync-CopilotInstructions.ps1
GitHub Copilot 不支援 @import 機制，因此必須內嵌內容。
-->

# GitHub Copilot 指示

本文件是 GitHub Copilot 與 Copilot CLI 看到的系統指令。內容必須與 ``AGENTS.md`` 保持一致。

---

"@

# 使用 CRLF 與 UTF-8（無 BOM）寫入，與其他 Markdown 一致。
$expected = ($header + $agentsContent).Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")
$expectedForComparison = $expected.Replace("`r`n", "`n")
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

if ($Check) {
    if (-not (Test-Path -LiteralPath $copilotPath)) {
        Write-Error 'copilot-instructions.md 不存在；請執行 Sync-CopilotInstructions.ps1。'
        exit 1
    }

    $current = [System.IO.File]::ReadAllText($copilotPath, $utf8NoBom).Replace("`r`n", "`n").Replace("`r", "`n")
    if ($current -ne $expectedForComparison) {
        Write-Error '.github/copilot-instructions.md 與 AGENTS.md 不同步；請執行 Sync-CopilotInstructions.ps1。'
        exit 1
    }

    Write-Host 'copilot-instructions.md 與 AGENTS.md 已同步。'
    exit 0
}

[System.IO.File]::WriteAllText($copilotPath, $expected, $utf8NoBom)
Write-Host '已將 AGENTS.md 同步到 .github/copilot-instructions.md。'
