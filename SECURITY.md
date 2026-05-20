# 安全政策

本專案不提供正式安全支援服務。

## 支援範圍

- 不提供 GitHub Issues 支援、私訊、email、bug bounty、SLA、漏洞回報服務或回應時程。
- 不代收、轉送或協調第三方元件的安全問題。
- 不保證執行階段輔助工具下載的第三方二進位檔適合特定商用、法規或安全要求。

## 使用者責任

使用者若散發或在生產環境使用執行階段輔助工具，必須自行審查第三方二進位檔、授權義務、SHA-256 釘選、來源可信度與供應鏈風險。本專案提供相關風險說明與輔助工具選項，但不提供安全事件處理服務。

供應鏈威脅模型、GitHub digest 限制、Sigstore provenance、`RequirePinnedSha256` 與第三方元件責任邊界，請見 [`docs/SECURITY_MODEL.md`](docs/SECURITY_MODEL.md)。
