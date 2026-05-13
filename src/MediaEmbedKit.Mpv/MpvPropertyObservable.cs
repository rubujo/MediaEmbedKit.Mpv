using System;
using System.Collections.Generic;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 提供 <see cref="MpvPlayer.Dispose"/> 通知所有泛型 <see cref="MpvPropertyObservable{T}"/> 終止的非泛型介面。
    /// </summary>
    internal interface IMpvPropertyObservableCompletion
    {
        /// <summary>
        /// 對所有目前訂閱者送出 <see cref="IObserver{T}.OnCompleted"/> 並清除清單。
        /// </summary>
        void Complete();
    }

    /// <summary>
    /// 將 libmpv 屬性變更包裝成 <see cref="IObservable{T}"/> 的內部實作。
    /// 同一 <see cref="MpvPlayer"/> 內針對相同 (屬性名稱, 格式) 共用單一 libmpv 觀察識別碼，
    /// 多個 subscriber 透過 refcount 共享。
    /// </summary>
    /// <typeparam name="T">要觀察的屬性值型別。</typeparam>
    internal sealed class MpvPropertyObservable<T> : IObservable<T>, IMpvPropertyObservableCompletion
    {
        /// <summary>
        /// 持有此觀察物件的 <see cref="MpvPlayer"/>。
        /// </summary>
        private readonly MpvPlayer _player;
        /// <summary>
        /// 要觀察的 libmpv 屬性名稱。
        /// </summary>
        private readonly string _propertyName;
        /// <summary>
        /// 要觀察的 libmpv 屬性格式。
        /// </summary>
        private readonly MpvFormat _format;
        /// <summary>
        /// 同步 subscriber 清單與註冊狀態的鎖。
        /// </summary>
        private readonly object _gate;
        /// <summary>
        /// 目前 subscriber 快照；以 copy-on-write 取代鎖內迭代。
        /// </summary>
        private IObserver<T>[] _observers;
        /// <summary>
        /// 已向 libmpv 註冊的觀察識別碼；尚未註冊時為 0。
        /// </summary>
        private ulong _observeId;
        /// <summary>
        /// 表示目前是否已向 libmpv 註冊觀察。
        /// </summary>
        private bool _registered;
        /// <summary>
        /// 目前訂閱的 <see cref="MpvPlayer.PropertyChanged"/> 處理常式。
        /// </summary>
        private EventHandler<MpvPropertyChangedEventArgs>? _propertyChangedHandler;

        /// <summary>
        /// 初始化 <see cref="MpvPropertyObservable{T}"/> 類別的新執行個體。
        /// </summary>
        /// <param name="player">要觀察的播放器。</param>
        /// <param name="propertyName">要觀察的屬性名稱。</param>
        /// <param name="format">要觀察的屬性格式。</param>
        internal MpvPropertyObservable(MpvPlayer player, string propertyName, MpvFormat format)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _propertyName = string.IsNullOrWhiteSpace(propertyName)
                ? throw new ArgumentException("屬性名稱不可為空白。", nameof(propertyName))
                : propertyName;
            _format = format;
            _gate = new object();
            _observers = Array.Empty<IObserver<T>>();
        }

        /// <summary>
        /// 訂閱屬性變更通知。
        /// </summary>
        /// <param name="observer">接收屬性變更值的觀察者。</param>
        /// <returns>取消訂閱用的 <see cref="IDisposable"/>。</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            bool needsRegister = false;
            lock (_gate)
            {
                int existingCount = _observers.Length;
                IObserver<T>[] next = new IObserver<T>[existingCount + 1];
                Array.Copy(_observers, next, existingCount);
                next[existingCount] = observer;
                _observers = next;

                if (!_registered)
                {
                    needsRegister = true;
                    _registered = true;
                }
            }

            if (needsRegister)
            {
                _propertyChangedHandler = OnPropertyChanged;
                _player.PropertyChanged += _propertyChangedHandler;
                try
                {
                    _observeId = _player.ObserveProperty(_propertyName, _format);
                }
                catch
                {
                    lock (_gate)
                    {
                        _registered = false;
                    }

                    if (_propertyChangedHandler != null)
                    {
                        _player.PropertyChanged -= _propertyChangedHandler;
                        _propertyChangedHandler = null;
                    }

                    RemoveObserver(observer);
                    throw;
                }
            }

            return new Subscription(this, observer);
        }

        /// <summary>
        /// 對所有目前訂閱者送出 <see cref="IObserver{T}.OnCompleted"/> 並清除清單。
        /// 在 <see cref="MpvPlayer.Dispose"/> 時呼叫，通知 Rx-style consumer 觀察已終止。
        /// </summary>
        public void Complete()
        {
            IObserver<T>[] snapshot;
            lock (_gate)
            {
                snapshot = _observers;
                _observers = Array.Empty<IObserver<T>>();
                _registered = false;
                _observeId = 0;
                if (_propertyChangedHandler != null)
                {
                    try
                    {
                        _player.PropertyChanged -= _propertyChangedHandler;
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _propertyChangedHandler = null;
                }
            }

            for (int index = 0; index < snapshot.Length; index++)
            {
                try
                {
                    snapshot[index].OnCompleted();
                }
                catch
                {
                    // 訂閱者錯誤不可中斷其他訂閱者。
                }
            }
        }

        /// <summary>
        /// 將指定觀察者從目前清單中移除。
        /// </summary>
        /// <param name="observer">要移除的觀察者。</param>
        private void RemoveObserver(IObserver<T> observer)
        {
            bool needsUnregister = false;
            lock (_gate)
            {
                int existingCount = _observers.Length;
                if (existingCount == 0)
                {
                    return;
                }

                int matchIndex = -1;
                for (int index = 0; index < existingCount; index++)
                {
                    if (ReferenceEquals(_observers[index], observer))
                    {
                        matchIndex = index;
                        break;
                    }
                }

                if (matchIndex < 0)
                {
                    return;
                }

                if (existingCount == 1)
                {
                    _observers = Array.Empty<IObserver<T>>();
                }
                else
                {
                    IObserver<T>[] next = new IObserver<T>[existingCount - 1];
                    if (matchIndex > 0)
                    {
                        Array.Copy(_observers, 0, next, 0, matchIndex);
                    }

                    if (matchIndex < existingCount - 1)
                    {
                        Array.Copy(_observers, matchIndex + 1, next, matchIndex, existingCount - matchIndex - 1);
                    }

                    _observers = next;
                }

                if (_observers.Length == 0 && _registered)
                {
                    needsUnregister = true;
                    _registered = false;
                }
            }

            if (needsUnregister)
            {
                if (_propertyChangedHandler != null)
                {
                    _player.PropertyChanged -= _propertyChangedHandler;
                    _propertyChangedHandler = null;
                }

                try
                {
                    _player.UnobserveProperty(_observeId);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (MpvException)
                {
                }
                finally
                {
                    _observeId = 0;
                }
            }
        }

        /// <summary>
        /// 處理 <see cref="MpvPlayer.PropertyChanged"/> 事件並轉發給訂閱者。
        /// </summary>
        /// <param name="sender">事件來源。</param>
        /// <param name="e">屬性變更事件資料。</param>
        private void OnPropertyChanged(object? sender, MpvPropertyChangedEventArgs e)
        {
            if (!string.Equals(e.Name, _propertyName, StringComparison.Ordinal))
            {
                return;
            }

            if (e.Format != _format)
            {
                return;
            }

            if (!TryConvertValue(e.Value, out T value))
            {
                return;
            }

            IObserver<T>[] snapshot = _observers;
            for (int index = 0; index < snapshot.Length; index++)
            {
                try
                {
                    snapshot[index].OnNext(value);
                }
                catch
                {
                    // 訂閱者錯誤不可中斷其他訂閱者。
                }
            }
        }

        /// <summary>
        /// 將事件值轉換成觀察者型別 <typeparamref name="T"/>。
        /// </summary>
        /// <param name="raw">事件回傳的原始值。</param>
        /// <param name="value">轉換後的值。</param>
        /// <returns>成功轉換時為 <see langword="true"/>。</returns>
        private static bool TryConvertValue(object? raw, out T value)
        {
            if (raw is T direct)
            {
                value = direct;
                return true;
            }

            if (raw == null)
            {
                value = default!;
                return false;
            }

            try
            {
                value = (T)raw;
                return true;
            }
            catch (InvalidCastException)
            {
                value = default!;
                return false;
            }
        }

        /// <summary>
        /// 訂閱句柄；釋放時將觀察者從清單移除並於最後一個觀察者離開時取消 libmpv 註冊。
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            /// <summary>
            /// 來源觀察物件。
            /// </summary>
            private MpvPropertyObservable<T>? _owner;
            /// <summary>
            /// 對應的觀察者。
            /// </summary>
            private IObserver<T>? _observer;

            /// <summary>
            /// 初始化 <see cref="Subscription"/> 類別的新執行個體。
            /// </summary>
            /// <param name="owner">擁有此訂閱的觀察物件。</param>
            /// <param name="observer">對應的觀察者。</param>
            internal Subscription(MpvPropertyObservable<T> owner, IObserver<T> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            /// <summary>
            /// 取消訂閱並從觀察物件移除。
            /// </summary>
            public void Dispose()
            {
                MpvPropertyObservable<T>? owner = _owner;
                IObserver<T>? observer = _observer;
                _owner = null;
                _observer = null;
                if (owner != null && observer != null)
                {
                    owner.RemoveObserver(observer);
                }
            }
        }
    }

    /// <summary>
    /// 將型別對應到 libmpv 屬性格式的協助方法集合。
    /// </summary>
    internal static class MpvPropertyFormatResolver
    {
        /// <summary>
        /// 依泛型型別 <typeparamref name="T"/> 推算對應的 libmpv 屬性格式。
        /// </summary>
        /// <typeparam name="T">要對應的型別。</typeparam>
        /// <returns>對應的 <see cref="MpvFormat"/>。</returns>
        internal static MpvFormat Resolve<T>()
        {
            Type type = typeof(T);
            if (type == typeof(double))
            {
                return MpvFormat.Double;
            }

            if (type == typeof(long))
            {
                return MpvFormat.Int64;
            }

            if (type == typeof(bool))
            {
                return MpvFormat.Flag;
            }

            if (type == typeof(string))
            {
                return MpvFormat.String;
            }

            if (type == typeof(MpvNode))
            {
                return MpvFormat.Node;
            }

            throw new NotSupportedException(
                "WatchProperty 目前支援 double / long / bool / string / MpvNode，未支援 " + type.FullName + "。");
        }
    }
}
