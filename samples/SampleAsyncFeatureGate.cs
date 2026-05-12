using System.Threading;

namespace MediaEmbedKit.Mpv.Samples
{
    /// <summary>
    /// 提供範例功能按鈕共用的非同步重入保護。
    /// </summary>
    internal sealed class SampleAsyncFeatureGate
    {
        /// <summary>
        /// 表示目前是否已有非同步功能正在執行。
        /// </summary>
        private int _isRunning;

        /// <summary>
        /// 取得目前是否已有非同步功能正在執行。
        /// </summary>
        /// <value>已有非同步功能執行中時為 <see langword="true"/>。</value>
        public bool IsRunning
        {
            get { return Volatile.Read(ref _isRunning) != 0; }
        }

        /// <summary>
        /// 嘗試進入非同步功能執行區段。
        /// </summary>
        /// <returns>成功取得執行權時為 <see langword="true"/>。</returns>
        public bool TryEnter()
        {
            return Interlocked.Exchange(ref _isRunning, 1) == 0;
        }

        /// <summary>
        /// 離開非同步功能執行區段。
        /// </summary>
        public void Exit()
        {
            Volatile.Write(ref _isRunning, 0);
        }
    }
}
