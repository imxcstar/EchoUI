namespace EchoUI.Render.Win32
{
    /// <summary>
    /// 基于 Win32 消息循环的 SynchronizationContext。
    /// 将 async/await 的延续操作调度到 UI 线程执行。
    /// </summary>
    public class Win32SynchronizationContext : SynchronizationContext
    {
        /// <summary>
        /// 待执行的回调队列
        /// </summary>
        private static readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
        private static readonly object _lock = new();

        /// <summary>
        /// 自定义消息 ID，用于通知消息循环处理回调
        /// </summary>
        public const uint WM_SYNC_CONTEXT = NativeInterop.WM_APP + 2;

        private static nint _hwnd;

        public static void SetWindow(nint hwnd)
        {
            _hwnd = hwnd;
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_lock)
            {
                _queue.Enqueue((d, state));
            }

            if (_hwnd != 0)
            {
                NativeInterop.PostMessage(_hwnd, WM_SYNC_CONTEXT, 0, 0);
            }
            else
            {
                // 窗口还没创建，直接在当前线程执行
                d(state);
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            // 同步执行
            d(state);
        }

        /// <summary>
        /// 在消息循环中调用，处理所有待执行的回调
        /// </summary>
        public static void ProcessQueue()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (_lock)
                {
                    if (_queue.Count == 0) break;
                    item = _queue.Dequeue();
                }
                item.Callback(item.State);
            }
        }
    }
}
