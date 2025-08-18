using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using EchoUI.Core;

// Using statements for WinForms and Drawing are still necessary,
// but types will be fully qualified where ambiguity exists.
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;

namespace EchoUI.Render.Winform
{
    /// <summary>
    /// Implements the IRenderer interface for Windows Forms.
    /// Translates EchoUI elements into native WinForms controls.
    /// </summary>
    public class WinformRenderer : IRenderer
    {
        // A map to store event handlers for each control to allow for unsubscribing later.
        private readonly Dictionary<Control, Dictionary<string, Delegate>> _eventHandlers = new();

        public object CreateElement(string type)
        {
            return type switch
            {
                "Container" => new LayoutablePanel(),
                "Text" => new ClickThroughLabel(),
                _ => throw new NotSupportedException($"Native element type '{type}' is not supported.")
            };
        }

        public void SetProps(object nativeElement, Core.Props? oldProps, Core.Props newProps)
        {
            if (nativeElement is not Control control) return;

            // Use pattern matching to handle different prop types
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
            if (parent is Control parentControl && child is Control childControl)
            {
                parentControl.Controls.Add(childControl);
                parentControl.Controls.SetChildIndex(childControl, index);
            }
        }

        public void RemoveChild(object parent, object child)
        {
            if (parent is Control parentControl && child is Control childControl)
            {
                parentControl.Controls.Remove(childControl);
                childControl.Dispose(); // Clean up resources
            }
        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            if (parent is Control parentControl && child is Control childControl)
            {
                parentControl.Controls.SetChildIndex(childControl, newIndex);
            }
        }

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            if (rootContainer is not Control control)
            {
                throw new ArgumentException("The root container must be a System.Windows.Forms.Control.", nameof(rootContainer));
            }
            return new WinformsUpdateScheduler(control);
        }

        #region Prop Setters

