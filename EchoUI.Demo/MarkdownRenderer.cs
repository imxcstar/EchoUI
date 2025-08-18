using EchoUI.Core;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

namespace EchoUI.Demo
{
    public record class MarkdownProps : Props
    {
        /// <summary>
        /// 要渲染的 Markdown 文本内容。
        /// </summary>
        public string Content { get; init; } = "";
    }

    // 接下来，我们将 MarkdownRenderer 组件和其辅助逻辑添加到 Elements 类中
    public partial class Elements
    {
        /// <summary>
        /// 一个将 Markdown 字符串渲染为 EchoUI 元素的组件。
        /// </summary>
        [Element(DefaultProperty = nameof(MarkdownProps.Content))]
        public static Element MarkdownRenderer(MarkdownProps props)
        {
            // 1. 使用 Markdig 解析 Markdown 文本，得到 AST 的根节点
            var document = Markdig.Markdown.Parse(props.Content ?? "");

            // 2. 从 AST 根节点开始，递归地将其转换为 EchoUI 元素
            return ConvertMarkdownObjectToElement(document);
        }

        /// <summary>
        /// 递归转换函数的核心，根据 Markdig AST 节点类型创建不同的 EchoUI 元素。
        /// </summary>
        private static Element ConvertMarkdownObjectToElement(MarkdownObject markdownObject)
        {
            return markdownObject switch
            {
                // 整个文档或列表项这类容器 -> 垂直布局的 Container
                MarkdownDocument doc => ToContainer(doc, LayoutDirection.Vertical, new Spacing(Dimension.Pixels(5))),
                ListItemBlock item => ToContainer(item, LayoutDirection.Vertical),

                // 标题 -> 带有特定字号和外边距的 Text 元素
                HeadingBlock heading => Text(new TextProps
                {
                    Text = ExtractTextFromInlines(heading.Inline),
                    FontSize = 24 - (heading.Level * 2), // H1=22, H2=20, etc.
                                                         // 在你的框架中似乎没有 FontWeight，否则这里会设置 Bold
                }),

                // 段落 -> 普通的 Text 元素
                ParagraphBlock paragraph => Container(new ContainerProps
                {
                    // 段落之间需要一些垂直间距
                    Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(8)),
                    Children = [
                        Text(new TextProps
                    {
                        Text = ExtractTextFromInlines(paragraph.Inline),
                        FontSize = 14,
                    })
                    ]
                }),

                // 列表 -> 带有左内边距的 Container
                ListBlock list => ToContainer(list, LayoutDirection.Vertical, new Spacing(Dimension.Pixels(20), Dimension.ZeroPixels, Dimension.ZeroPixels, Dimension.ZeroPixels), childrenPrefixer: (child, index) =>
                {
                    // 为每个列表项添加项目符号 (bullet)
                    var bullet = list.IsOrdered ? $"{index + 1}. " : "• ";
                    return Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Gap = 8,
                        Children =
                        [
                            Text(new TextProps { Text = bullet, FontSize = 14 }),
                        // 子元素本身是一个 Container，我们需要让它占据剩余空间
                        // 你的框架似乎没有 FlexGrow 这样的属性，所以我们暂时这样处理
                        child
                        ]
                    });
                }),

                // 引用块 -> 带有左边框和内边距的特殊 Container
                QuoteBlock quote => Container(new ContainerProps
                {
                    Margin = new Spacing(Dimension.Pixels(10)),
                    Padding = new Spacing(Dimension.Pixels(10)),
                    BorderColor = Color.LightGray,
                    BorderWidth = 2,
                    BorderStyle = BorderStyle.Solid,
                    Children = ToContainer(quote, LayoutDirection.Vertical).Props.Children
                }),

                // 代码块 -> 使用不同背景和等宽字体的 Container
                FencedCodeBlock code => Container(new ContainerProps
                {
                    BackgroundColor = Color.Gainsboro,
                    Padding = new Spacing(Dimension.Pixels(10)),
                    Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(8)),
                    BorderRadius = 4,
                    Children =
                    [
                        Text(new TextProps
                    {
                        Text = GetTextFromLeafBlock(code),
                        FontFamily = "Consolas", // 使用等宽字体
                        FontSize = 13
                    })
                    ]
                }),

                // 水平分割线 -> 一个有背景色的矮 Container
                ThematicBreakBlock => Container(new ContainerProps
                {
                    Height = Dimension.Pixels(1),
                    BackgroundColor = Color.LightGray,
                    Margin = new Spacing(Dimension.ZeroPixels, Dimension.Pixels(15)),
                }),

                // 对于无法识别的块，返回一个空元素
                _ => Empty()
            };
        }

        /// <summary>
        /// 辅助函数：将一个 ContainerBlock 转换为 EchoUI 的 Container 元素。
        /// </summary>
        private static Element ToContainer(
            ContainerBlock containerBlock,
            LayoutDirection direction,
            Spacing? margin = null,
            Func<Element, int, Element>? childrenPrefixer = null)
        {
            var children = new List<Element>();
            var index = 0;
            foreach (var child in containerBlock.Select(ConvertMarkdownObjectToElement))
            {
                if (child.Type.IsNative && child.Type.AsNativeType == "Container" && ((ContainerProps)child.Props).Children.Count == 0)
                {
                    continue; // 跳过空的占位符元素
                }

                children.Add(childrenPrefixer != null ? childrenPrefixer(child, index) : child);
                index++;
            }

            return Container(new ContainerProps
            {
                Direction = direction,
                Margin = margin,
                Children = children
            });
        }

        /// <summary>
        /// 辅助函数：从内联元素集合 (如段落、标题) 中提取纯文本。
        /// 这个实现比较简单，它会忽略粗体、斜体等格式，只提取文本内容。
        /// 一个更高级的实现会为粗体、斜体创建不同的 Text 元素。
        /// </summary>
        private static string ExtractTextFromInlines(ContainerInline? inlines)
        {
            if (inlines == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var inline in inlines)
            {
                if (inline is LiteralInline literal)
                {
                    sb.Append(literal.Content);
                }
                else if (inline is ContainerInline container)
                {
                    // 递归处理嵌套的内联元素 (例如 **粗*斜*体**)
                    sb.Append(ExtractTextFromInlines(container));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 辅助函数：从 LeafBlock (特别是代码块) 中提取原始文本。
        /// </summary>
        private static string GetTextFromLeafBlock(LeafBlock leafBlock)
        {
            var sb = new StringBuilder();
            if (leafBlock.Lines.Lines != null)
            {
                for (int i = 0; i < leafBlock.Lines.Count; i++)
                {
                    sb.AppendLine(leafBlock.Lines.Lines[i].ToString());
                }
            }
            return sb.ToString();
        }
    }
}
