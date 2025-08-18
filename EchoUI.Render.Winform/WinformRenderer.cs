using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Color = EchoUI.Core.Color;
using Point = EchoUI.Core.Point;

namespace EchoUI.Render.Winform
{
    /// <summary>
    /// Implements the IRenderer interface for Windows Forms.
    /// Manages and manipulates native WinForms controls.
    /// </summary>
    public class WinformRenderer : IRenderer
    {
        private readonly Control _rootContainer;
        private readonly Dictionary<object, Control> _elements = new();
        private readonly Dictionary<object, Props> _elementProps = new();
        private readonly Dictionary<(object, string), Delegate> _eventHandlers = new();
        private readonly HashSet<(object, string)> _attachedEvents = new();

        public WinformRenderer(Control rootContainer)
        {
            _rootContainer = rootContainer ?? throw new ArgumentNullException(nameof(rootContainer));
            // Ensure the root container can host freely positioned controls
            if (_rootContainer is Form form)
            {
                form.AutoScaleMode = AutoScaleMode.None;
            }
        }

        private void InvokeOnUI(Action action)
        {
            if (_rootContainer.InvokeRequired)
            {
                _rootContainer.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public object CreateElement(string type)
        {
            var elementId = $"eui-{Guid.NewGuid()}";
            Control? control = null;

            InvokeOnUI(() =>
            {
                control = ToControl(type);
                control.Tag = elementId; // Store ID for reverse lookup
                _elements[elementId] = control;
            });

            return elementId;
        }

        private Control ToControl(string type)
        {
            return type switch
            {
                ElementCoreName.Container => new Panel { BackColor = System.Drawing.Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty },
                ElementCoreName.Text => new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, BackColor = System.Drawing.Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty },
                ElementCoreName.Input => new TextBox { BorderStyle = System.Windows.Forms.BorderStyle.None, Margin = Padding.Empty },
                _ => new Panel() // Default to Panel for unknown types
            };
        }

        public void AddChild(object parent, object child, int index)
        {
            InvokeOnUI(() =>
            {
                var parentControl = (parent as string) != null ? _elements[parent] : _rootContainer;
                var childControl = _elements[child];

                parentControl.Controls.Add(childControl);
                parentControl.Controls.SetChildIndex(childControl, index);

                // If the parent is a container, re-apply layout
                if (parentControl is Panel)
                {
                    ApplyFlexboxLayout(parentControl);
                }
            });
        }

        public void RemoveChild(object parent, object child)
        {
            InvokeOnUI(() =>
            {
                var parentControl = (parent as string) != null ? _elements[parent] : _rootContainer;
                var childControl = _elements[child];

                parentControl.Controls.Remove(childControl);

                // If the parent is a container, re-apply layout
                if (parentControl is Panel)
                {
                    ApplyFlexboxLayout(parentControl);
                }
            });
        }

        public void MoveChild(object parent, object child, int newIndex)
        {
            InvokeOnUI(() =>
            {
                var parentControl = (parent as string) != null ? _elements[parent] : _rootContainer;
                var childControl = _elements[child];

                parentControl.Controls.SetChildIndex(childControl, newIndex);

                // If the parent is a container, re-apply layout
                if (parentControl is Panel)
                {
                    ApplyFlexboxLayout(parentControl);
                }
            });
        }

        public void PatchProperties(object nativeElement, Props newProps, PropertyPatch patch)
        {
            InvokeOnUI(() =>
            {
                if (!_elements.TryGetValue(nativeElement, out var control)) return;

                _elementProps[nativeElement] = newProps;

                // [!重要!] 始终直接从 newProps 更新 C# 端的事件处理器引用，
                // 确保即使 Reconciler 没有报告变更，我们也能拿到最新的委托实例。
                UpdateEventHandlers(nativeElement, newProps);

                // 处理由 Reconciler 确认需要更新的属性
                if (patch.UpdatedProperties != null)
                {
                    foreach (var (propName, propValue) in patch.UpdatedProperties)
                    {
                        TranslatePropertyToControl(control, newProps, propName, propValue);
                    }
                }

                // [!重要!] 为了保持不同平台的一致性，为不同类型的元素应用默认样式
                // In WinForms, these are best set once at creation or managed via properties.
                // The ToControl method handles initial defaults.
                switch (newProps)
                {
                    case InputProps:
                        // These styles are effectively managed by setting Dock = Fill or similar
                        control.Dock = DockStyle.Fill;
                        break;
                }

                // If the control is a container and layout properties might have changed, re-apply layout.
                if (control.Parent is Panel)
                {
                    ApplyFlexboxLayout(control.Parent);
                }
            });
        }

        /// <summary>
        /// 将 Reconciler 传入的单个属性变更转换为具体的、可序列化的 DOM/CSS 变更。
        /// </summary>
        private void TranslatePropertyToControl(Control control, object props, string propName, object? propValue)
        {
            switch (props)
            {
                case ContainerProps:
                    switch (propName)
                    {
                        // --- Layout ---
                        case nameof(ContainerProps.Width): control.Width = (int)ToPixels(propValue as Dimension?); break;
                        case nameof(ContainerProps.Height): control.Height = (int)ToPixels(propValue as Dimension?); break;
                        case nameof(ContainerProps.MinWidth): control.MinimumSize = new Size((int)ToPixels(propValue as Dimension?), control.MinimumSize.Height); break;
                        case nameof(ContainerProps.MinHeight): control.MinimumSize = new Size(control.MinimumSize.Width, (int)ToPixels(propValue as Dimension?)); break;
                        case nameof(ContainerProps.MaxWidth): control.MaximumSize = new Size((int)ToPixels(propValue as Dimension?, 10000), control.MaximumSize.Height); break;
                        case nameof(ContainerProps.MaxHeight): control.MaximumSize = new Size(control.MaximumSize.Width, (int)ToPixels(propValue as Dimension?, 10000)); break;
                        case nameof(ContainerProps.Margin): control.Margin = ToWinformsPadding(propValue as Spacing?); break;
                        case nameof(ContainerProps.Padding): control.Padding = ToWinformsPadding(propValue as Spacing?); break;

                        // --- Flexbox --- (Handled by ApplyFlexboxLayout)
                        case nameof(ContainerProps.Direction):
                        case nameof(ContainerProps.JustifyContent):
                        case nameof(ContainerProps.AlignItems):
                        case nameof(ContainerProps.Gap):
                            ApplyFlexboxLayout(control);
                            break;

                        // --- Appearance ---
                        case nameof(ContainerProps.BackgroundColor): control.BackColor = ToWinformsColor(propValue as Color?); break;
                        case nameof(ContainerProps.BorderStyle):
                            if (control is Panel p) p.BorderStyle = ToWinformsBorderStyle(propValue as Core.BorderStyle?);
                            break;
                        // BorderColor, BorderWidth, BorderRadius would require custom drawing via the Paint event, which is more advanced.

                        // --- Events ---
                        case nameof(ContainerProps.OnClick): ManageEventSubscription<EventHandler>(control, "Click", propValue, (c, h) => c.Click += h, (c, h) => c.Click -= h, HandleClick); break;
                        case nameof(ContainerProps.OnMouseMove): ManageEventSubscription<MouseEventHandler>(control, "MouseMove", propValue, (c, h) => c.MouseMove += h, (c, h) => c.MouseMove -= h, HandleMouseMove); break;
                        case nameof(ContainerProps.OnMouseDown): ManageEventSubscription<MouseEventHandler>(control, "MouseDown", propValue, (c, h) => c.MouseDown += h, (c, h) => c.MouseDown -= h, HandleMouseDown); break;
                        case nameof(ContainerProps.OnMouseUp): ManageEventSubscription<MouseEventHandler>(control, "MouseUp", propValue, (c, h) => c.MouseUp += h, (c, h) => c.MouseUp -= h, HandleMouseUp); break;
                        case nameof(ContainerProps.OnMouseEnter): ManageEventSubscription<EventHandler>(control, "MouseEnter", propValue, (c, h) => c.MouseEnter += h, (c, h) => c.MouseEnter -= h, HandleGenericEvent); break;
                        case nameof(ContainerProps.OnMouseLeave): ManageEventSubscription<EventHandler>(control, "MouseLeave", propValue, (c, h) => c.MouseLeave += h, (c, h) => c.MouseLeave -= h, HandleGenericEvent); break;
                        case nameof(ContainerProps.OnKeyDown): ManageEventSubscription<KeyEventHandler>(control, "KeyDown", propValue, (c, h) => c.KeyDown += h, (c, h) => c.KeyDown -= h, HandleKey); break;
                        case nameof(ContainerProps.OnKeyUp): ManageEventSubscription<KeyEventHandler>(control, "KeyUp", propValue, (c, h) => c.KeyUp += h, (c, h) => c.KeyUp -= h, HandleKey); break;
                    }
                    break;

                case TextProps:
                    var label = (Label)control;
                    switch (propName)
                    {
                        // --- Text ---
                        case nameof(TextProps.Text): label.Text = propValue as string; break;
                        case nameof(TextProps.FontFamily): label.Font = new Font(propValue as string ?? label.Font.FontFamily.Name, label.Font.Size, label.Font.Style); break;
                        case nameof(TextProps.FontSize): label.Font = new Font(label.Font.FontFamily, (float?)propValue ?? label.Font.Size, label.Font.Style); break;
                        case nameof(TextProps.Color): label.ForeColor = ToWinformsColor(propValue as Color?); break;
                        case nameof(TextProps.MouseThrough): control.Enabled = !((bool?)propValue == true); break;
                    }
                    break;

                case InputProps:
                    var textBox = (TextBox)control;
                    switch (propName)
                    {
                        // --- Input ---
                        case nameof(InputProps.Value): textBox.Text = propValue as string; break;
                        case nameof(InputProps.OnValueChanged): ManageEventSubscription<EventHandler>(control, "TextChanged", propValue, (c, h) => c.TextChanged += h, (c, h) => c.TextChanged -= h, HandleTextChanged); break;
                    }
                    break;
            }
        }

        #region Converters
        private float ToPixels(Dimension? dim, float defaultValue = 0) => dim.HasValue && dim.Value.Unit == DimensionUnit.Pixels ? dim.Value.Value : defaultValue;
        private System.Drawing.Color ToWinformsColor(Color? color) => color.HasValue ? System.Drawing.Color.FromArgb(color.Value.A, color.Value.R, color.Value.G, color.Value.B) : System.Drawing.Color.Transparent;
        private Padding ToWinformsPadding(Spacing? spacing)
        {
            if (!spacing.HasValue) return Padding.Empty;
            return new Padding(
                (int)ToPixels(spacing.Value.Left),
                (int)ToPixels(spacing.Value.Top),
                (int)ToPixels(spacing.Value.Right),
                (int)ToPixels(spacing.Value.Bottom)
            );
        }
        private System.Windows.Forms.BorderStyle ToWinformsBorderStyle(Core.BorderStyle? style) => style switch
        {
            EchoUI.Core.BorderStyle.Solid => System.Windows.Forms.BorderStyle.FixedSingle,
            EchoUI.Core.BorderStyle.None => System.Windows.Forms.BorderStyle.None,
            _ => System.Windows.Forms.BorderStyle.None
        };
        #endregion

        #region Event Handling
        /// <summary>
        /// 始终同步 C# 端的事件处理器字典，确保回调使用最新的委托实例。
        /// </summary>
        private void UpdateEventHandlers(object elementId, Props newProps)
        {
            if (newProps is ContainerProps p)
            {
                UpdateHandler(elementId, "Click", p.OnClick);
                UpdateHandler(elementId, "MouseMove", p.OnMouseMove);
                UpdateHandler(elementId, "MouseDown", p.OnMouseDown);
                UpdateHandler(elementId, "MouseUp", p.OnMouseUp);
                UpdateHandler(elementId, "MouseEnter", p.OnMouseEnter);
                UpdateHandler(elementId, "MouseLeave", p.OnMouseLeave);
                UpdateHandler(elementId, "KeyDown", p.OnKeyDown);
                UpdateHandler(elementId, "KeyUp", p.OnKeyUp);
            }
            else if (newProps is InputProps ip)
            {
                UpdateHandler(elementId, "TextChanged", ip.OnValueChanged);
            }
        }

        private void UpdateHandler(object elementId, string eventName, Delegate? handler)
        {
            var key = (elementId, eventName);
            if (handler != null)
            {
                _eventHandlers[key] = handler;
            }
            else
            {
                _eventHandlers.Remove(key);
            }
        }

        private void ManageEventSubscription<TEventArgs>(Control control, string eventName, object? handler, Action<Control, EventHandler<TEventArgs>> add, Action<Control, EventHandler<TEventArgs>> remove, EventHandler<TEventArgs> staticHandler) where TEventArgs : EventArgs
        {
            var key = (control.Tag, eventName);
            bool shouldBeAttached = handler != null;
            bool isAttached = _attachedEvents.Contains(key);

            if (shouldBeAttached && !isAttached)
            {
                add(control, staticHandler);
                _attachedEvents.Add(key);
            }
            else if (!shouldBeAttached && isAttached)
            {
                remove(control, staticHandler);
                _attachedEvents.Remove(key);
            }
        }

        // Overload for simple EventHandler
        private void ManageEventSubscription<T>(Control control, string eventName, object? handler, Action<Control, T> add, Action<Control, T> remove, T staticHandler)
        {
            var key = (control.Tag, eventName);
            bool shouldBeAttached = handler != null;
            bool isAttached = _attachedEvents.Contains(key);

            if (shouldBeAttached && !isAttached)
            {
                add(control, staticHandler);
                _attachedEvents.Add(key);
            }
            else if (!shouldBeAttached && isAttached)
            {
                remove(control, staticHandler);
                _attachedEvents.Remove(key);
            }
        }

        private void HandleGenericEvent(object sender, EventArgs e)
        {
            var control = (Control)sender;
            var eventName = e.GetType().Name.Replace("EventArgs", "");
            if (_eventHandlers.TryGetValue((control.Tag, eventName), out var handler) && handler is Action action)
            {
                action.Invoke();
            }
        }

        private void HandleClick(object sender, EventArgs e)
        {
            var control = (Control)sender;
            if (_eventHandlers.TryGetValue((control.Tag, "Click"), out var handler) && handler is Action action)
            {
                action.Invoke();
            }
        }

        private void HandleTextChanged(object sender, EventArgs e)
        {
            var control = (Control)sender;
            if (_eventHandlers.TryGetValue((control.Tag, "TextChanged"), out var handler) && handler is Action<string> action)
            {
                action.Invoke(control.Text);
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            if (_eventHandlers.TryGetValue((control.Tag, "MouseMove"), out var handler) && handler is Action<Point> action)
            {
                action.Invoke(new Point(e.X, e.Y));
            }
        }

        private void HandleMouseDown(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            if (_eventHandlers.TryGetValue((control.Tag, "MouseDown"), out var handler) && handler is Action<MouseButton> action)
            {
                action.Invoke(e.Button == MouseButtons.Left ? MouseButton.Left : e.Button == MouseButtons.Right ? MouseButton.Right : MouseButton.Middle);
            }
        }

        private void HandleMouseUp(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            if (_eventHandlers.TryGetValue((control.Tag, "MouseUp"), out var handler) && handler is Action<MouseButton> action)
            {
                action.Invoke(e.Button == MouseButtons.Left ? MouseButton.Left : e.Button == MouseButtons.Right ? MouseButton.Right : MouseButton.Middle);
            }
        }

        private void HandleKey(object sender, KeyEventArgs e)
        {
            var control = (Control)sender;
            var eventName = e.GetType().Name.Replace("EventArgs", "");
            if (_eventHandlers.TryGetValue((control.Tag, eventName), out var handler) && handler is Action<int> action)
            {
                action.Invoke(e.KeyValue);
            }
        }

        #endregion

        #region Flexbox Layout
        private void ApplyFlexboxLayout(Control parentControl)
        {
            if (!_elementProps.TryGetValue(parentControl.Tag, out var props) || props is not ContainerProps containerProps)
            {
                return;
            }

            var children = parentControl.Controls.Cast<Control>().ToList();
            if (children.Count == 0) return;

            bool isHorizontal = containerProps.Direction.GetValueOrDefault(LayoutDirection.Horizontal) == LayoutDirection.Horizontal;
            float gap = containerProps.Gap.GetValueOrDefault(0);

            // Calculate total size of children along the main axis
            float totalChildrenSize = children.Sum(c => isHorizontal ? c.Width + c.Margin.Horizontal : c.Height + c.Margin.Vertical);
            float totalGap = Math.Max(0, children.Count - 1) * gap;
            float totalContentSize = totalChildrenSize + totalGap;

            float freeSpace = (isHorizontal ? parentControl.ClientSize.Width : parentControl.ClientSize.Height) - totalContentSize;

            float startOffset = 0;
            switch (containerProps.JustifyContent.GetValueOrDefault(JustifyContent.Start))
            {
                case JustifyContent.Center:
                    startOffset = freeSpace / 2;
                    break;
                case JustifyContent.End:
                    startOffset = freeSpace;
                    break;
                case JustifyContent.SpaceAround:
                    startOffset = freeSpace / (children.Count * 2);
                    break;
                case JustifyContent.SpaceBetween:
                    startOffset = 0;
                    break;
            }

            float currentPos = startOffset;
            if (containerProps.JustifyContent == JustifyContent.SpaceAround)
            {
                currentPos = freeSpace / (children.Count + 1);
            }

            foreach (var child in children)
            {
                // Align on cross-axis
                float crossAxisPos = 0;
                switch (containerProps.AlignItems.GetValueOrDefault(AlignItems.Start))
                {
                    case AlignItems.Center:
                        crossAxisPos = ((isHorizontal ? parentControl.ClientSize.Height : parentControl.ClientSize.Width) - (isHorizontal ? child.Height : child.Width)) / 2.0f;
                        break;
                    case AlignItems.End:
                        crossAxisPos = (isHorizontal ? parentControl.ClientSize.Height : parentControl.ClientSize.Width) - (isHorizontal ? child.Height : child.Width);
                        break;
                }

                // Position on main-axis
                var locationX = isHorizontal ? (int)currentPos + child.Margin.Left : (int)crossAxisPos + child.Margin.Left;
                var locationY = isHorizontal ? (int)crossAxisPos + child.Margin.Top : (int)currentPos + child.Margin.Top;
                child.Location = new System.Drawing.Point(locationX, locationY);

                currentPos += (isHorizontal ? child.Width + child.Margin.Horizontal : child.Height + child.Margin.Vertical) + gap;

                if (containerProps.JustifyContent == JustifyContent.SpaceBetween && children.Count > 1)
                {
                    currentPos += freeSpace / (children.Count - 1);
                }
                else if (containerProps.JustifyContent == JustifyContent.SpaceAround)
                {
                    currentPos += freeSpace / (children.Count + 1);
                }
            }
        }
        #endregion

        public IUpdateScheduler GetScheduler(object rootContainer)
        {
            return new WinformUpdateScheduler((Control)rootContainer);
        }
    }

    /// <summary>
    /// A scheduler that ensures UI updates run on the correct thread for WinForms.
    /// </summary>
    public class WinformUpdateScheduler : IUpdateScheduler
    {
        private readonly Control _syncControl;

        public WinformUpdateScheduler(Control syncControl)
        {
            _syncControl = syncControl;
        }

        public void Schedule(Func<Task> updateAction)
        {
            if (_syncControl.IsDisposed || _syncControl.Disposing) return;

            try
            {
                if (_syncControl.InvokeRequired)
                {
                    _syncControl.BeginInvoke(new Action(() => updateAction()));
                }
                else
                {
                    _ = updateAction.Invoke();
                }
            }
            catch (ObjectDisposedException)
            {
                // The form was likely closed during the operation, can be ignored.
            }
        }
    }
}