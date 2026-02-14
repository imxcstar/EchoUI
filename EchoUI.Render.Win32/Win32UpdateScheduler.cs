using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 更新调度器，通过 PostMessage 将更新调度到主窗口消息循环线程。
    /// </summary>
    internal class Win32UpdateScheduler : IUpdateScheduler
    {
        private readonly nint _hwnd;
        private readonly List<Func<Task>> _pendingActions = [];
        private readonly object _lock = new();

        public Win32UpdateScheduler(nint hwnd)
        {
            _hwnd = hwnd;
        }

        public void Schedule(Func<Task> updateAction)
        {
            lock (_lock)
            {
                _pendingActions.Add(updateAction);
            }
            // 发送自定义消息到窗口消息循环，触发更新处理
            NativeInterop.PostMessage(_hwnd, NativeInterop.WM_ECHOUI_UPDATE, 0, 0);
        }

        /// <summary>
        /// 在消息循环线程中处理所有待执行的更新操作
        /// </summary>
        public async Task ProcessPendingUpdates()
        {
            List<Func<Task>> actions;
            lock (_lock)
            {
                actions = [.. _pendingActions];
                _pendingActions.Clear();
            }

            foreach (var action in actions)
            {
                await action();
            }
        }
    }
}
