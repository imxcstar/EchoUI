using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EchoUI.Render.Avalonia
{
    /// <summary>
    /// Implements the IRenderer interface for the Avalonia UI framework.
    /// Manipulates Avalonia controls directly based on commands from the EchoUI reconciler.
    /// </summary>
    public class AvaloniaRenderer : IRenderer
    {
        private readonly Panel _rootContainer;
        private static readonly Dictionary<(Control, string), Delegate> EventWrappers = new();

        public AvaloniaRenderer(Panel rootContainer)
        {
            _rootContainer = rootContainer;
        }

        /// <summary>
        /// Creates a native Avalonia control based on the element type.
        /// </summary>
        public object CreateElement(string type)
        {
            return ToControl(type);
        }

        private Control ToControl(string type)
        {
            return type switch
            {
                // A Border handles background/border properties, and its child StackPanel handles layout.
                ElementCoreName.Container => new Border { Child = new StackPanel() },
                ElementCoreName.Text => new TextBlock(),
                ElementCoreName.Input => new TextBox(),
                _ => new Panel() // Fallback for unknown types
            };
        }

        /// <summary>
        /// Adds a child control to a parent container at a specific index.
        /// </summary>
        public void AddChild(object parent, object child, int index)
        {
            var parentControl = (parent as Control) ?? _rootContainer;
            var childControl = (Control)child;

            var panel = GetPanel(parentControl);
            panel?.Children.Insert(index, childControl);
        }

        /// <summary>
        /// Removes a child control from its parent.
        /// </summary>
        public void RemoveChild(object parent, object child)
        {
            var parentControl = (parent as Control) ?? _rootContainer;
            var childControl = (Control)child;

            var panel = GetPanel(parentControl);
            panel?.Children.Remove(childControl);
        }

        /// <summary>
        /// Moves a child control to a new position within its parent.
        /// </summary>
        public void MoveChild(object parent, object child, int newIndex)
        {
            var parentControl = (parent as Control) ?? _rootContainer;
            var childControl = (Control)child;

            if (GetPanel(parentControl) is { } panel)
            {
                int oldIndex = panel.Children.IndexOf(childControl);
                if (oldIndex != -1)
                {
                    panel.Children.Move(oldIndex, newIndex);
                }
            }
        }

        private Panel? GetPanel(Control control) => control switch
        {
            Panel p => p,
            Border b when b.Child is Panel bp => bp,
            ContentControl cc when cc.Content is Panel cp => cp,
            _ => null
        };

        /// <summary>
        /// Applies property changes to a native Avalonia control.
        /// </summary>
        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            var control = (Control)nativeElement;

            // [!重要!] Always update event handlers with the latest delegate instances from newProps.
            UpdateEventHandlers(control, newProps);

            // Apply default styles for consistency across platforms.
            switch (newProps)
            {
                case ContainerProps:
                    if (control is Border b)
                    {
                        b.ClipToBounds = true;
                        if (b.Child is StackPanel sp)
                        {
                            // Default flexbox-like settings
                            sp.Orientation = Orientation.Vertical;
                        }
                    }
                    break;
                case TextProps:
                    if (control is TextBlock tb)
                    {
                        tb.TextWrapping = TextWrapping.Wrap;
                    }
                    break;
            }

            // Process confirmed property updates from the Reconciler.
            if (patch.UpdatedProperties != null)
            {
                foreach (var (propName, propValue) in patch.UpdatedProperties)
                {
                    TranslatePropertyToAvalonia(control, newProps, propName, propValue);
                }
            }
        }

        /// <summary>
        /// Translates a single property change from the reconciler into a direct property
        /// set on an Avalonia control.
        /// </summary>
        private void TranslatePropertyToAvalonia(Control control, object props, string propName, object? propValue)
        {
            var border = control as Border;
            var stackPanel = border?.Child as StackPanel;
            var textBlock = control as TextBlock;
            var textBox = control as TextBox;

            switch (props)
            {
                case ContainerProps:
                    if (border == null) break;
                    switch (propName)
                    {
                        // --- Layout ---
                        case nameof(ContainerProps.Width): border.Width = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.Height): border.Height = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.MinWidth): border.MinWidth = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.MinHeight): border.MinHeight = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.MaxWidth): border.MaxWidth = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.MaxHeight): border.MaxHeight = ToAvalonia(propValue as Dimension?); break;
                        case nameof(ContainerProps.Margin): border.Margin = ToAvalonia(propValue as Spacing?); break;
                        case nameof(ContainerProps.Padding): border.Padding = ToAvalonia(propValue as Spacing?); break;

                        // --- Flexbox (emulated with StackPanel) ---
                        case nameof(ContainerProps.Direction): if (stackPanel != null) stackPanel.Orientation = propValue is LayoutDirection.Vertical ? Orientation.Vertical : Orientation.Horizontal; break;
                        case nameof(ContainerProps.JustifyContent): if (stackPanel != null) SetJustifyContent(stackPanel, (JustifyContent?)propValue); break;
                        case nameof(ContainerProps.AlignItems): if (stackPanel != null) SetAlignItems(stackPanel, (AlignItems?)propValue); break;
                        case nameof(ContainerProps.Gap): if (stackPanel != null) stackPanel.Spacing = Convert.ToDouble(propValue ?? 0); break;

                        // --- Appearance ---
                        case nameof(ContainerProps.BackgroundColor): border.Background = ToAvalonia(propValue as Core.Color?); break;
                        case nameof(ContainerProps.BorderColor): border.BorderBrush = ToAvalonia(propValue as Core.Color?); break;
                        case nameof(ContainerProps.BorderWidth): border.BorderThickness = new Thickness(Convert.ToDouble(propValue ?? 0)); break;
                        case nameof(ContainerProps.BorderRadius): border.CornerRadius = new CornerRadius(Convert.ToDouble(propValue ?? 0)); break;
                        // BorderStyle is complex in Avalonia and often requires a custom Pen. Solid is the default.

                        // --- Animation ---
                        case nameof(ContainerProps.Transitions): border.Transitions = ToAvalonia(propValue as ValueDictionary<string, Transition>?); break;
                    }
                    break;

                case TextProps:
                    if (textBlock == null) break;
                    switch (propName)
                    {
                        case nameof(TextProps.Text): textBlock.Text = propValue as string; break;
                        case nameof(TextProps.FontFamily): textBlock.FontFamily = new FontFamily(propValue as string ?? "sans-serif"); break;
                        case nameof(TextProps.FontSize): textBlock.FontSize = Convert.ToDouble(propValue ?? 12); break;
                        case nameof(TextProps.Color): textBlock.Foreground = ToAvalonia(propValue as Core.Color?); break;
                        case nameof(TextProps.MouseThrough): textBlock.IsHitTestVisible = !((bool?)propValue == true); break;
                    }
                    break;

                case InputProps:
                    if (textBox == null) break;
                    switch (propName)
                    {
                        case nameof(InputProps.Value): textBox.Text = propValue as string; break;
                    }
                    break;
            }
        }

        #region Property Converters

        private double ToAvalonia(Dimension? dim) => dim.HasValue && dim.Value.Unit == DimensionUnit.Pixels ? dim.Value.Value : double.NaN;
        private IBrush? ToAvalonia(Core.Color? color) => color.HasValue ? new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B)) : null;
        private Thickness ToAvalonia(Spacing? spacing) => spacing.HasValue ? new Thickness(spacing.Value.Left.Value, spacing.Value.Top.Value, spacing.Value.Right.Value, spacing.Value.Bottom.Value) : new Thickness();

        private void SetJustifyContent(StackPanel panel, JustifyContent? jc)
        {
            // JustifyContent aligns children along the main axis.
            var isVertical = panel.Orientation == Orientation.Vertical;
            (HorizontalAlignment, VerticalAlignment) alignment = jc switch
            {
                JustifyContent.Start => (HorizontalAlignment.Left, VerticalAlignment.Top),
                JustifyContent.Center => (HorizontalAlignment.Center, VerticalAlignment.Center),
                JustifyContent.End => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
                _ => (HorizontalAlignment.Left, VerticalAlignment.Top) // SpaceBetween/Around requires a different panel type like Grid.
            };
            foreach (var child in panel.Children.OfType<Control>())
            {
                if (isVertical) child.VerticalAlignment = alignment.Item2; else child.HorizontalAlignment = alignment.Item1;
            }
        }

        private void SetAlignItems(StackPanel panel, AlignItems? ai)
        {
            // AlignItems aligns children along the cross axis.
            var isVertical = panel.Orientation == Orientation.Vertical;
            (HorizontalAlignment, VerticalAlignment) alignment = ai switch
            {
                AlignItems.Start => (HorizontalAlignment.Left, VerticalAlignment.Top),
                AlignItems.Center => (HorizontalAlignment.Center, VerticalAlignment.Center),
                AlignItems.End => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
                _ => (HorizontalAlignment.Stretch, VerticalAlignment.Stretch)
            };
            foreach (var child in panel.Children.OfType<Control>())
            {
                if (isVertical) child.HorizontalAlignment = alignment.Item1; else child.VerticalAlignment = alignment.Item2;
            }
        }

        private Transitions? ToAvalonia(ValueDictionary<string, Transition>? transitions)
        {
            var data = transitions?.Data;
            if (data == null || data.Count == 0) return null;

            var avaloniaTransitions = new Transitions();
            foreach (var (propName, transition) in data)
            {
                if (CSharpPropToAvaloniaProp(propName) is not { } avaloniaProp) continue;

                var avaloniaEasing = ToAvalonia(transition.Easing);

                // Note: This is simplified. A full implementation needs to check property type.
                if (avaloniaProp.PropertyType == typeof(double))
                {
                    avaloniaTransitions.Add(new DoubleTransition { Property = avaloniaProp, Duration = TimeSpan.FromMilliseconds(transition.DurationMs), Easing = avaloniaEasing });
                }
                else if (avaloniaProp.PropertyType == typeof(IBrush))
                {
                    avaloniaTransitions.Add(new BrushTransition { Property = avaloniaProp, Duration = TimeSpan.FromMilliseconds(transition.DurationMs), Easing = avaloniaEasing });
                }
            }
            return avaloniaTransitions.Any() ? avaloniaTransitions : null;
        }

        private AvaloniaProperty? CSharpPropToAvaloniaProp(string propName) => propName switch
        {
            nameof(ContainerProps.BackgroundColor) => Border.BackgroundProperty,
            // Add other animatable properties here...
            _ => null
        };

        private global::Avalonia.Animation.Easings.Easing ToAvalonia(Core.Easing easing) => easing switch
        {
            Core.Easing.Ease => new SplineEasing(0.25, 0.1, 0.25, 1.0),
            Core.Easing.EaseIn => new SplineEasing(0.42, 0.0, 1.0, 1.0),
            Core.Easing.EaseOut => new SplineEasing(0.0, 0.0, 0.58, 1.0),
            Core.Easing.EaseInOut => new SplineEasing(0.42, 0.0, 0.58, 1.0),
            _ => new LinearEasing()
        };

        #endregion

        #region Event Handling

        private void UpdateEventHandlers(Control control, Props newProps)
        {
            if (newProps is ContainerProps p)
            {
                UpdateHandler<TappedEventArgs>(control, nameof(p.OnClick), p.OnClick, h => control.Tapped += h, h => control.Tapped -= h, handler => (s, e) => ((Action<Core.MouseButton>?)handler)?.Invoke(Core.MouseButton.Left));
                UpdateHandler<PointerEventArgs>(control, nameof(p.OnMouseMove), p.OnMouseMove, h => control.PointerMoved += h, h => control.PointerMoved -= h, handler => (s, e) => ((Action<Core.Point>?)handler)?.Invoke(ToPoint(e.GetPosition((Visual?)s))));
                UpdateHandler<PointerPressedEventArgs>(control, nameof(p.OnMouseDown), p.OnMouseDown, h => control.PointerPressed += h, h => control.PointerPressed -= h, handler => (s, e) => ((Action?)handler)?.Invoke());
                UpdateHandler<PointerReleasedEventArgs>(control, nameof(p.OnMouseUp), p.OnMouseUp, h => control.PointerReleased += h, h => control.PointerReleased -= h, handler => (s, e) => ((Action?)handler)?.Invoke());
                UpdateHandler<PointerEventArgs>(control, nameof(p.OnMouseEnter), p.OnMouseEnter, h => control.PointerEntered += h, h => control.PointerEntered -= h, handler => (s, e) => ((Action?)handler)?.Invoke());
                UpdateHandler<PointerEventArgs>(control, nameof(p.OnMouseLeave), p.OnMouseLeave, h => control.PointerExited += h, h => control.PointerExited -= h, handler => (s, e) => ((Action?)handler)?.Invoke());
                UpdateHandler<KeyEventArgs>(control, nameof(p.OnKeyDown), p.OnKeyDown, h => control.KeyDown += h, h => control.KeyDown -= h, handler => (s, e) => ((Action<int>?)handler)?.Invoke((int)e.Key));
                UpdateHandler<KeyEventArgs>(control, nameof(p.OnKeyUp), p.OnKeyUp, h => control.KeyUp += h, h => control.KeyUp -= h, handler => (s, e) => ((Action<int>?)handler)?.Invoke((int)e.Key));
            }
            else if (newProps is InputProps ip && control is TextBox textBox)
            {
                UpdateHandler<TextChangedEventArgs>(textBox, nameof(ip.OnValueChanged), ip.OnValueChanged, h => textBox.TextChanged += h, h => textBox.TextChanged -= h, handler => (s, e) => ((Action<string>?)handler)?.Invoke(((TextBox?)s)?.Text ?? string.Empty));
            }
        }

        private void UpdateHandler<TEventArgs>(Control control, string eventName, Delegate? newHandler, Action<EventHandler<TEventArgs>> add, Action<EventHandler<TEventArgs>> remove, Func<Delegate?, EventHandler<TEventArgs>> createWrapper) where TEventArgs : RoutedEventArgs
        {
            var key = (control, eventName);

            if (EventWrappers.TryGetValue(key, out var oldWrapper))
            {
                remove((EventHandler<TEventArgs>)oldWrapper);
                EventWrappers.Remove(key);
            }

            if (newHandler != null)
            {
                var newWrapper = createWrapper(newHandler);
                add(newWrapper);
                EventWrappers[key] = newWrapper;
            }
        }

        private Core.Point ToPoint(global::Avalonia.Point p) => new((int)p.X, (int)p.Y);
        private Core.MouseButton ToMouseButton(PointerUpdateKind kind) => kind switch
        {
            PointerUpdateKind.LeftButtonPressed => Core.MouseButton.Left,
            PointerUpdateKind.RightButtonPressed => Core.MouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => Core.MouseButton.Middle,
            PointerUpdateKind.LeftButtonReleased => Core.MouseButton.Left,
            PointerUpdateKind.RightButtonReleased => Core.MouseButton.Right,
            PointerUpdateKind.MiddleButtonReleased => Core.MouseButton.Middle,
            _ => Core.MouseButton.Left
        };

        #endregion

        /// <summary>
        /// Provides a scheduler that ensures UI updates run on the Avalonia UI thread.
        /// </summary>
        public IUpdateScheduler GetScheduler(object rootContainer) => new AvaloniaUpdateScheduler();
    }

    /// <summary>
    /// A web-specific patch object that can be directly serialized to JSON for the JS interop layer.
    /// This is created by the WebRenderer from the generic PropertyPatch.
    /// </summary>
    public class AvaloniaUpdateScheduler : IUpdateScheduler
    {
        public void Schedule(Func<Task> updateAction)
        {
            // Ensure the update logic is dispatched to the UI thread.
            Dispatcher.UIThread.InvokeAsync(updateAction);
        }
    }
}