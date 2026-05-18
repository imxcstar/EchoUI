using System.Diagnostics;
using EchoUI.Core;

namespace EchoUI.Render.Win32;

/// <summary>
/// Win32 动画驱动器：负责用 WM_TIMER 驱动 Core 动画引擎，并把动画结果映射为 Win32 重绘/重排请求。
/// </summary>
internal sealed class Win32AnimationManager
{
    private readonly Win32Window _window;
    private readonly Win32Renderer _renderer;
    private readonly AnimationEngine<Win32Element> _engine;
    private readonly Dictionary<Win32Element, ScrollAnimationState> _scrollAnimations = [];
    private const uint TimerIntervalMs = 30;
    private const double ScrollStopThreshold = 6.6;
    private const double ScrollSmoothingMs = 55.0;

    private nint _timerId;
    private bool _timerRunning;
    private bool _timerResolutionRaised;
    private int _nextTimerId = 100;
    private long _lastTickTimestamp;

    public Win32AnimationManager(Win32Window window, Win32Renderer renderer)
    {
        _window = window;
        _renderer = renderer;
        _engine = new AnimationEngine<Win32Element>(new Win32AnimationTargetAdapter());
    }

    public object? GetPropertyValue(Win32Element element, string propertyName)
    {
        return _engine.GetPropertyValue(element, propertyName);
    }

    public void StartAnimation(Win32Element element, string propertyName, object? fromValue, object? toValue, Transition transition)
    {
        _engine.StartAnimation(element, propertyName, fromValue, toValue, transition);
        if (_engine.HasAnimations)
            EnsureTimerRunning();
    }

    public void StopAnimation(Win32Element? element, string? propertyName = null)
    {
        _engine.StopAnimation(element, propertyName);
        if (!HasActiveWork)
            StopTimer();
    }

    public void StopAnimationsForElement(Win32Element element)
    {
        _engine.StopAnimationsForTarget(element);
        _scrollAnimations.Remove(element);
        if (!HasActiveWork)
            StopTimer();
    }

    public void StartScrollAnimation(Win32Element element, float previousScrollX, float previousScrollY)
    {
        var requestedX = element.ScrollOffsetX;
        var requestedY = element.ScrollOffsetY;
        var deltaX = requestedX - previousScrollX;
        var deltaY = requestedY - previousScrollY;

        if (_scrollAnimations.TryGetValue(element, out var state))
        {
            _scrollAnimations[element] = state with
            {
                TargetX = ClampScrollX(element, state.TargetX + deltaX),
                TargetY = ClampScrollY(element, state.TargetY + deltaY)
            };
        }
        else
        {
            _scrollAnimations[element] = new ScrollAnimationState(
                ClampScrollX(element, requestedX),
                ClampScrollY(element, requestedY));
        }

        element.ScrollOffsetX = previousScrollX;
        element.ScrollOffsetY = previousScrollY;
        _renderer.ApplyScrollReposition(element);
        EnsureTimerRunning();
    }

    public void OnTimerTick()
    {
        var now = Stopwatch.GetTimestamp();
        var deltaMs = _lastTickTimestamp == 0
            ? TimerIntervalMs
            : (now - _lastTickTimestamp) * 1000.0 / Stopwatch.Frequency;
        _lastTickTimestamp = now;

        var result = _engine.Tick(deltaMs);
        ApplyUpdateResult(result);
        TickScrollAnimations(deltaMs);

        if (!HasActiveWork)
            StopTimer();
    }

    public void ResetTickTime()
    {
        _lastTickTimestamp = 0;
    }

    private bool HasActiveWork => _engine.HasAnimations || _scrollAnimations.Count > 0;

    private void ApplyUpdateResult(AnimationUpdateResult<Win32Element> result)
    {
        if (!result.HasUpdates)
            return;

        if (result.NeedsRelayout)
        {
            _renderer.RequestAnimationRelayout();
        }
        else if (result.NeedsFullRepaint)
        {
            _renderer.RequestRepaint();
        }
        else
        {
            foreach (var element in result.DirtyTargets)
                _renderer.RequestRepaint(element);
        }
    }

    private void TickScrollAnimations(double deltaMs)
    {
        if (_scrollAnimations.Count == 0)
            return;

        var factor = 1.0 - Math.Exp(-Math.Max(1.0, deltaMs) / ScrollSmoothingMs);
        foreach (var (element, state) in _scrollAnimations.ToArray())
        {
            var targetX = ClampScrollX(element, state.TargetX);
            var targetY = ClampScrollY(element, state.TargetY);
            var nextX = Lerp(element.ScrollOffsetX, targetX, factor);
            var nextY = Lerp(element.ScrollOffsetY, targetY, factor);

            if (Math.Abs(targetX - nextX) <= ScrollStopThreshold)
                nextX = targetX;
            if (Math.Abs(targetY - nextY) <= ScrollStopThreshold)
                nextY = targetY;

            element.ScrollOffsetX = nextX;
            element.ScrollOffsetY = nextY;
            _renderer.ApplyScrollReposition(element);

            if (nextX.Equals(targetX) && nextY.Equals(targetY))
                _scrollAnimations.Remove(element);
            else
                _scrollAnimations[element] = state with { TargetX = targetX, TargetY = targetY };
        }
    }

    private static float Lerp(float from, float to, double factor)
    {
        return from + (to - from) * (float)Math.Clamp(factor, 0.0, 1.0);
    }

    private static float ClampScrollX(Win32Element element, float value)
    {
        var maxScroll = Math.Max(0, element.CachedContentWidth - element.LayoutWidth);
        return Math.Clamp(value, 0, maxScroll);
    }

    private static float ClampScrollY(Win32Element element, float value)
    {
        var maxScroll = Math.Max(0, element.CachedContentHeight - element.LayoutHeight);
        return Math.Clamp(value, 0, maxScroll);
    }

    private readonly record struct ScrollAnimationState(float TargetX, float TargetY);

    private void EnsureTimerRunning()
    {
        if (_timerRunning || _window.Hwnd == 0)
            return;

        if (!_timerResolutionRaised)
        {
            NativeInterop.timeBeginPeriod(1);
            _timerResolutionRaised = true;
        }

        _timerId = (nint)_nextTimerId++;
        NativeInterop.SetTimer(_window.Hwnd, _timerId, TimerIntervalMs, 0);
        _timerRunning = true;
        ResetTickTime();
    }

    private void StopTimer()
    {
        if (_timerRunning)
        {
            if (_window.Hwnd != 0 && _timerId != 0)
                NativeInterop.KillTimer(_window.Hwnd, _timerId);

            _timerRunning = false;
            _timerId = 0;
            _lastTickTimestamp = 0;
        }

        if (_timerResolutionRaised)
        {
            NativeInterop.timeEndPeriod(1);
            _timerResolutionRaised = false;
        }
    }
}
