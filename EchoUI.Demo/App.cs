namespace EchoUI.Demo;

using EchoUI.Core;
using Markdig;
using System.ComponentModel;
using System.Diagnostics;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

public static class Demo
{
    private const string SampleMarkdown = @"
# Welcome to EchoUI Markdown

This is a demonstration of rendering Markdown content directly within the EchoUI framework.

- Bullet points are supported.
- **Bold** and *italic* text are rendered as plain text in this basic version.

## Code Blocks

```csharp
public static Element App() {
    var (count, setCount, _) = Hooks.State(0);

    return Container(new ContainerProps {
        Children = [
            Text(new TextProps { Text = $""Counter: {count.Value}"" }),
            Button(new ButtonProps {
                Text = ""Click Me!"",
                OnClick = () => setCount(count.Value + 1)
            })
        ]
    });
}
";

    public static Element? Render(Props props)
    {
        var tabsContent = Memo(() => new List<Element>
        {
            Create(Counter, new()
            {
                Key = "Counter",
            }),
            Create(InputTest, new()
            {
                Key = "Input",
            }),
            Create(Markdown, new()
            {
                Key = "Markdown",
            }),
        }, []);

        return Container(
            Width: Dimension.Percent(100),
            Height: Dimension.Percent(100),
            Children: [
                Tabs(
                    Titles : ["Counter", "Input", "Markdown"],
                    Content : i => tabsContent[i]
                )
            ]
        );
    }

    public static Element? Markdown(Props props)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(20)),
            BackgroundColor = Color.White,
            Children =
            [
                // 直接使用 MarkdownRenderer 组件！
                EchoUI.Demo.Elements.MarkdownRenderer(new MarkdownProps
                {
                    Content = SampleMarkdown
                })
            ]
        });
    }

    public static Element? InputTest(Props props)
    {
        var (inputText, setInputText, _) = State("");

        return Container(
            Direction: LayoutDirection.Vertical,
            Children: [
                Text(inputText.Value),
                Container(
                    Width: Dimension.Percent(100),
                    Height: Dimension.Pixels(50),
                    BorderStyle: BorderStyle.Solid,
                    BorderWidth: 1,
                    BorderColor: Color.Black,
                    Children: [
                        Input(
                            OnValueChanged: v => setInputText(v)
                        )
                    ]    
                )
            ]
        );
    }

    public static Element? Counter(Props props)
    {
        var (count, setCount, updateCount) = State(0);
        return Container(
            Direction: LayoutDirection.Vertical,
            Children: [
                Text(
                    Text : $"count: {count.Value}",
                    Color : count.Value == 0 ? Color.Black : (count.Value < 0 ? Color.Red : Color.Green)
                ),
                Container(
                    Direction : LayoutDirection.Horizontal,
                    Padding :  new Spacing(Dimension.Pixels(5)),
                    Gap : 5,
                    Children : [
                        Container(
                            Height : Dimension.Pixels(30),
                            Width : Dimension.Pixels(30),
                            JustifyContent : JustifyContent.Center,
                            AlignItems : AlignItems.Center,
                            Children : [
                                Button(
                                    Text : "-",
                                    OnClick : _ => updateCount(v => v - 1)
                                ),
                            ]
                        ),
                        Container(
                            Height : Dimension.Pixels(30),
                            Width : Dimension.Pixels(30),
                            JustifyContent : JustifyContent.Center,
                            AlignItems : AlignItems.Center,
                            Children : [
                                Button(
                                    Text : "+",
                                    OnClick : _ => updateCount(v => v + 1)
                                ),
                            ]
                        ),
                        Container(
                            Height : Dimension.Pixels(30),
                            Width : Dimension.Pixels(80),
                            JustifyContent : JustifyContent.Center,
                            AlignItems : AlignItems.Center,
                            Children : [
                                Button(
                                    Text : "Reset",
                                    OnClick : _ => setCount(0)
                                ),
                            ]
                        )
                    ]
                ),
                Switch(),
                RadioGroup([
                    "aaa",
                    "bbb",
                    "ccc"
                ]),
                CheckBox("test"),
                ComboBox([
                    "test1",
                    "test2",
                    "test3"
                ]),
            ]
        );
    }
}
