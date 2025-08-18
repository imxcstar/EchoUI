using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Namespace for the Avalonia-specific renderer implementation.
namespace EchoUI.Render.Avalonia
{
    /// <summary>
    /// Implements the IRenderer interface for Avalonia UI.
    /// Translates EchoUI elements into native Avalonia controls using composition.
    /// </summary>
    public class AvaloniaRenderer : IRenderer
    {
        private readonly Dictionary<Control, Dictionary<string, (RoutedEvent, Delegate)>> _eventHandlers = new();

        public object CreateElement(string type)
        {
            return type switch
            {
                "Container" => new Border { Child = new LayoutablePanel() },
                "Text" => new TextBlock { TextWrapping = TextWrapping.NoWrap },
                _ => throw new NotSupportedException($"Native element type '{type}' is not supported.")
            };
        }

        public void SetProps(object nativeElement, Core.Props? oldProps, Core.Props newProps)
        {
            if (nativeElement is not Control control) return;

            switch (newProps)
            {
                case ContainerProps p:
                    SetContainerProps(control, oldProps as ContainerProps, p);
                    break;
                case TextProps p:
                    SetTextProps(control, oldProps as TextProps, p);
                    break;
            }
        }

        public void AddChild(object parent, object child, int index)
        {
            if (child is not Control childControl) return;

            // Handle adding the first element to the root DockPanel
            if (parent is DockPanel dockPanel)
            {
                // DockPanel doesn't support inserting at an index, just add.
                dockPanel.Children.Add(childControl);
            }
            // Handle adding elements to your custom container
            else if (parent is Border { Child: LayoutablePanel panel })
            {
                panel.Children.Insert(index, childControl);
            }
        }

        public void RemoveChild(object parent, object child)
        {
            if (child is not Control childControl) return;

            if (parent is DockPanel dockPanel)
            {
                dockPanel.Children.Remove(childControl);
            }
            else if (parent is Border { Child: LayoutablePanel panel })
            {
                panel.Children.Remove(childControl);
            }
        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            if (child is not Control childControl) return;

            // Moving children within the root DockPanel isn't typically required for this setup,
            // as it will only ever have one child. But for completeness:
            if (parent is DockPanel dockPanel)
            {
                int oldIndex = dockPanel.Children.IndexOf(childControl);
                if (oldIndex != -1)
                {
                    dockPanel.Children.Move(oldIndex, newIndex);
                }
            }
            else if (parent is Border { Child: LayoutablePanel panel })
            {
                int oldIndex = panel.Children.IndexOf(childControl);
                if (oldIndex != -1)
                {
                    panel.Children.Move(oldIndex, newIndex);
                }
            }
        }

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            return new AvaloniaUpdateScheduler();
        }

        #region Prop Setters

