using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native
{
    /// <summary>
    /// 管理 libmpv 用戶端控制代碼生命週期的安全控制代碼。
    /// </summary>
    internal sealed class SafeMpvHandle : SafeHandle
    {
        /// <summary>
        /// 初始化 <see cref="SafeMpvHandle"/> 類別的新執行個體。
        /// </summary>
        public SafeMpvHandle()
            : base(IntPtr.Zero, true)
        {
        }

        /// <summary>
        /// 使用指定原生控制代碼初始化 <see cref="SafeMpvHandle"/> 類別的新執行個體。
        /// </summary>
        /// <param name="handle">libmpv 用戶端原生控制代碼。</param>
        public SafeMpvHandle(IntPtr handle)
            : base(IntPtr.Zero, true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// 取得控制代碼是否無效。
        /// </summary>
        /// <value>控制代碼為零時為 <see langword="true"/>。</value>
        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        /// <summary>
        /// 釋放 libmpv 用戶端控制代碼。
        /// </summary>
        /// <returns>控制代碼釋放完成時為 <see langword="true"/>。</returns>
        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                MpvNative.mpv_terminate_destroy(handle);
                handle = IntPtr.Zero;
            }

            return true;
        }
    }
}
