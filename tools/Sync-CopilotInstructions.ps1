#requires -Version 7.0
<#
.SYNOPSIS
從 AGENTS.md 產生 GitHub Copilot 相容鏡像。

.DESCRIPTION
`.github/copilot-instructions.md` 保留給 GitHub Copilot 的儲存庫自訂指示
介面與舊有工作流程。GitHub Copilot CLI 可直接讀取 `AGENTS.md`；本腳本僅
維持相容鏡像與主要入口一致，避免人工漂移。

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
此檔案由 tools/Sync-CopilotInstructions.ps1 從 AGENTS.md 產生相容鏡像。
請勿手動編輯；要更動規則請改 AGENTS.md，再執行：
  pwsh tools/Sync-CopilotInstructions.ps1
GitHub Copilot CLI 以 AGENTS.md 為主要入口；此檔保留給 GitHub Copilot 的儲存庫自訂指示介面。
-->

# GitHub Copilot 指示

本文件是 GitHub Copilot 的相容鏡像。主要規則來源為 ``AGENTS.md``。

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
        Write-Error '.github/copilot-instructions.md 與 AGENTS.md 相容鏡像不同步；請執行 Sync-CopilotInstructions.ps1。'
        exit 1
    }

    Write-Host 'copilot-instructions.md 與 AGENTS.md 相容鏡像已同步。'
    exit 0
}

[System.IO.File]::WriteAllText($copilotPath, $expected, $utf8NoBom)
Write-Host '已從 AGENTS.md 產生 .github/copilot-instructions.md 相容鏡像。'