        private void SetContainerProps(object nativeElement, ContainerProps? oldProps, ContainerProps newProps)
        {
            if (nativeElement is not Border border || border.Child is not LayoutablePanel panel) return;

            UpdateSize(border, oldProps, newProps);
            UpdateSpacing(border, oldProps?.Margin, newProps.Margin, isMargin: true);

            if (oldProps?.BackgroundColor != newProps.BackgroundColor)
            {
                border.Background = newProps.BackgroundColor?.ToAvaloniaBrush();
            }

            border.BorderThickness = new Thickness(newProps.BorderWidth ?? 0);
            border.CornerRadius = new CornerRadius(newProps.BorderRadius ?? 0);
            border.BorderBrush = newProps.BorderColor?.ToAvaloniaBrush() ?? Brushes.Transparent;

            UpdateEventHandler(border, "OnMouseMove", InputElement.PointerMovedEvent, oldProps?.OnMouseMove, newProps.OnMouseMove,
                (Action<Core.Point> a) => (sender, e) => a(new Core.Point((int)e.GetPosition(sender as Visual).X, (int)e.GetPosition(sender as Visual).Y)));

            UpdateEventHandler(border, "OnMouseEnter", InputElement.PointerEnteredEvent, oldProps?.OnMouseEnter, newProps.OnMouseEnter,
                    (Action a) => (sender, e) => a(), RoutingStrategies.Direct);

            UpdateEventHandler(border, "OnMouseLeave", InputElement.PointerExitedEvent, oldProps?.OnMouseLeave, newProps.OnMouseLeave,
                (Action a) => (sender, e) => a(), RoutingStrategies.Direct);

            UpdateEventHandler(border, "OnMouseDown", InputElement.PointerPressedEvent, oldProps?.OnMouseDown, newProps.OnMouseDown,
                (Action a) => (sender, e) => { (sender as IInputElement)?.Focus(); a(); });

            UpdateEventHandler(border, "OnMouseUp", InputElement.PointerReleasedEvent, oldProps?.OnMouseUp, newProps.OnMouseUp,
                (Action a) => (sender, e) => a());

            UpdateEventHandler(border, "OnClick", InputElement.PointerReleasedEvent, oldProps?.OnClick, newProps.OnClick,
                (Action<Core.MouseButton> a) => (sender, e) =>
                {
                    var button = e.InitialPressMouseButton switch
                    {
                        global::Avalonia.Input.MouseButton.Left => Core.MouseButton.Left,
                        global::Avalonia.Input.MouseButton.Right => Core.MouseButton.Right,
                        global::Avalonia.Input.MouseButton.Middle => Core.MouseButton.Middle,
                        _ => Core.MouseButton.Left
                    };
                    a(button);
                });

            UpdateEventHandler(border, "OnKeyDown", InputElement.KeyDownEvent, oldProps?.OnKeyDown, newProps.OnKeyDown,
                (Action<int> a) => (sender, e) => a((int)e.Key));

            UpdateEventHandler(border, "OnKeyUp", InputElement.KeyUpEvent, oldProps?.OnKeyUp, newProps.OnKeyUp,
               (Action<int> a) => (sender, e) => a((int)e.Key));

            UpdateSpacing(panel, oldProps?.Padding, newProps.Padding, isMargin: false);

            bool layoutChanged = false;
            if (oldProps?.Direction != newProps.Direction) { panel.UIDirection = newProps.Direction ?? Core.LayoutDirection.Vertical; layoutChanged = true; }
            if (oldProps?.JustifyContent != newProps.JustifyContent) { panel.UIJustifyContent = newProps.JustifyContent ?? Core.JustifyContent.Start; layoutChanged = true; }
            if (oldProps?.AlignItems != newProps.AlignItems) { panel.UIAlignItems = newProps.AlignItems ?? Core.AlignItems.Start; layoutChanged = true; }
            if (oldProps?.Gap != newProps.Gap) { panel.UIGap = newProps.Gap ?? 0f; layoutChanged = true; }
            if (layoutChanged) panel.InvalidateArrange();
        }

