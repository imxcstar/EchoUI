namespace EchoUI.Core;

public enum AnimationPropertyImpact
{
    None,
    ElementRepaint,
    FullRepaint,
    Relayout
}

public readonly record struct AnimationUpdateResult<TTarget>(
    bool HasUpdates,
    bool NeedsRelayout,
    bool NeedsFullRepaint,
    IReadOnlyCollection<TTarget> DirtyTargets);

public interface IAnimationTargetAdapter<TTarget> where TTarget : class
{
    object? GetPropertyValue(TTarget target, string propertyName);
    void SetPropertyValue(TTarget target, string propertyName, object? value);
    AnimationPropertyImpact GetPropertyImpact(string propertyName);
    IEnumerable<TTarget> GetDescendants(TTarget target);
}

public sealed class AnimationEngine<TTarget> where TTarget : class
{
    private readonly IAnimationTargetAdapter<TTarget> _adapter;
    private readonly List<ActiveAnimation> _animations = [];
    private readonly HashSet<TTarget> _dirtyTargets = new(ReferenceEqualityComparer<TTarget>.Instance);

    public AnimationEngine(IAnimationTargetAdapter<TTarget> adapter)
    {
        _adapter = adapter;
    }

    public bool HasAnimations => _animations.Count > 0;

    public object? GetPropertyValue(TTarget target, string propertyName)
    {
        return _adapter.GetPropertyValue(target, propertyName);
    }

    public void StartAnimation(TTarget target, string propertyName, object? fromValue, object? toValue, Transition transition)
    {
        if (AnimationValueInterpolator.ValuesEqual(fromValue, toValue))
            return;

        if (fromValue == null || toValue == null)
            return;

        StopAnimation(target, propertyName);
        _adapter.SetPropertyValue(target, propertyName, fromValue);

        _animations.Add(new ActiveAnimation
        {
            Target = target,
            PropertyName = propertyName,
            FromValue = fromValue,
            ToValue = toValue,
            DurationMs = Math.Max(1, transition.DurationMs),
            Easing = transition.Easing,
            Impact = _adapter.GetPropertyImpact(propertyName),
            ElapsedMs = 0
        });
    }

    public void StopAnimation(TTarget? target, string? propertyName = null)
    {
        if (target == null)
        {
            _animations.Clear();
            return;
        }

        _animations.RemoveAll(a =>
            ReferenceEquals(a.Target, target) &&
            (propertyName == null || a.PropertyName == propertyName));
    }

    public void StopAnimationsForTarget(TTarget target)
    {
        var targets = new HashSet<TTarget>(ReferenceEqualityComparer<TTarget>.Instance) { target };
        foreach (var descendant in _adapter.GetDescendants(target))
            targets.Add(descendant);

        _animations.RemoveAll(a => targets.Contains(a.Target));
    }

    public AnimationUpdateResult<TTarget> Tick(double deltaMs)
    {
        if (_animations.Count == 0)
            return new AnimationUpdateResult<TTarget>(false, false, false, Array.Empty<TTarget>());

        var hasUpdates = false;
        var needsRelayout = false;
        var needsFullRepaint = false;
        _dirtyTargets.Clear();

        var writeIndex = 0;
        var count = _animations.Count;

        for (var readIndex = 0; readIndex < count; readIndex++)
        {
            var animation = _animations[readIndex];
            animation.ElapsedMs += deltaMs;

            var t = animation.ElapsedMs / animation.DurationMs;
            if (t >= 1.0)
            {
                _adapter.SetPropertyValue(animation.Target, animation.PropertyName, animation.ToValue);
                hasUpdates = true;
            }
            else
            {
                var easedT = AnimationValueInterpolator.ApplyEasing((float)t, animation.Easing);
                var current = AnimationValueInterpolator.Interpolate(animation.FromValue, animation.ToValue, easedT);
                _adapter.SetPropertyValue(animation.Target, animation.PropertyName, current);
                _animations[writeIndex++] = animation;
                hasUpdates = true;
            }

            switch (animation.Impact)
            {
                case AnimationPropertyImpact.Relayout:
                    needsRelayout = true;
                    break;
                case AnimationPropertyImpact.FullRepaint:
                    needsFullRepaint = true;
                    break;
                case AnimationPropertyImpact.ElementRepaint:
                    _dirtyTargets.Add(animation.Target);
                    break;
            }
        }

        if (writeIndex < count)
        {
            _animations.RemoveRange(writeIndex, count - writeIndex);
        }

        return new AnimationUpdateResult<TTarget>(
            hasUpdates,
            needsRelayout,
            needsFullRepaint,
            _dirtyTargets.Count > 0 ? _dirtyTargets : Array.Empty<TTarget>());
    }