        private void SetContainerProps(Control control, ContainerProps? oldProps, ContainerProps newProps)
        {
            var panel = control as LayoutablePanel;
            if (panel == null) return;

            // Layout (Size & Margin)
            UpdateSize(control, oldProps, newProps);
            UpdateSpacing(control, oldProps?.Margin, newProps.Margin, isMargin: true);
            UpdateSpacing(control, oldProps?.Padding, newProps.Padding, isMargin: false);

            // Background
            if (oldProps?.BackgroundColor != newProps.BackgroundColor)
            {
                control.BackColor = newProps.BackgroundColor?.ToDrawingColor() ?? SystemColors.Control;
            }

            // Border
            if (panel != null)
            {
                float scaleFactor = GetDpiScaleFactor(control);
                bool borderChanged = false;
                if (oldProps?.BorderStyle != newProps.BorderStyle) { panel.UIBorderStyle = newProps.BorderStyle ?? Core.BorderStyle.None; borderChanged = true; }
                if (oldProps?.BorderColor != newProps.BorderColor) { panel.UIBorderColor = newProps.BorderColor?.ToDrawingColor() ?? System.Drawing.Color.Black; borderChanged = true; }
                if (oldProps?.BorderWidth != newProps.BorderWidth) { panel.UIBorderWidth = (newProps.BorderWidth ?? 0f) * scaleFactor; borderChanged = true; }
                if (oldProps?.BorderRadius != newProps.BorderRadius) { panel.UIBorderRadius = (newProps.BorderRadius ?? 0f) * scaleFactor; borderChanged = true; }
                if (borderChanged) control.Invalidate();
            }

            // Child Layout
            if (panel != null)
            {
                float scaleFactor = GetDpiScaleFactor(control);
                bool layoutChanged = false;
                if (oldProps?.Direction != newProps.Direction) { panel.UIDirection = newProps.Direction ?? Core.LayoutDirection.Vertical; layoutChanged = true; }
                if (oldProps?.JustifyContent != newProps.JustifyContent) { panel.UIJustifyContent = newProps.JustifyContent ?? Core.JustifyContent.Start; layoutChanged = true; }
                if (oldProps?.AlignItems != newProps.AlignItems) { panel.UIAlignItems = newProps.AlignItems ?? Core.AlignItems.Start; layoutChanged = true; }
                if (oldProps?.Gap != newProps.Gap) { panel.UIGap = (newProps.Gap ?? 0f) * scaleFactor; layoutChanged = true; }
                if (layoutChanged) control.PerformLayout();
            }

            // Interactions
            UpdateEventHandler(control, "MouseMove", oldProps?.OnMouseMove, newProps.OnMouseMove, (Action<Core.Point> a) =>
                new MouseEventHandler((sender, e) =>
                {
                    a(new Core.Point(e.X, e.Y));
                }));

            UpdateEventHandler(control, "MouseEnter", oldProps?.OnMouseEnter, newProps.OnMouseEnter, (Action a) =>
                new EventHandler((sender, e) => a()));

            UpdateEventHandler(control, "MouseLeave", oldProps?.OnMouseLeave, newProps.OnMouseLeave, (Action a) =>
                new EventHandler((sender, e) => a()));

            UpdateEventHandler(control, "MouseDown", oldProps?.OnMouseDown, newProps.OnMouseDown, (Action a) =>
                new MouseEventHandler((sender, e) =>
                {
                    (sender as Control)?.Focus();
                    a();
                }));

            UpdateEventHandler(control, "MouseUp", oldProps?.OnMouseUp, newProps.OnMouseUp, (Action a) =>
                new MouseEventHandler((sender, e) =>
                {
                    (sender as Control)?.Focus();
                    a();
                }));

            UpdateEventHandler(control, "MouseUp", oldProps?.OnClick, newProps.OnClick, (Action<Core.MouseButton> a) =>
                new MouseEventHandler((sender, e) =>
                {
                    (sender as Control)?.Focus();
                    // 这里可以判断一下控件是否还处于按下状态，防止意外触发，但通常直接执行逻辑即可
                    var button = e.Button switch
                    {
                        MouseButtons.Left => Core.MouseButton.Left,
                        MouseButtons.Right => Core.MouseButton.Right,
                        _ => Core.MouseButton.Middle
                    };
                    a(button);
                }));

            UpdateEventHandler(control, "KeyDown", oldProps?.OnKeyDown, newProps.OnKeyDown, (Action<int> a) =>
                new KeyEventHandler((sender, e) =>
                {
                    a(e.KeyValue);
                }));

            UpdateEventHandler(control, "KeyUp", oldProps?.OnKeyUp, newProps.OnKeyUp, (Action<int> a) =>
                new KeyEventHandler((sender, e) =>
                {
                    a(e.KeyValue);
                }));
        }

        private void SetTextProps(Control control, TextProps? oldProps, TextProps newProps)
        {
            if (control is not ClickThroughLabel label) return;

            if (oldProps?.Text != newProps.Text)
            {
                label.Text = newProps.Text;
            }
            if (oldProps?.Color != newProps.Color)
            {
                label.ForeColor = newProps.Color?.ToDrawingColor() ?? SystemColors.ControlText;
            }

            // Check if any font-related properties have changed.
            bool fontChanged = oldProps?.FontFamily != newProps.FontFamily ||
                               oldProps?.FontSize != newProps.FontSize;

            if (fontChanged)
            {
                // 1. Get the DPI scale factor for this specific control.
                float scaleFactor = GetDpiScaleFactor(label);

                // Keep a reference to the old font to dispose of it later.
                var oldFont = label.Font;

                // 2. Determine the base font family and size from your props.
                var family = newProps.FontFamily ?? oldFont.FontFamily.Name;
                // Use the old font's unscaled size as a fallback if the new prop is null.
                // NOTE: We must use the *unscaled* size from oldFont for this to work.
                // oldFont.Size is already scaled, so we divide by the scale factor to get back to the base size.
                var baseSize = newProps.FontSize ?? (oldFont.Size / (GetDpiScaleFactor(label)));

                // 3. Apply the scaling factor to the base font size.
                var scaledSize = baseSize * scaleFactor;

                // 4. Create the new font with the correctly SCALED size.
                label.Font = new System.Drawing.Font(family, scaledSize);

                // 5. IMPORTANT: Dispose the old font to prevent resource leaks. You already did this correctly!
                oldFont.Dispose();
            }

            if (oldProps?.MouseThrough != newProps.MouseThrough)
            {
                label.MouseThrough = newProps.MouseThrough;
            }
        }

