using System;
using System.Text;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 表示 mpv 內建 ytdl hook 執行 yt-dlp JSON 子程序後留下的結果。
    /// </summary>
    public sealed class MpvYtdlJsonSubprocessResult
    {
        /// <summary>
        /// 初始化 <see cref="MpvYtdlJsonSubprocessResult"/> 類別的新執行個體。
        /// </summary>
        /// <param name="status">ytdl 子程序結束狀態碼。</param>
        /// <param name="standardOutput">ytdl 子程序標準輸出內容。</param>
        /// <param name="standardError">ytdl 子程序標準錯誤內容。</param>
        /// <param name="errorString">mpv 子程序命令回報的錯誤文字。</param>
        /// <param name="killedByMpv">子程序是否由 mpv 主動終止。</param>
        /// <param name="rawNode">mpv 原始節點結果。</param>
        private MpvYtdlJsonSubprocessResult(
            long status,
            string standardOutput,
            string standardError,
            string errorString,
            bool killedByMpv,
            MpvNode rawNode)
        {
            Status = status;
            StandardOutput = standardOutput;
            StandardError = standardError;
            ErrorString = errorString;
            KilledByMpv = killedByMpv;
            RawNode = rawNode;
        }

        /// <summary>
        /// 取得 ytdl 子程序結束狀態碼。
        /// </summary>
        /// <value>ytdl 子程序結束狀態碼。</value>
        public long Status { get; private set; }

        /// <summary>
        /// 取得 ytdl 子程序標準輸出內容。
        /// </summary>
        /// <value>ytdl 子程序標準輸出內容。</value>
        public string StandardOutput { get; private set; }

        /// <summary>
        /// 取得 ytdl 子程序標準錯誤內容。
        /// </summary>
        /// <value>ytdl 子程序標準錯誤內容。</value>
        public string StandardError { get; private set; }

        /// <summary>
        /// 取得 mpv 子程序命令回報的錯誤文字。
        /// </summary>
        /// <value>錯誤文字；沒有錯誤文字時為空字串。</value>
        public string ErrorString { get; private set; }

        /// <summary>
        /// 取得子程序是否由 mpv 主動終止。
        /// </summary>
        /// <value>子程序由 mpv 主動終止時為 <see langword="true"/>。</value>
        public bool KilledByMpv { get; private set; }

        /// <summary>
        /// 取得 ytdl 子程序是否以成功狀態完成。
        /// </summary>
        /// <value>狀態碼為 0 且沒有錯誤文字時為 <see langword="true"/>。</value>
        public bool Succeeded
        {
            get { return Status == 0 && string.IsNullOrEmpty(ErrorString); }
        }

        /// <summary>
        /// 取得 mpv 原始節點結果。
        /// </summary>
        /// <value>mpv 原始節點結果。</value>
        public MpvNode RawNode { get; private set; }

        /// <summary>
        /// 從 mpv 節點建立 ytdl JSON 子程序結果。
        /// </summary>
        /// <param name="node">mpv 的 <c>user-data/mpv/ytdl/json-subprocess-result</c> 節點。</param>
        /// <returns>轉換後的 ytdl JSON 子程序結果。</returns>
        internal static MpvYtdlJsonSubprocessResult FromNode(MpvNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            long status = node.GetValueOrNone("status").AsInt64();
            string standardOutput = ReadText(node.GetValueOrNone("stdout"));
            string standardError = ReadText(node.GetValueOrNone("stderr"));
            string errorString = node.GetValueOrNone("error_string").AsString() ?? string.Empty;
            bool killedByMpv = node.GetValueOrNone("killed_by_us").AsBoolean();
            return new MpvYtdlJsonSubprocessResult(status, standardOutput, standardError, errorString, killedByMpv, node);
        }

        /// <summary>
        /// 將節點文字或位元組陣列轉成 UTF-8 文字。
        /// </summary>
        /// <param name="node">要讀取的節點。</param>
        /// <returns>節點文字內容；沒有內容時為空字串。</returns>
        private static string ReadText(MpvNode node)
        {
            string? text = node.AsString();
            if (text != null)
            {
                return text;
            }

            byte[] bytes = node.AsByteArray();
            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }
    }
}