    private sealed class ActiveAnimation
    {
        public TTarget Target { get; init; } = null!;
        public string PropertyName { get; init; } = string.Empty;
        public object? FromValue { get; init; }
        public object? ToValue { get; init; }
        public double DurationMs { get; init; }
        public Easing Easing { get; init; }
        public AnimationPropertyImpact Impact { get; init; }
        public double ElapsedMs { get; set; }
    }
}

public static class AnimationValueInterpolator
{
    public static object? Interpolate(object? from, object? to, float t)
    {
        if (from == null || to == null) return t >= 1f ? to : from;
        if (from.GetType() != to.GetType()) return t >= 1f ? to : from;

        return from switch
        {
            Color c => LerpColor(c, (Color)to, t),
            BoxShadow shadow => LerpBoxShadow(shadow, (BoxShadow)to, t),
            float f => LerpFloat(f, (float)to, t),
            int iv => iv + (int)(((int)to - iv) * t),
            Dimension d => LerpDimension(d, (Dimension)to, t),
            Spacing spacing => LerpSpacing(spacing, (Spacing)to, t),
            Transform transform => LerpTransform(transform, (Transform)to, t),
            TransformOrigin origin => LerpTransformOrigin(origin, (TransformOrigin)to, t),
            _ => t >= 1f ? to : from
        };
    }

    public static float ApplyEasing(float t, Easing easing)
    {
        return easing switch
        {
            Easing.Linear => t,
            Easing.Ease => EaseInOutCubic(t),
            Easing.EaseIn => t * t * t,
            Easing.EaseOut => EaseOutCubic(t),
            Easing.EaseInOut => EaseInOutCubic(t),
            _ => t
        };
    }

    private static float EaseOutCubic(float t)
    {
        var inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
            return 4f * t * t * t;

        var x = -2f * t + 2f;
        return 1f - x * x * x / 2f;
    }

    public static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;

        if (a is Transform ta && b is Transform tb)
            return TransformsEqual(ta, tb);

        return a.Equals(b);
    }

    private static Color LerpColor(Color from, Color to, float t)
    {
        return new Color(
            (byte)Math.Clamp(Math.Round(from.R + (to.R - from.R) * t), 0, 255),
            (byte)Math.Clamp(Math.Round(from.G + (to.G - from.G) * t), 0, 255),
            (byte)Math.Clamp(Math.Round(from.B + (to.B - from.B) * t), 0, 255),
            (byte)Math.Clamp(Math.Round(from.A + (to.A - from.A) * t), 0, 255));
    }

    private static BoxShadow LerpBoxShadow(BoxShadow from, BoxShadow to, float t)
    {
        return new BoxShadow(
            LerpColor(from.Color, to.Color, t),
            LerpFloat(from.OffsetY, to.OffsetY, t),
            LerpFloat(from.Blur, to.Blur, t));
    }

    private static Dimension LerpDimension(Dimension from, Dimension to, float t)
    {
        return new Dimension(LerpFloat(from.Value, to.Value, t), to.Unit);
    }

    private static Spacing LerpSpacing(Spacing from, Spacing to, float t)
    {
        return new Spacing(
            LerpDimension(from.Left, to.Left, t),
            LerpDimension(from.Top, to.Top, t),
            LerpDimension(from.Right, to.Right, t),
            LerpDimension(from.Bottom, to.Bottom, t));
    }

    private static TransformOrigin LerpTransformOrigin(TransformOrigin from, TransformOrigin to, float t)
    {
        return new TransformOrigin(LerpFloat(from.X, to.X, t), LerpFloat(from.Y, to.Y, t));
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
        for (var i = 0; i < fromFunctions.Length; i++)
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
        for (var i = 0; i < functions.Length; i++)
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

    private static bool TransformsEqual(Transform a, Transform b)
    {
        var af = a.Functions ?? [];
        var bf = b.Functions ?? [];
        if (af.Length != bf.Length) return false;

        for (var i = 0; i < af.Length; i++)
        {
            if (!Equals(af[i], bf[i])) return false;
        }

        return true;
    }
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
{
    public static ReferenceEqualityComparer<T> Instance { get; } = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