        #endregion

        #region Helper Methods
        private float GetDpiScaleFactor(Control control)
        {
            // The base DPI in Windows is 96.
            // DeviceDpi gives the current DPI of the monitor the control is on.
            // This requires HighDpiMode.PerMonitorV2, which you've already set.
            return control.DeviceDpi / 96.0f;
        }

        private void UpdateSize(Control control, ContainerProps? oldProps, ContainerProps newProps)
        {
            float scaleFactor = GetDpiScaleFactor(control);

            // Helper to calculate a pixel value from a Dimension struct.
            // It now correctly handles the case where a parent is not yet available for percentage calculations.
            int GetPixelValue(Dimension? dim, int? parentDimensionSize)
            {
                if (dim == null) return -1; // Sentinel for "not specified"

                switch (dim.Value.Unit)
                {
                    case DimensionUnit.Pixels:
                        // Apply the scaling factor here!
                        return (int)(dim.Value.Value * scaleFactor);
                    case DimensionUnit.Percent:
                        return parentDimensionSize.HasValue
                            ? (int)(parentDimensionSize.Value * dim.Value.Value / 100f)
                            : -1; // Cannot calculate percent without a parent
                    default: // Includes Auto
                        return -1;
                }
            }

            bool isAutoSize = newProps.Width?.Unit == DimensionUnit.Auto || newProps.Height?.Unit == DimensionUnit.Auto;
            if (control.AutoSize != isAutoSize)
            {
                control.AutoSize = isAutoSize;
            }

            var parent = control.Parent;
            int? parentWidth = parent?.ClientSize.Width;
            int? parentHeight = parent?.ClientSize.Height;

            int w = GetPixelValue(newProps.Width, parentWidth);
            if (w != -1 && control.Width != w)
            {
                control.Width = w;
            }

            int h = GetPixelValue(newProps.Height, parentHeight);
            if (h != -1 && control.Height != h)
            {
                control.Height = h;
            }

            int minW = GetPixelValue(newProps.MinWidth, parentWidth);
            int minH = GetPixelValue(newProps.MinHeight, parentHeight);
            var newMinSize = new System.Drawing.Size(minW != -1 ? minW : 0, minH != -1 ? minH : 0);
            if (control.MinimumSize != newMinSize)
            {
                control.MinimumSize = newMinSize;
            }

            int maxW = GetPixelValue(newProps.MaxWidth, parentWidth);
            int maxH = GetPixelValue(newProps.MaxHeight, parentHeight);
            var newMaxSize = new System.Drawing.Size(maxW != -1 ? maxW : 0, maxH != -1 ? maxH : 0);
            if (control.MaximumSize != newMaxSize)
            {
                control.MaximumSize = newMaxSize;
            }
        }

        private void UpdateSpacing(Control control, Spacing? oldSpacing, Spacing? newSpacing, bool isMargin)
        {
            if (oldSpacing == newSpacing) return;

            float scaleFactor = GetDpiScaleFactor(control);
            var s = newSpacing ?? default;
            var padding = new System.Windows.Forms.Padding(
                (int)(s.Left.Value * scaleFactor),
                (int)(s.Top.Value * scaleFactor),
                (int)(s.Right.Value * scaleFactor),
                (int)(s.Bottom.Value * scaleFactor)
            );

            if (isMargin)
            {
                if (control.Margin != padding) control.Margin = padding;
            }
            else
            {
                if (control.Padding != padding) control.Padding = padding;
            }
        }

        private void UpdateEventHandler<T>(Control control, string eventName, T? oldAction, T newAction, Func<T, Delegate> converter) where T : Delegate
        {
            if (EqualityComparer<T>.Default.Equals(oldAction, newAction)) return;

            if (!_eventHandlers.ContainsKey(control))
            {
                _eventHandlers[control] = new Dictionary<string, Delegate>();
            }

            var ev = control.GetType().GetEvent(eventName);
            if (ev == null) return;

            // Unsubscribe old handler if it exists
            if (oldAction != null && _eventHandlers[control].TryGetValue(eventName, out var oldHandlerDelegate))
            {
                ev.RemoveEventHandler(control, oldHandlerDelegate);
                _eventHandlers[control].Remove(eventName);
            }

            // Subscribe new handler if it exists
            if (newAction != null)
            {
                var newHandler = converter(newAction);
                ev.AddEventHandler(control, newHandler);
                _eventHandlers[control][eventName] = newHandler;
            }
        }

