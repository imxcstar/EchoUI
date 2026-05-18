using System;
using System.Collections.Generic;
using System.Diagnostics;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 动画引擎：驱动 ContainerProps.Transitions 声明的属性过渡动画。
    /// 通过 WM_TIMER 驱动逐帧插值，每帧更新 Win32Element 属性并触发重绘。
    /// 支持插值类型：Color?, float, Dimension?, Spacing?, Transform, TransformOrigin
    /// </summary>
    internal class Win32AnimationManager
    {
        private readonly Win32Window _window;
        private readonly Win32Renderer _renderer;
        private const uint TimerIntervalMs = 16; // ~60 FPS

        private readonly List<ActiveAnimation> _animations = new();
        private nint _timerId;
        private bool _timerRunning;
        private bool _timerResolutionRaised;
        private int _nextTimerId = 100;
        private long _lastTickTimestamp;

        public Win32AnimationManager(Win32Window window, Win32Renderer renderer)
        {
            _window = window;
            _renderer = renderer;
        }

        /// <summary>
        /// 启动一个属性动画
        /// </summary>
        public void StartAnimation(Win32Element element, string propertyName,
            object? fromValue, object? toValue, Transition transition)
        {
            // 如果起止值相同，不启动动画
            if (ValuesEqual(fromValue, toValue))
                return;

            // 如果起止值任一为 null，直接跳转到目标值，不动画
            if (fromValue == null || toValue == null)
                return;

            // 停止同元素同属性的已有动画
            StopAnimation(element, propertyName);
            SetElementProperty(element, propertyName, fromValue);

            _animations.Add(new ActiveAnimation
            {
                Element = element,
                PropertyName = propertyName,
                FromValue = fromValue,
                ToValue = toValue,
                DurationMs = transition.DurationMs,
                Easing = transition.Easing,
                ElapsedMs = 0
            });

            EnsureTimerRunning();
        }

        /// <summary>
        /// 停止指定元素指定属性的动画
        /// </summary>
        public void StopAnimation(Win32Element? element, string? propertyName = null)
        {
            if (element == null)
            {
                _animations.Clear();
                StopTimer();
                return;
            }

            _animations.RemoveAll(a =>
                a.Element == element &&
                (propertyName == null || a.PropertyName == propertyName));

            if (_animations.Count == 0)
                StopTimer();
        }

        /// <summary>
        /// 停止指定元素及其子树的所有动画（用于元素卸载时）
        /// </summary>
        public void StopAnimationsForElement(Win32Element element)
        {
            var toRemove = new List<ActiveAnimation>();
            CollectAnimationsForSubtree(element, toRemove);
            foreach (var anim in toRemove)
                _animations.Remove(anim);

            if (_animations.Count == 0)
                StopTimer();
        }

        private void CollectAnimationsForSubtree(Win32Element element, List<ActiveAnimation> result)
        {
            foreach (var anim in _animations)
            {
                if (anim.Element == element)
                    result.Add(anim);
            }
            foreach (var child in element.Children)
                CollectAnimationsForSubtree(child, result);
        }

        /// <summary>
        /// WM_TIMER 回调：推进所有动画
        /// </summary>
        public void OnTimerTick()
        {
            var now = Stopwatch.GetTimestamp();
            var deltaMs = _lastTickTimestamp == 0
                ? TimerIntervalMs
                : (now - _lastTickTimestamp) * 1000.0 / Stopwatch.Frequency;
            _lastTickTimestamp = now;

            UpdateAnimations(deltaMs);
        }

        /// <summary>
        /// 重置计时器基准时间（定时器重启时调用）
        /// </summary>
        public void ResetTickTime()
        {
            _lastTickTimestamp = 0;
        }

        // --- 内部实现 ---

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

        private void UpdateAnimations(double deltaMs)
        {
            if (_animations.Count == 0)
            {
                StopTimer();
                return;
            }

            bool anyUpdated = false;
            bool needsRelayout = false;
            bool needsFullRepaint = false;
            HashSet<Win32Element>? dirtyElements = null;

            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                var anim = _animations[i];
                anim.ElapsedMs += deltaMs;

                var isLayoutProperty = IsLayoutProperty(anim.PropertyName);
                double t = anim.ElapsedMs / anim.DurationMs;
                if (t >= 1.0)
                {
                    SetElementProperty(anim.Element, anim.PropertyName, anim.ToValue);
                    _animations.RemoveAt(i);
                    anyUpdated = true;
                }
                else
                {
                    var easedT = ApplyEasing((float)t, anim.Easing);
                    var current = Interpolate(anim.FromValue, anim.ToValue, easedT);
                    SetElementProperty(anim.Element, anim.PropertyName, current);
                    anyUpdated = true;
                }

                if (isLayoutProperty)
                {
                    needsRelayout = true;
                }
                else if (NeedsFullRepaint(anim.PropertyName))
                {
                    needsFullRepaint = true;
                }
                else
                {
                    dirtyElements ??= [];
                    dirtyElements.Add(anim.Element);
                }
            }

            if (!anyUpdated)
                return;

            if (needsRelayout)
            {
                _renderer.RequestAnimationRelayout();
            }
            else if (needsFullRepaint)
            {
                _renderer.RequestRepaint();
            }
            else if (dirtyElements != null)
            {
                foreach (var element in dirtyElements)
                    _renderer.RequestRepaint(element);
            }

            if (_animations.Count == 0)
                StopTimer();
        }

        // --- 插值函数 ---

        private static object? Interpolate(object? from, object? to, float t)
        {
            if (from == null || to == null) return t >= 1f ? to : from;
            if (from.GetType() != to.GetType()) return t >= 1f ? to : from;

            return from switch
            {
                Color c => LerpColor(c, (Color)to, t),
                BoxShadow shadow => LerpBoxShadow(shadow, (BoxShadow)to, t),
                float f => f + ((float)to - f) * t,
                int iv => iv + (int)(((int)to - iv) * t),
                Dimension d => LerpDimension(d, (Dimension)to, t),
                Spacing spacing => LerpSpacing(spacing, (Spacing)to, t),
                Transform transform => LerpTransform(transform, (Transform)to, t),
                TransformOrigin origin => LerpTransformOrigin(origin, (TransformOrigin)to, t),
                _ => t >= 1f ? to : from
            };
        }

        private static Color LerpColor(Color from, Color to, float t)
        {
            return new Color(
                (byte)Math.Clamp(Math.Round(from.R + (to.R - from.R) * t), 0, 255),
                (byte)Math.Clamp(Math.Round(from.G + (to.G - from.G) * t), 0, 255),
                (byte)Math.Clamp(Math.Round(from.B + (to.B - from.B) * t), 0, 255),
                (byte)Math.Clamp(Math.Round(from.A + (to.A - from.A) * t), 0, 255)
            );
        }

        private static BoxShadow LerpBoxShadow(BoxShadow from, BoxShadow to, float t)
        {
            return new BoxShadow(
                LerpColor(from.Color, to.Color, t),
                from.OffsetY + (to.OffsetY - from.OffsetY) * t,
                from.Blur + (to.Blur - from.Blur) * t);
        }

        private static Dimension LerpDimension(Dimension from, Dimension to, float t)
        {
            // 保持目标值的单位，插值数值
            return new Dimension(from.Value + (to.Value - from.Value) * t, to.Unit);
        }

        private static Spacing LerpSpacing(Spacing from, Spacing to, float t)
        {
            return new Spacing(
                LerpDimension(from.Left, to.Left, t),
                LerpDimension(from.Top, to.Top, t),
                LerpDimension(from.Right, to.Right, t),
                LerpDimension(from.Bottom, to.Bottom, t)
            );
        }

        private static TransformOrigin LerpTransformOrigin(TransformOrigin from, TransformOrigin to, float t)
        {
            return new TransformOrigin(
                LerpFloat(from.X, to.X, t),
                LerpFloat(from.Y, to.Y, t));
        }

        private static Transform LerpTransform(Transform from, Transform to, float t)
        {
            var fromFunctions = from.Functions ?? [];
            var toFunctions = to.Functions ?? [];

            if (fromFunctions.Length == 0 && toFunctions.Length > 0)
                fromFunctions = CreateIdentityFunctionsLike(toFunctions);
            else if (toFunctions.Length == 0 && fromFunctions.Length > 0)
                toFunctions = CreateIdentityFunctionsLike(fromFunctions);

            if (fromFunctions.Length != toFunctions.Length)
                return t >= 1f ? to : from;

            var result = new TransformFunction[fromFunctions.Length];
            for (int i = 0; i < fromFunctions.Length; i++)
            {
                var interpolated = LerpTransformFunction(fromFunctions[i], toFunctions[i], t);
                if (interpolated == null)
                    return t >= 1f ? to : from;

                result[i] = interpolated;
            }

            return new Transform(result);
        }

        private static TransformFunction[] CreateIdentityFunctionsLike(TransformFunction[] functions)
        {
            var result = new TransformFunction[functions.Length];
            for (int i = 0; i < functions.Length; i++)
            {
                result[i] = functions[i] switch
                {
                    TranslateTransform => new TranslateTransform(0, 0),
                    ScaleTransform => new ScaleTransform(1, 1),
                    RotateTransform => new RotateTransform(0),
                    SkewTransform => new SkewTransform(0, 0),
                    _ => functions[i]
                };
            }
            return result;
        }

        private static TransformFunction? LerpTransformFunction(TransformFunction from, TransformFunction to, float t)
        {
            return (from, to) switch
            {
                (TranslateTransform a, TranslateTransform b) => new TranslateTransform(LerpFloat(a.X, b.X, t), LerpFloat(a.Y, b.Y, t)),
                (ScaleTransform a, ScaleTransform b) => new ScaleTransform(LerpFloat(a.X, b.X, t), LerpFloat(a.Y, b.Y, t)),
                (RotateTransform a, RotateTransform b) => new RotateTransform(LerpFloat(a.AngleDeg, b.AngleDeg, t)),
                (SkewTransform a, SkewTransform b) => new SkewTransform(LerpFloat(a.XDeg, b.XDeg, t), LerpFloat(a.YDeg, b.YDeg, t)),
                _ => null
            };
        }

        private static float LerpFloat(float from, float to, float t) => from + (to - from) * t;

        private static float ApplyEasing(float t, Easing easing)
        {
            return easing switch
            {
                Easing.Linear => t,
                Easing.Ease => t < 0.5f
                    ? 4f * t * t * t
                    : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f,
                Easing.EaseIn => t * t * t,
                Easing.EaseOut => 1f - (float)Math.Pow(1f - t, 3),
                Easing.EaseInOut => t < 0.5f
                    ? 4f * t * t * t
                    : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f,
                _ => t
            };
        }

        // --- 属性读写 ---

        internal static object? GetPropertyValue(Win32Element element, string propName)
        {
            return propName switch
            {
                nameof(Win32Element.BackgroundColor) => element.BackgroundColor,
                nameof(Win32Element.BorderColor) => element.BorderColor,
                nameof(Win32Element.Shadow) => element.Shadow,
                nameof(Win32Element.BorderWidth) => element.BorderWidth,
                nameof(Win32Element.BorderRadius) => element.BorderRadius,
                nameof(Win32Element.Margin) => element.Margin,
                nameof(Win32Element.Padding) => element.Padding,
                nameof(Win32Element.Width) => element.Width,
                nameof(Win32Element.Height) => element.Height,
                nameof(Win32Element.MinWidth) => element.MinWidth,
                nameof(Win32Element.MinHeight) => element.MinHeight,
                nameof(Win32Element.MaxWidth) => element.MaxWidth,
                nameof(Win32Element.MaxHeight) => element.MaxHeight,
                nameof(Win32Element.Gap) => element.Gap,
                nameof(Win32Element.Transform) => element.Transform,
                nameof(Win32Element.TransformOrigin) => element.TransformOrigin,
                _ => null
            };
        }

        private static void SetElementProperty(Win32Element element, string propName, object? value)
        {
            switch (propName)
            {
                case nameof(Win32Element.BackgroundColor):
                    element.BackgroundColor = (Color?)value;
                    break;
                case nameof(Win32Element.BorderColor):
                    element.BorderColor = (Color?)value;
                    break;
                case nameof(Win32Element.Shadow):
                    element.Shadow = value is BoxShadow shadow ? shadow : BoxShadow.None;
                    break;
                case nameof(Win32Element.BorderWidth):
                    element.BorderWidth = value is float bw ? bw : 0;
                    break;
                case nameof(Win32Element.BorderRadius):
                    element.BorderRadius = value is float br ? br : 0;
                    break;
                case nameof(Win32Element.Margin):
                    element.Margin = (Spacing?)value;
                    break;
                case nameof(Win32Element.Padding):
                    element.Padding = (Spacing?)value;
                    break;
                case nameof(Win32Element.Width):
                    element.Width = (Dimension?)value;
                    break;
                case nameof(Win32Element.Height):
                    element.Height = (Dimension?)value;
                    break;
                case nameof(Win32Element.MinWidth):
                    element.MinWidth = (Dimension?)value;
                    break;
                case nameof(Win32Element.MinHeight):
                    element.MinHeight = (Dimension?)value;
                    break;
                case nameof(Win32Element.MaxWidth):
                    element.MaxWidth = (Dimension?)value;
                    break;
                case nameof(Win32Element.MaxHeight):
                    element.MaxHeight = (Dimension?)value;
                    break;
                case nameof(Win32Element.Gap):
                    element.Gap = value is float gap ? gap : 0;
                    break;
                case nameof(Win32Element.Transform):
                    element.Transform = value is Transform transform ? transform : new Transform();
                    break;
                case nameof(Win32Element.TransformOrigin):
                    element.TransformOrigin = value is TransformOrigin origin ? origin : TransformOrigin.Center;
                    break;
            }
        }

        // --- 辅助 ---

        private static bool IsLayoutProperty(string propName)
        {
            return propName switch
            {
                nameof(Win32Element.BorderWidth) => true,
                nameof(Win32Element.Margin) => true,
                nameof(Win32Element.Padding) => true,
                nameof(Win32Element.Width) => true,
                nameof(Win32Element.Height) => true,
                nameof(Win32Element.MinWidth) => true,
                nameof(Win32Element.MinHeight) => true,
                nameof(Win32Element.MaxWidth) => true,
                nameof(Win32Element.MaxHeight) => true,
                nameof(Win32Element.Gap) => true,
                _ => false
            };
        }

        private static bool NeedsFullRepaint(string propName)
        {
            return propName switch
            {
                nameof(Win32Element.Transform) => true,
                nameof(Win32Element.TransformOrigin) => true,
                _ => false
            };
        }

        private static bool ValuesEqual(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;

            if (a is Transform ta && b is Transform tb)
                return TransformsEqual(ta, tb);

            return a.Equals(b);
        }

        private static bool TransformsEqual(Transform a, Transform b)
        {
            var af = a.Functions ?? [];
            var bf = b.Functions ?? [];
            if (af.Length != bf.Length) return false;

            for (int i = 0; i < af.Length; i++)
            {
                if (!Equals(af[i], bf[i])) return false;
            }

            return true;
        }

        /// <summary>
        /// 动画条目
        /// </summary>
        private class ActiveAnimation
        {
            public Win32Element Element { get; set; } = null!;
            public string PropertyName { get; set; } = "";
            public object? FromValue { get; set; }
            public object? ToValue { get; set; }
            public double DurationMs { get; set; }
            public Easing Easing { get; set; }
            public double ElapsedMs { get; set; }
        }
    }
}