        private void SetTextProps(object nativeElement, TextProps? oldProps, TextProps newProps)
        {
            if (nativeElement is not TextBlock textBlock) return;

            if (oldProps?.Text != newProps.Text)
            {
                textBlock.Text = newProps.Text;
            }
            if (oldProps?.Color != newProps.Color)
            {
                textBlock.Foreground = newProps.Color?.ToAvaloniaBrush() ?? Brushes.Black;
            }
            if (oldProps?.FontFamily != newProps.FontFamily || oldProps?.FontSize != newProps.FontSize)
            {
                if (newProps.FontFamily != null) textBlock.FontFamily = new FontFamily(newProps.FontFamily);
                if (newProps.FontSize.HasValue) textBlock.FontSize = newProps.FontSize.Value;
            }
            if (oldProps?.MouseThrough != newProps.MouseThrough)
            {
                textBlock.IsHitTestVisible = !newProps.MouseThrough;
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateSize(Control control, ContainerProps? oldProps, ContainerProps newProps)
        {
            double GetPixels(Dimension? dim)
            {
                if (dim == null || dim.Value.Unit == DimensionUnit.Auto) return double.NaN;
                return dim.Value.Unit switch
                {
                    DimensionUnit.Pixels => dim.Value.Value,
                    _ => double.NaN,
                };
            }
            if (oldProps?.Width != newProps.Width) control.Width = GetPixels(newProps.Width);
            if (oldProps?.Height != newProps.Height) control.Height = GetPixels(newProps.Height);
            if (oldProps?.MinWidth != newProps.MinWidth) control.MinWidth = GetPixels(newProps.MinWidth);
            if (oldProps?.MinHeight != newProps.MinHeight) control.MinHeight = GetPixels(newProps.MinHeight);
            if (oldProps?.MaxWidth != newProps.MaxWidth) control.MaxWidth = GetPixels(newProps.MaxWidth);
            if (oldProps?.MaxHeight != newProps.MaxHeight) control.MaxHeight = GetPixels(newProps.MaxHeight);
        }

        private void UpdateSpacing(Control control, Spacing? oldSpacing, Spacing? newSpacing, bool isMargin)
        {
            if (oldSpacing == newSpacing) return;
            var s = newSpacing ?? default;
            var thickness = new Thickness(s.Left.Value, s.Top.Value, s.Right.Value, s.Bottom.Value);

            if (isMargin)
            {
                control.Margin = thickness;
            }
            else
            {
                if (control is LayoutablePanel panel)
                {
                    panel.UIPadding = thickness;
                }
            }
        }

        private void UpdateEventHandler<T, TArgs>(Control control, string propName, RoutedEvent<TArgs> routedEvent, T? oldAction, T? newAction, Func<T, EventHandler<TArgs>> converter, RoutingStrategies strategy = RoutingStrategies.Bubble)
            where T : Delegate
            where TArgs : RoutedEventArgs
        {
            if (EqualityComparer<T>.Default.Equals(oldAction, newAction)) return;
            if (!_eventHandlers.ContainsKey(control))
            {
                _eventHandlers[control] = new Dictionary<string, (RoutedEvent, Delegate)>();
            }
            if (oldAction != null && _eventHandlers[control].TryGetValue(propName, out var oldHandlerInfo))
            {
                control.RemoveHandler(oldHandlerInfo.Item1, oldHandlerInfo.Item2);
                _eventHandlers[control].Remove(propName);
            }
            if (newAction != null)
            {
                var newHandler = converter(newAction);
                // Use the new strategy parameter here
                control.AddHandler(routedEvent, newHandler, strategy);
                _eventHandlers[control][propName] = (routedEvent, newHandler);
            }
        }

        #endregion
    }

    public class LayoutablePanel : Panel
    {
        #region UI Properties
        private Core.LayoutDirection _direction = Core.LayoutDirection.Vertical;
        public Core.LayoutDirection UIDirection { get => _direction; set { SetAndInvalidate(ref _direction, value); } }

        private Core.JustifyContent _justifyContent = Core.JustifyContent.Start;
        public Core.JustifyContent UIJustifyContent { get => _justifyContent; set { SetAndInvalidate(ref _justifyContent, value); } }

        private Core.AlignItems _alignItems = Core.AlignItems.Start;
        public Core.AlignItems UIAlignItems { get => _alignItems; set { SetAndInvalidate(ref _alignItems, value); } }

        private float _gap;
        public float UIGap { get => _gap; set { SetAndInvalidate(ref _gap, value); } }

        private Thickness _padding;
        public Thickness UIPadding { get => _padding; set { SetAndInvalidate(ref _padding, value); } }
        #endregion

        public LayoutablePanel()
        {
            Background = Brushes.Transparent;
        }

        private void SetAndInvalidate<T>(ref T field, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                InvalidateMeasure(); // InvalidateMeasure is more appropriate here
            }
        }

        // --- CORRECTED MeasureOverride ---
        protected override Size MeasureOverride(Size availableSize)
        {
            var children = Children.Where(c => c.IsVisible).ToList();
            var paddedSize = availableSize.Deflate(UIPadding);
            bool isVertical = UIDirection == Core.LayoutDirection.Vertical;

            double mainAxisSize = 0;
            double crossAxisSize = 0;

            if (!children.Any())
            {
                return new Size(UIPadding.Left + UIPadding.Right, UIPadding.Top + UIPadding.Bottom);
            }

            var childConstraint = isVertical
                ? new Size(paddedSize.Width, double.PositiveInfinity)
                : new Size(double.PositiveInfinity, paddedSize.Height);

            foreach (var child in children)
            {
                child.Measure(childConstraint);
                var margin = child.Margin;
                if (isVertical)
                {
                    mainAxisSize += child.DesiredSize.Height + margin.Top + margin.Bottom;
                    crossAxisSize = Math.Max(crossAxisSize, child.DesiredSize.Width + margin.Left + margin.Right);
                }
                else
                {
                    mainAxisSize += child.DesiredSize.Width + margin.Left + margin.Right;
                    crossAxisSize = Math.Max(crossAxisSize, child.DesiredSize.Height + margin.Top + margin.Bottom);
                }
            }

            mainAxisSize += (children.Count - 1) * UIGap;

            double desiredWidth, desiredHeight;
            if (isVertical)
            {
                desiredWidth = (UIAlignItems == Core.AlignItems.Stretch && !double.IsInfinity(availableSize.Width))
                    ? availableSize.Width
                    : crossAxisSize + UIPadding.Left + UIPadding.Right;

                desiredHeight = (UIJustifyContent == Core.JustifyContent.Stretch && !double.IsInfinity(availableSize.Height))
                    ? availableSize.Height
                    : mainAxisSize + UIPadding.Top + UIPadding.Bottom;
            }
            else // Horizontal
            {
                desiredWidth = (UIJustifyContent == Core.JustifyContent.Stretch && !double.IsInfinity(availableSize.Width))
                    ? availableSize.Width
                    : mainAxisSize + UIPadding.Left + UIPadding.Right;

                desiredHeight = (UIAlignItems == Core.AlignItems.Stretch && !double.IsInfinity(availableSize.Height))
                    ? availableSize.Height
                    : crossAxisSize + UIPadding.Top + UIPadding.Bottom;
            }

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = Children.Where(c => c.IsVisible).ToList();
            if (!children.Any()) return finalSize;

            bool isVertical = UIDirection == Core.LayoutDirection.Vertical;
            var contentRect = new Rect(finalSize).Deflate(UIPadding);

            double totalChildrenMainSize = children.Sum(c => {
                var margin = c.Margin;
                return isVertical
                    ? c.DesiredSize.Height + margin.Top + margin.Bottom
                    : c.DesiredSize.Width + margin.Left + margin.Right;
            });

            double totalGap = (children.Count - 1) * UIGap;
            double mainAxisAvailable = isVertical ? contentRect.Height : contentRect.Width;
            double remainingSpace = mainAxisAvailable - totalChildrenMainSize - totalGap;

            double currentPos = isVertical ? contentRect.Top : contentRect.Left;
            double spacing = 0;
            double extraSpacePerChild = 0;

            switch (UIJustifyContent)
            {
                case Core.JustifyContent.Center: currentPos += remainingSpace / 2; break;
                case Core.JustifyContent.End: currentPos += remainingSpace; break;
                case Core.JustifyContent.SpaceBetween: if (children.Count > 1) spacing = remainingSpace / (children.Count - 1); break;
                case Core.JustifyContent.SpaceAround: if (children.Count > 1) { spacing = remainingSpace / children.Count; currentPos += spacing; } break; // Fix: SpaceAround spacing and offset logic
                case Core.JustifyContent.Stretch: if (children.Count > 0 && remainingSpace > 0) extraSpacePerChild = remainingSpace / children.Count; break;
            }

            foreach (var child in children)
            {
                var margin = child.Margin;
                var desiredSize = child.DesiredSize;
                var finalChildSize = desiredSize;

                double mainMarginStart = isVertical ? margin.Top : margin.Left;
                double mainMarginEnd = isVertical ? margin.Bottom : margin.Right;
                double crossMarginStart = isVertical ? margin.Left : margin.Top;
                double crossMarginEnd = isVertical ? margin.Right : margin.Bottom;

                double crossAxisAvailable = isVertical ? contentRect.Width : contentRect.Height;

                // Handle stretching on both axes
                if (isVertical)
                {
                    if (UIAlignItems == Core.AlignItems.Stretch)
                        finalChildSize = finalChildSize.WithWidth(Math.Max(0, crossAxisAvailable - crossMarginStart - crossMarginEnd));
                    if (extraSpacePerChild > 0)
                        finalChildSize = finalChildSize.WithHeight(finalChildSize.Height + extraSpacePerChild);
                }
                else // Horizontal
                {
                    if (UIAlignItems == Core.AlignItems.Stretch)
                        finalChildSize = finalChildSize.WithHeight(Math.Max(0, crossAxisAvailable - crossMarginStart - crossMarginEnd));
                    if (extraSpacePerChild > 0)
                        finalChildSize = finalChildSize.WithWidth(finalChildSize.Width + extraSpacePerChild);
                }

                double childCrossSize = isVertical ? finalChildSize.Width : finalChildSize.Height;
                double crossOffset = 0;
                switch (UIAlignItems)
                {
                    case Core.AlignItems.Center: crossOffset = (crossAxisAvailable - childCrossSize - crossMarginStart - crossMarginEnd) / 2; break;
                    case Core.AlignItems.End: crossOffset = crossAxisAvailable - childCrossSize - crossMarginStart - crossMarginEnd; break;
                }

                double mainPos = currentPos + mainMarginStart;
                double crossPos = (isVertical ? contentRect.Left : contentRect.Top) + crossMarginStart + crossOffset;

                var finalRect = isVertical
                    ? new Rect(crossPos, mainPos, finalChildSize.Width, finalChildSize.Height)
                    : new Rect(mainPos, crossPos, finalChildSize.Width, finalChildSize.Height);

                child.Arrange(finalRect);

                currentPos += mainMarginStart + (isVertical ? finalChildSize.Height : finalChildSize.Width) + mainMarginEnd + UIGap + (UIJustifyContent == Core.JustifyContent.SpaceAround ? spacing : 0);
                if (UIJustifyContent == Core.JustifyContent.SpaceBetween) currentPos += spacing;
            }

            return finalSize;
        }
    }

    public class AvaloniaUpdateScheduler : IUpdateScheduler
    {
        public void Schedule(Func<Task> updateAction)
        {
            Dispatcher.UIThread.Post(async () => await updateAction(), DispatcherPriority.Background);
        }
    }

    public static class AvaloniaConversionExtensions
    {
        public static global::Avalonia.Media.Color ToAvaloniaColor(this Core.Color color)
        {
            return global::Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static IBrush ToAvaloniaBrush(this Core.Color color)
        {
            return new SolidColorBrush(color.ToAvaloniaColor());
        }
    }
}