        #endregion
    }

    /// <summary>
    /// A custom panel that supports drawing advanced borders and laying out children
    /// in a flexbox-like manner.
    /// </summary>
    public class LayoutablePanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public System.Drawing.Color UIBorderColor { get; set; } = System.Drawing.Color.Black;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float UIBorderWidth { get; set; } = 0f;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float UIBorderRadius { get; set; } = 0f;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Core.BorderStyle UIBorderStyle { get; set; } = Core.BorderStyle.None;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public Core.LayoutDirection UIDirection { get; set; } = Core.LayoutDirection.Vertical;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Core.JustifyContent UIJustifyContent { get; set; } = Core.JustifyContent.Start;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Core.AlignItems UIAlignItems { get; set; } = Core.AlignItems.Start;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float UIGap { get; set; } = 0f;

        public LayoutablePanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.Selectable, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (UIBorderStyle != Core.BorderStyle.None && UIBorderWidth > 0)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var pen = new Pen(UIBorderColor, UIBorderWidth);
                switch (UIBorderStyle)
                {
                    case Core.BorderStyle.Dashed:
                        pen.DashStyle = DashStyle.Dash;
                        break;
                    case Core.BorderStyle.Dotted:
                        pen.DashStyle = DashStyle.Dot;
                        break;
                }

                if (UIBorderRadius > 0)
                {
                    using var path = GetRoundedRectPath(ClientRectangle, UIBorderRadius, UIBorderWidth);
                    e.Graphics.DrawPath(pen, path);
                }
                else
                {
                    float halfWidth = UIBorderWidth / 2;
                    e.Graphics.DrawRectangle(pen, halfWidth, halfWidth, this.ClientSize.Width - UIBorderWidth, this.ClientSize.Height - UIBorderWidth);
                }
            }
        }

        private static GraphicsPath GetRoundedRectPath(System.Drawing.RectangleF rect, float radius, float borderWidth)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            float innerOffset = borderWidth;
            System.Drawing.RectangleF innerRect = new System.Drawing.RectangleF(rect.X + innerOffset / 2, rect.Y + innerOffset / 2, rect.Width - innerOffset, rect.Height - innerOffset);

            if (d > innerRect.Width) d = innerRect.Width;
            if (d > innerRect.Height) d = innerRect.Height;

            path.AddArc(innerRect.X, innerRect.Y, d, d, 180, 90);
            path.AddArc(innerRect.Right - d, innerRect.Y, d, d, 270, 90);
            path.AddArc(innerRect.Right - d, innerRect.Bottom - d, d, d, 0, 90);
            path.AddArc(innerRect.X, innerRect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnLayout(LayoutEventArgs le)
        {
            base.OnLayout(le);

            var children = this.Controls.Cast<Control>().Where(c => c.Visible).ToList();
            if (children.Count == 0) return;

            bool isVertical = UIDirection == Core.LayoutDirection.Vertical;
            var clientRect = this.ClientRectangle;
            var availableSize = clientRect.Size - this.Padding.Size;
            float totalGap = Math.Max(0, children.Count - 1) * UIGap;

            if (UIJustifyContent == Core.JustifyContent.Stretch)
            {
                float mainAxisTotalSize = isVertical ? availableSize.Height : availableSize.Width;
                float totalMargins = children.Sum(c => isVertical ? c.Margin.Vertical : c.Margin.Horizontal);
                float spaceForChildrenSizing = mainAxisTotalSize - totalGap - totalMargins;

                if (spaceForChildrenSizing > 0)
                {
                    float sizePerChild = spaceForChildrenSizing / children.Count;
                    foreach (var child in children)
                    {
                        if (isVertical)
                        {
                            child.Height = (int)sizePerChild;
                        }
                        else
                        {
                            child.Width = (int)sizePerChild;
                        }
                    }
                }
            }

            float totalChildrenSize = children.Sum(c => isVertical ? c.Height + c.Margin.Vertical : c.Width + c.Margin.Horizontal);
            float remainingSpace = (isVertical ? availableSize.Height : availableSize.Width) - totalChildrenSize - totalGap;

            float currentPos = isVertical ? this.Padding.Top : this.Padding.Left;
            float spacing = 0;

            switch (UIJustifyContent)
            {
                // After sizing, Stretch behaves like Start for positioning
                case Core.JustifyContent.Stretch:
                case Core.JustifyContent.Start:
                    break;
                case Core.JustifyContent.Center:
                    currentPos += remainingSpace / 2;
                    break;
                case Core.JustifyContent.End:
                    currentPos += remainingSpace;
                    break;
                case Core.JustifyContent.SpaceBetween:
                    if (children.Count > 1) spacing = remainingSpace / (children.Count - 1);
                    break;
                case Core.JustifyContent.SpaceAround:
                    if (children.Count > 0)
                    {
                        spacing = remainingSpace / children.Count;
                        currentPos += spacing / 2;
                    }
                    break;
            }

            foreach (var child in children)
            {
                // Main Axis Positioning
                if (isVertical)
                {
                    currentPos += child.Margin.Top;
                    child.Top = (int)currentPos;
                    currentPos += child.Height + child.Margin.Bottom + UIGap + spacing;
                }
                else
                {
                    currentPos += child.Margin.Left;
                    child.Left = (int)currentPos;
                    currentPos += child.Width + child.Margin.Right + UIGap + spacing;
                }

                // Cross Axis Alignment
                float crossSize = isVertical ? availableSize.Width : availableSize.Height;
                float childCrossSize = isVertical ? child.Width + child.Margin.Horizontal : child.Height + child.Margin.Vertical;
                float crossPos = isVertical ? this.Padding.Left : this.Padding.Top;

                switch (UIAlignItems)
                {
                    case Core.AlignItems.Start:
                        break;
                    case Core.AlignItems.Center:
                        crossPos += (crossSize - childCrossSize) / 2;
                        break;
                    case Core.AlignItems.End:
                        crossPos += crossSize - childCrossSize;
                        break;
                    case Core.AlignItems.Stretch:
                        if (isVertical) child.Width = (int)crossSize - child.Margin.Horizontal;
                        else child.Height = (int)crossSize - child.Margin.Vertical;
                        break;
                }

                if (isVertical)
                {
                    child.Left = (int)crossPos + child.Margin.Left;
                }
                else
                {
                    child.Top = (int)crossPos + child.Margin.Top;
                }
            }
        }
    }

    public class ClickThroughLabel : Label
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTTRANSPARENT = -1;

        /// <summary>
        /// Gets or sets a value indicating whether mouse events should pass through this label.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        /// <summary>
        /// Gets or sets a value indicating whether mouse events should pass through this label.
        /// </summary>
        public bool MouseThrough { get; set; } = false;

        public ClickThroughLabel()
        {
            this.AutoSize = true;
            this.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        }

        protected override void WndProc(ref Message m)
        {
            // 如果启用了穿透，并且收到了鼠标命中测试消息
            if (MouseThrough && m.Msg == WM_NCHITTEST)
            {
                // 将消息结果设置为“透明”，然后直接返回
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }

            // 否则，按正常方式处理消息
            base.WndProc(ref m);
        }
    }

    /// <summary>
    /// Schedules updates on the WinForms UI thread.
    /// </summary>
    public class WinformsUpdateScheduler : IUpdateScheduler
    {
        private readonly Control _control;

        public WinformsUpdateScheduler(Control control)
        {
            _control = control;
        }

        public void Schedule(Func<Task> updateAction)
        {
            // Use BeginInvoke to run the action on the UI thread without blocking.
            _control.BeginInvoke(new Action(async () => await updateAction()));
        }
    }

    /// <summary>
    /// Extension method to convert EchoUI.Core.Color to System.Drawing.Color.
    /// </summary>
    public static class ColorExtensions
    {
        public static System.Drawing.Color ToDrawingColor(this EchoUI.Core.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}