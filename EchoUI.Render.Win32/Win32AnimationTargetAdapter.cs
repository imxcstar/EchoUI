using EchoUI.Core;

namespace EchoUI.Render.Win32;

internal sealed class Win32AnimationTargetAdapter : IAnimationTargetAdapter<Win32Element>
{
    public object? GetPropertyValue(Win32Element target, string propertyName)
    {
        return propertyName switch
        {
            nameof(Win32Element.BackgroundColor) => target.BackgroundColor,
            nameof(Win32Element.BorderColor) => target.BorderColor,
            nameof(Win32Element.Shadow) => target.Shadow,
            nameof(Win32Element.BorderWidth) => target.BorderWidth,
            nameof(Win32Element.BorderRadius) => target.BorderRadius,
            nameof(Win32Element.Margin) => target.Margin,
            nameof(Win32Element.Padding) => target.Padding,
            nameof(Win32Element.Width) => target.Width,
            nameof(Win32Element.Height) => target.Height,
            nameof(Win32Element.MinWidth) => target.MinWidth,
            nameof(Win32Element.MinHeight) => target.MinHeight,
            nameof(Win32Element.MaxWidth) => target.MaxWidth,
            nameof(Win32Element.MaxHeight) => target.MaxHeight,
            nameof(Win32Element.Gap) => target.Gap,
            nameof(Win32Element.Transform) => target.Transform,
            nameof(Win32Element.TransformOrigin) => target.TransformOrigin,
            _ => null
        };
    }

    public void SetPropertyValue(Win32Element target, string propertyName, object? value)
    {
        switch (propertyName)
        {
            case nameof(Win32Element.BackgroundColor):
                target.BackgroundColor = (Color?)value;
                break;
            case nameof(Win32Element.BorderColor):
                target.BorderColor = (Color?)value;
                break;
            case nameof(Win32Element.Shadow):
                target.Shadow = value is BoxShadow shadow ? shadow : BoxShadow.None;
                break;
            case nameof(Win32Element.BorderWidth):
                target.BorderWidth = value is float bw ? bw : 0;
                break;
            case nameof(Win32Element.BorderRadius):
                target.BorderRadius = value is float br ? br : 0;
                break;
            case nameof(Win32Element.Margin):
                target.Margin = (Spacing?)value;
                break;
            case nameof(Win32Element.Padding):
                target.Padding = (Spacing?)value;
                break;
            case nameof(Win32Element.Width):
                target.Width = (Dimension?)value;
                break;
            case nameof(Win32Element.Height):
                target.Height = (Dimension?)value;
                break;
            case nameof(Win32Element.MinWidth):
                target.MinWidth = (Dimension?)value;
                break;
            case nameof(Win32Element.MinHeight):
                target.MinHeight = (Dimension?)value;
                break;
            case nameof(Win32Element.MaxWidth):
                target.MaxWidth = (Dimension?)value;
                break;
            case nameof(Win32Element.MaxHeight):
                target.MaxHeight = (Dimension?)value;
                break;
            case nameof(Win32Element.Gap):
                target.Gap = value is float gap ? gap : 0;
                break;
            case nameof(Win32Element.Transform):
                target.Transform = value is Transform transform ? transform : new Transform();
                break;
            case nameof(Win32Element.TransformOrigin):
                target.TransformOrigin = value is TransformOrigin origin ? origin : TransformOrigin.Center;
                break;
        }
    }

    public AnimationPropertyImpact GetPropertyImpact(string propertyName)
    {
        return propertyName switch
        {
            nameof(Win32Element.BorderWidth) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Margin) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Padding) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Width) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Height) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.MinWidth) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.MinHeight) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.MaxWidth) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.MaxHeight) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Gap) => AnimationPropertyImpact.Relayout,
            nameof(Win32Element.Transform) => AnimationPropertyImpact.FullRepaint,
            nameof(Win32Element.TransformOrigin) => AnimationPropertyImpact.FullRepaint,
            nameof(Win32Element.BackgroundColor) => AnimationPropertyImpact.ElementRepaint,
            nameof(Win32Element.BorderColor) => AnimationPropertyImpact.ElementRepaint,
            nameof(Win32Element.Shadow) => AnimationPropertyImpact.ElementRepaint,
            nameof(Win32Element.BorderRadius) => AnimationPropertyImpact.ElementRepaint,
            _ => AnimationPropertyImpact.None
        };
    }

    public IEnumerable<Win32Element> GetDescendants(Win32Element target)
    {
        foreach (var child in target.Children)
        {
            yield return child;
            foreach (var descendant in GetDescendants(child))
                yield return descendant;
        }
    }
}
