using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 提供 libmpv 錯誤碼的共用處理方法。
    /// </summary>
    public static class MpvError
    {
        /// <summary>
        /// 在 libmpv 傳回錯誤碼時擲回 <see cref="MpvException"/>。
        /// </summary>
        /// <param name="errorCode">libmpv API 傳回的整數錯誤碼。</param>
        public static void ThrowIfError(int errorCode)
        {
            if (errorCode < 0)
            {
                throw new MpvException(errorCode);
            }
        }

        /// <summary>
        /// 取得 libmpv 錯誤碼對應的文字訊息。
        /// </summary>
        /// <param name="errorCode">要查詢的 libmpv 錯誤碼。</param>
        /// <returns>libmpv 提供的錯誤訊息文字。</returns>
        public static string GetMessage(int errorCode)
        {
            return MpvNative.GetErrorString(errorCode);
        }
    }
}
