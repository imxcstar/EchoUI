using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32NativeOverlayManager
{
    private readonly Win32NativeInputService _nativeInputService;
    private bool _pendingPositionSync;
    private long _positionSyncFrameVersion;

    public Win32NativeOverlayManager(Win32NativeInputService nativeInputService)
    {
        _nativeInputService = nativeInputService;
    }

    public void SyncPositions(Win32Element root, float viewportWidth, float viewportHeight, IReadOnlyList<Win32Element> floatingElements)
    {
        _nativeInputService.UpdatePositions(root, viewportWidth, viewportHeight, floatingElements);
    }

    public void RequestPositionSyncAfterNextAcceptedFrame()
    {
        _pendingPositionSync = true;
        _positionSyncFrameVersion = long.MaxValue;
    }

    public void MarkAcceptedFrame(long frameVersion)
    {
        if (_pendingPositionSync && _positionSyncFrameVersion == long.MaxValue)
            _positionSyncFrameVersion = frameVersion;
    }

    public void TrySyncAfterPresent(long completedVersion, Win32Element? root, float viewportWidth, float viewportHeight, IReadOnlyList<Win32Element> floatingElements)
    {
        if (!_pendingPositionSync || completedVersion < _positionSyncFrameVersion)
            return;

        _pendingPositionSync = false;
        _positionSyncFrameVersion = 0;
        if (root != null && viewportWidth > 0 && viewportHeight > 0)
            SyncPositions(root, viewportWidth, viewportHeight, floatingElements);
    }
}
