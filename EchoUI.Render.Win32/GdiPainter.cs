using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// GDI+ 绘制引擎，遍历 Win32Element 树并绘制到 Graphics 上。
    /// 支持双缓冲、圆角矩形、文本渲染、裁剪。
    /// </summary>
    internal static class GdiPainter
    {
        /// <summary>
        /// 绘制整棵元素树
        /// </summary>
        /// <summary>
        /// 绘制整棵元素树
        /// </summary>
        public static void Paint(Graphics g, Win32Element root, IReadOnlyCollection<Win32Element>? floatingElements, float viewportWidth, float viewportHeight)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(System.Drawing.Color.White);

            var viewportRect = new RectangleF(0, 0, viewportWidth, viewportHeight);

            // 第一遍：绘制非 Float 元素
            PaintElement(g, root, viewportRect, floatingElements);

            // 第二遍：绘制 Float 元素（作为顶层覆盖）
            if (floatingElements != null)
            {
                foreach (var floatElem in floatingElements)
                {
                    // Float 元素通常不受父级 overflow 裁剪（除非我们想那样），
                    // 这里我们只裁剪到视口
                    PaintElement(g, floatElem, viewportRect, null);
                }
            }
        }

        private static void PaintElement(Graphics g, Win32Element element, RectangleF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            // 如果该元素在待会要画的 Float 列表里，这一遍先跳过
            if (skippedElements != null && skippedElements.Contains(element))
                return;

            var bounds = element.GetAbsoluteBounds();

            // 如果元素完全在裁剪区域外，跳过
            if (bounds.Right < clipRect.Left || bounds.Left > clipRect.Right ||
                bounds.Bottom < clipRect.Top || bounds.Top > clipRect.Bottom)
            {
                return;
            }

            switch (element.ElementType)
            {
                case ElementCoreName.Container:
                    PaintContainer(g, element, bounds, clipRect, skippedElements);
                    break;
                case ElementCoreName.Text:
                    PaintText(g, element, bounds, clipRect);
                    break;
                case ElementCoreName.Input:
                    // Input 由原生 Edit 控件绘制，这里只画边框/背景
                    PaintInputBackground(g, element, bounds);
                    break;
                default:
                    // 未知类型当作容器处理
                    PaintContainer(g, element, bounds, clipRect, skippedElements);
                    break;
            }
        }

        private static void PaintContainer(Graphics g, Win32Element element, RectangleF bounds, RectangleF clipRect, IReadOnlyCollection<Win32Element>? skippedElements)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // 绘制背景
            if (element.BackgroundColor.HasValue && element.BackgroundColor.Value.A > 0)
            {
                var color = ToGdiColor(element.BackgroundColor.Value);
                using var brush = new SolidBrush(color);

                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.FillPath(brush, path);
                }
                else
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            // 绘制边框
            if (element.BorderWidth > 0 && element.BorderStyle != Core.BorderStyle.None && element.BorderColor.HasValue)
            {
                var borderColor = ToGdiColor(element.BorderColor.Value);
                var dashStyle = element.BorderStyle switch
                {
                    Core.BorderStyle.Dashed => System.Drawing.Drawing2D.DashStyle.Dash,
                    Core.BorderStyle.Dotted => System.Drawing.Drawing2D.DashStyle.Dot,
                    _ => System.Drawing.Drawing2D.DashStyle.Solid
                };

                using var pen = new Pen(borderColor, element.BorderWidth) { DashStyle = dashStyle };

                if (element.BorderRadius > 0)
                {
                    using var path = CreateRoundedRect(bounds, element.BorderRadius);
                    g.DrawPath(pen, path);
                }
                else
                {
                    g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }

            // 裁剪子元素（如果 Overflow != Visible）
            var childClip = clipRect;
            GraphicsState? savedState = null;

            if (element.Overflow != Overflow.Visible)
            {
                savedState = g.Save();
                var clipRegion = new RectangleF(
                    Math.Max(bounds.X, clipRect.X),
                    Math.Max(bounds.Y, clipRect.Y),
                    Math.Min(bounds.Right, clipRect.Right) - Math.Max(bounds.X, clipRect.X),
                    Math.Min(bounds.Bottom, clipRect.Bottom) - Math.Max(bounds.Y, clipRect.Y));

                if (clipRegion.Width > 0 && clipRegion.Height > 0)
                {
                    g.SetClip(clipRegion);
                    childClip = clipRegion;
                }
                else
                {
                    g.Restore(savedState);
                    return; // 完全被裁剪
                }
            }

            // 绘制子元素
            foreach (var child in element.Children)
            {
                PaintElement(g, child, childClip, skippedElements);
            }

            if (savedState != null)
            {
                g.Restore(savedState);
            }

            // 绘制滚动条（如果需要）
            if (element.Overflow == Overflow.Auto || element.Overflow == Overflow.Scroll)
            {
                PaintScrollbar(g, element, bounds);
            }
        }

        private static void PaintText(Graphics g, Win32Element element, RectangleF bounds, RectangleF clipRect)
        {
            if (string.IsNullOrEmpty(element.Text)) return;

            var fontSize = element.FontSize > 0 ? element.FontSize : 14f;
            var fontStyle = FontStyle.Regular;
            if (element.FontWeight != null)
            {
                var weight = element.FontWeight.ToLower();
                if (weight is "bold" or "700" or "800" or "900")
                    fontStyle = FontStyle.Bold;
                else if (weight is "semibold" or "600" or "500")
                    fontStyle = FontStyle.Bold; // GDI+ 没有 SemiBold，用 Bold 近似
            }

            var fontFamily = element.FontFamily ?? "Segoe UI";
            using var font = new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);

            var color = element.TextColor.HasValue
                ? ToGdiColor(element.TextColor.Value)
                : System.Drawing.Color.Black;
            using var brush = new SolidBrush(color);

            var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.NoWrap
            };

            g.DrawString(element.Text, font, brush, bounds.Location, format);
        }

        private static void PaintInputBackground(Graphics g, Win32Element element, RectangleF bounds)
        {
            // Input 的背景和边框由渲染器绘制，内容由原生 Edit 控件绘制
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            if (element.BackgroundColor.HasValue)
            {
                using var brush = new SolidBrush(ToGdiColor(element.BackgroundColor.Value));
                g.FillRectangle(brush, bounds);
            }
        }

        private static void PaintScrollbar(Graphics g, Win32Element element, RectangleF bounds)
        {
            float contentHeight = FlexLayout.MeasureContentHeight(element,
                bounds.Width, bounds.Height);

            if (contentHeight <= element.LayoutHeight) return;

            float scrollbarWidth = 6;
            float trackHeight = bounds.Height;
            float thumbHeight = Math.Max(20, trackHeight * (element.LayoutHeight / contentHeight));
            float maxScroll = contentHeight - element.LayoutHeight;
            float thumbY = bounds.Y + (element.ScrollOffsetY / maxScroll) * (trackHeight - thumbHeight);

            // 滚动条轨道
            var trackRect = new RectangleF(
                bounds.Right - scrollbarWidth - 2, bounds.Y,
                scrollbarWidth, trackHeight);

            // 滚动条滑块
            var thumbRect = new RectangleF(
                trackRect.X, thumbY,
                scrollbarWidth, thumbHeight);

            using var thumbBrush = new SolidBrush(System.Drawing.Color.FromArgb(128, 128, 128, 128));
            using var thumbPath = CreateRoundedRect(thumbRect, scrollbarWidth / 2);
            g.FillPath(thumbBrush, thumbPath);
        }

        // --- 辅助方法 ---

        private static System.Drawing.Color ToGdiColor(Core.Color c)
        {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;

            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

            // 左上角
            path.AddArc(arc, 180, 90);
            // 右上角
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            // 右下角
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // 左下角
            arc.X = rect.X;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
