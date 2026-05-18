namespace EchoUI.Demo;

using EchoUI.Core;
using System.Text;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

/// <summary>
/// Unified Dashboard-style UI showcase with sidebar navigation.
/// All EchoUI demos accessible from a single left sidebar + right content layout.
/// </summary>
public static class Showcase
{
    // ──────────────── Nav items ────────────────
    private static readonly (string Label, string Icon, string Id, Func<Element> Content)[] Sections =
    [
        ("Dashboard",  "🏠", "dashboard",  DashboardPage),
        ("Components", "🧩", "components", ComponentsPage),
        ("Animation",  "✨", "animation",  AnimationPage),
        ("Layout",     "📐", "layout",     LayoutPage),
        ("Cards",      "🃏", "cards",      CardsPage),
        ("Counter",    "🔢", "counter",    CounterPage),
        //("Markdown",   "📝", "markdown",   MarkdownPage),
        ("Diagnostics", "🧪", "diagnostics", DiagnosticsPage),
    ];

    // ═══════════════════════════════════════════════════════
    //  Shell
    // ═══════════════════════════════════════════════════════

    public static Element? Render(Props props)
    {
        var (activeId, setActiveId, _) = State("dashboard");

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.ViewportHeight(100),
            Direction = LayoutDirection.Horizontal,
            BackgroundColor = C.Bg,
            Children =
            [
                Sidebar(activeId.Value, id => setActiveId(id)),
                MainContent(activeId.Value)
            ]
        });
    }

    private static Element Sidebar(string activeId, Action<string> navigate)
    {
        var items = new List<Element>();
        foreach (var s in Sections)
            items.Add(NavItem(s.Label, s.Icon, s.Id, s.Id == activeId, () => navigate(s.Id)));

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(240),
            Height = Dimension.Percent(100),
            FlexShrink = 0,
            BackgroundColor = C.SidebarBg,
            Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(16)),
            Shadow = new BoxShadow(C.ShadowInput, 0, 18),
            Direction = LayoutDirection.Vertical,
            Gap = 8,
            Children =
            [
                // Logo
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 10,
                    Padding = new Spacing(Dimension.Pixels(0), Dimension.Pixels(4)),
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(36), Height = Dimension.Pixels(36),
                            BackgroundColor = C.Accent, BorderRadius = 18,
                            Shadow = new BoxShadow(C.Shadow, 3),
                            JustifyContent = JustifyContent.Center, AlignItems = AlignItems.Center,
                            Children = [Text(new TextProps { Text = "🌿", Color = C.TextTitle, FontWeight = "800", FontSize = 18 })]
                        }),
                        Text(new TextProps { Text = "Echo Island", Color = C.TextTitle, FontSize = 20, FontWeight = "900" })
                    ]
                }),

                Container(new ContainerProps { Height = Dimension.Pixels(16) }),

                // Version tag
                Container(new ContainerProps
                {
                    Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(6)),
                    BackgroundColor = C.AccentBg,
                    BorderRadius = 99,
                    Shadow = new BoxShadow(C.ShadowInput, 2),
                    Children = [Text(new TextProps { Text = ".NET 9 · Win32 + Web", Color = C.Accent, FontSize = 11, FontWeight = "800" })]
                }),

                Container(new ContainerProps { Height = Dimension.Pixels(8) }),

                // Nav items
                .. items,

                // Spacer
                Container(new ContainerProps { FlexGrow = 1 }),

                // Footer
                Container(new ContainerProps
                {
                    Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(6)),
                    BorderRadius = 6,
                    Direction = LayoutDirection.Vertical,
                    Gap = 2,
                    Children =
                    [
                        Text(new TextProps { Text = "Animal Island Style", Color = C.TextMuted, FontSize = 11, FontWeight = "700" }),
                        Text(new TextProps { Text = "Warm · Round · Playful", Color = C.TextFaint, FontSize = 10 }),
                    ]
                })
            ]
        });
    }

    private static Element NavItem(string label, string icon, string id, bool active, Action onClick)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(14), Dimension.Pixels(10)),
            BackgroundColor = active ? C.NavActiveBg : C.NavInactiveBg,
            BorderRadius = 14,
            Shadow = active ? new BoxShadow(C.ShadowInput, 3) : BoxShadow.None,
            Cursor = "pointer",
            OnClick = _ => onClick(),
            Direction = LayoutDirection.Horizontal,
            Gap = 10,
            AlignItems = AlignItems.Center,
            Transitions = Trans((nameof(ContainerProps.BackgroundColor), 180, Easing.EaseOut)),
            Children =
            [
                Text(new TextProps { Text = icon, FontSize = 15 }),
                Text(new TextProps
                {
                    Text = label,
                    Color = active ? Color.White : C.TextMuted,
                    FontSize = 14,
                    FontWeight = active ? "800" : "600"
                })
            ]
        });
    }

    private static Element MainContent(string activeId)
    {
        var content = Array.Find(Sections, s => s.Id == activeId).Content
                      ?? Sections[0].Content;

        return Container(new ContainerProps
        {
            Key = $"main-content-{activeId}",
            FlexGrow = 1,
            FlexShrink = 1,
            Height = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(28)),
            Direction = LayoutDirection.Vertical,
            Overflow = Overflow.Auto,
            AlignItems = AlignItems.Stretch,
            // 每页包裹为独立子组件，避免 hook 跨页污染
            Children = [new Element((Component)(_ => content()), new Props { Key = activeId })]
        });
    }

    // ═══════════════════════════════════════════════════════
    //  Section helpers
    // ═══════════════════════════════════════════════════════

    private static Element SectionTitle(string text) =>
        Text(new TextProps { Text = text, FontSize = 28, Color = C.TextTitle, FontWeight = "900" });

    private static Element Subtitle(string text) =>
        Text(new TextProps { Text = text, Color = C.TextMuted, FontSize = 14, FontWeight = "600" });

    private static Element Card(string? title, IReadOnlyList<Element> children)
    {
        var list = new List<Element>();
        if (title != null)
            list.Add(Text(new TextProps { Text = title, FontSize = 17, Color = C.TextTitle, FontWeight = "800" }));
        list.AddRange(children);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = C.CardBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 2,
            BorderRadius = 20,
            Padding = new Spacing(Dimension.Pixels(24)),
            Shadow = new BoxShadow(C.ShadowInput, 4, 10),
            Direction = LayoutDirection.Vertical,
            Gap = 16,
            AlignItems = AlignItems.Stretch,
            Children = list
        });
    }

    private static Element CardRow(IReadOnlyList<Element> children) =>
        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 16, AlignItems = AlignItems.Center, Children = children });

    private static Element StatCard(string label, string value, Color color)
    {
        return Container(new ContainerProps
        {
            FlexGrow = 1, FlexShrink = 1,
            BackgroundColor = C.CardBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 2,
            BorderRadius = 18,
            Padding = new Spacing(Dimension.Pixels(20)),
            Shadow = new BoxShadow(C.ShadowInput, 3, 8),
            Direction = LayoutDirection.Vertical,
            Gap = 10,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    JustifyContent = JustifyContent.SpaceBetween,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Text(new TextProps { Text = label, Color = C.TextSecondary, FontSize = 13, FontWeight = "500" }),
                        Container(new ContainerProps { Width = Dimension.Pixels(8), Height = Dimension.Pixels(8), BorderRadius = 4, BackgroundColor = color })
                    ]
                }),
                Text(new TextProps { Text = value, Color = C.TextTitle, FontSize = 32, FontWeight = "900" }),
            ]
        });
    }

    private static Element Badge(string text, Color? bg = null, Color? fg = null) =>
        Container(new ContainerProps
        {
            Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(6)),
            BackgroundColor = bg ?? C.AccentBg,
            BorderRadius = 99,
            Shadow = new BoxShadow(C.ShadowInput.WithAlpha(120), 2),
            Children = [Text(new TextProps { Text = text, Color = fg ?? C.Accent, FontSize = 11, FontWeight = "700" })]
        });

    private static Element Tag(string text, Color bg, Color fg) =>
        Container(new ContainerProps
        {
            Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(6)),
            BackgroundColor = bg, BorderRadius = 99,
            Children = [Text(new TextProps { Text = text, Color = fg, FontSize = 11, FontWeight = "700" })]
        });

    private static Element NookTile(string emoji, string label, Color bg, Color fg) =>
        Container(new ContainerProps
        {
            Width = Dimension.Pixels(118),
            Height = Dimension.Pixels(118),
            BackgroundColor = bg,
            BorderRadius = 38,
            Shadow = new BoxShadow(C.Shadow, 5, 8),
            Direction = LayoutDirection.Vertical,
            Gap = 8,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Children =
            [
                Text(new TextProps { Text = emoji, FontSize = 30 }),
                Text(new TextProps { Text = label, Color = fg, FontSize = 12, FontWeight = "900" })
            ]
        });

    private static Element SoftPanel(IReadOnlyList<Element> children) =>
        Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = C.InputBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 2,
            BorderRadius = 24,
            Padding = new Spacing(Dimension.Pixels(18)),
            Shadow = new BoxShadow(C.ShadowInput, 3),
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            Children = children
        });

    private static ValueDictionary<string, Transition> Trans(params (string Prop, int Ms, Easing Easing)[] items)
    {
        var d = new Dictionary<string, Transition>();
        foreach (var (prop, ms, easing) in items)
            d[prop] = new Transition(ms, easing);
        return new ValueDictionary<string, Transition>(d);
    }

    // ═══════════════════════════════════════════════════════
    //  1. Dashboard Page
    // ═══════════════════════════════════════════════════════

    private sealed record UserModel(string Name, string Role, string Status);

    private static Element DashboardPage()
    {
        var (users, setUsers, _) = State(new List<UserModel>
        {
            new("Alice Johnson", "Administrator", "Active"),
            new("Bob Smith", "Editor", "Pending"),
            new("Charlie Brown", "Viewer", "Suspended"),
            new("Diana Prince", "Developer", "Active"),
        });

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 28,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical, Gap = 4,
                    Children =
                    [
                        SectionTitle("Dashboard"),
                        Subtitle("Welcome back, Administrator. Here's what's happening.")
                    ]
                }),

                // Stats row
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal, Gap = 16,
                    Children =
                    [
                        StatCard("Total Users", users.Value.Count.ToString("N0"), C.Accent),
                        StatCard("Active Sessions", "842", C.Success),
                        StatCard("Server Load", "24%", C.Warning),
                    ]
                }),

                // Form + User list
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal, Gap = 16, AlignItems = AlignItems.Stretch,
                    Children =
                    [
                        // Form
                        Container(new ContainerProps
                        {
                            FlexGrow = 1, FlexShrink = 1,
                            Children = [UserForm(newUser => setUsers([.. users.Value, newUser]))]
                        }),
                        // System status
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(240), FlexShrink = 0,
                            Children = [SystemStatusCard()]
                        })
                    ]
                }),

                // User list
                Card("Recent Users", users.Value.Select(u => UserRow(u)).ToList()),
            ]
        });
    }

    private static Element UserForm(Action<UserModel> onCreate)
    {
        var (name, setName, _) = State("");
        var (roleIdx, setRoleIdx, _) = State(0);
        var (statusIdx, setStatusIdx, _) = State(0);
        var roles = new[] { "Administrator", "Editor", "Viewer", "Guest" };
        var statuses = new[] { "Active", "Pending", "Suspended" };

        return Card("Add New User",
        [
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal, Gap = 16, AlignItems = AlignItems.Stretch,
                Children =
                [
                    Container(new ContainerProps { FlexGrow = 1, Direction = LayoutDirection.Vertical, Gap = 12, Children =
                    [
                        Text(new TextProps { Text = "Username", Color = C.TextSecondary, FontSize = 13, FontWeight = "600" }),
                        TextInput(new TextInputProps { Value = name.Value, OnValueChanged = v => setName(v), Width = Dimension.Percent(100) })
                    ]}),
                    Container(new ContainerProps { Width = Dimension.Pixels(180), Direction = LayoutDirection.Vertical, Gap = 12, Children =
                    [
                        Text(new TextProps { Text = "Role", Color = C.TextSecondary, FontSize = 13, FontWeight = "600" }),
                        ComboBox(new ComboBoxProps { Options = roles, SelectedIndex = roleIdx.Value, OnSelectionChanged = v => setRoleIdx(v) })
                    ]}),
                ]
            }),
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal, Gap = 14, AlignItems = AlignItems.Center,
                Children =
                [
                    Text(new TextProps { Text = "Status:", Color = C.TextSecondary, FontSize = 13, FontWeight = "600" }),
                    RadioGroup(new RadioGroupProps { Options = statuses, SelectedIndex = statusIdx.Value, OnSelectionChanged = v => setStatusIdx(v), Direction = LayoutDirection.Horizontal, SelectedColor = C.Accent }),
                ]
            }),
            Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Direction = LayoutDirection.Horizontal,
                JustifyContent = JustifyContent.End,
                Children =
                [
                    Button(new ButtonProps
                    {
                        Text = "Create User",
                        BackgroundColor = C.Accent, TextColor = C.TextTitle,
                        Width = Dimension.Pixels(130), Height = Dimension.Pixels(38),
                        BorderRadius = 6,
                        OnClick = _ =>
                        {
                            if (!string.IsNullOrWhiteSpace(name.Value))
                            {
                                onCreate(new UserModel(name.Value, roles[roleIdx.Value], statuses[statusIdx.Value]));
                                setName("");
                            }
                        }
                    })
                ]
            })
        ]);
    }

    private static Element SystemStatusCard()
    {
        var items = new[] {
            ("Database", "Online", C.Success),
            ("Redis Cache", "Online", C.Success),
            ("Email Service", "Degraded", C.Warning),
            ("Background Jobs", "Online", C.Success),
        };

        return Card("System Status", items.Select(i =>
        {
            var (name, status, color) = i;
            return (Element)Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                JustifyContent = JustifyContent.SpaceBetween,
                AlignItems = AlignItems.Center,
                Width = Dimension.Percent(100),
                Children =
                [
                    Text(new TextProps { Text = name, Color = C.TextSecondary, FontSize = 13 }),
                    Tag(status, color.WithAlpha(30), color)
                ]
            });
        }).ToList());
    }

    private static Element UserRow(UserModel u)
    {
        var (color, bg) = u.Status switch
        {
            "Active" => (C.Success, C.Success.WithAlpha(25)),
            "Pending" => (C.Warning, C.Warning.WithAlpha(25)),
            _ => (C.Error, C.Error.WithAlpha(25))
        };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Horizontal,
            JustifyContent = JustifyContent.SpaceBetween,
            AlignItems = AlignItems.Center,
            Padding = new Spacing(Dimension.Pixels(14), Dimension.Pixels(12)),
            BackgroundColor = C.InputBg,
            BorderRadius = 8,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 2, Children =
                [
                    Text(new TextProps { Text = u.Name, Color = C.TextTitle, FontSize = 14, FontWeight = "600" }),
                    Text(new TextProps { Text = u.Role, Color = C.TextSecondary, FontSize = 12 })
                ]}),
                Tag(u.Status, bg, color)
            ]
        });
    }

    // ═══════════════════════════════════════════════════════
    //  2. Components Page
    // ═══════════════════════════════════════════════════════

    private static Element ComponentsPage()
    {
        var (btnClicks, _, updateBtn) = State(0);
        var (inputVal, setInputVal, _) = State("");
        var (textVal, setTextVal, _) = State("");
        var (comboIdx, setComboIdx, _) = State(0);
        var (radioIdx, setRadioIdx, _) = State(0);
        var (switchOn, setSwitchOn, _) = State(false);
        var (checkOn, setCheckOn, _) = State(true);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Components"),
                    Subtitle("All built-in controls with live state — click, type, toggle to see reactivity.")
                ]}),

                // Buttons
                Card("Buttons",
                [
                    CardRow([
                        Button(new ButtonProps { Text = $"Click ({btnClicks.Value})", OnClick = _ => updateBtn(v => v + 1) }),
                        Button(new ButtonProps { Text = "Primary", BackgroundColor = C.Accent, TextColor = C.TextTitle }),
                        Button(new ButtonProps { Text = "Success", BackgroundColor = C.Success, TextColor = C.TextTitle }),
                        Button(new ButtonProps { Text = "Warning", BackgroundColor = C.Warning, TextColor = C.TextTitle }),
                        Button(new ButtonProps { Text = "Danger", BackgroundColor = C.Error, TextColor = C.TextTitle }),
                        Button(new ButtonProps { Text = "Ghost", BackgroundColor = C.InputBg, TextColor = C.TextBody }),
                    ]),
                    CardRow([
                        Button(new ButtonProps { Text = "Small", Height = Dimension.Pixels(30) }),
                        Button(new ButtonProps { Text = "Medium", Height = Dimension.Pixels(38) }),
                        Button(new ButtonProps { Text = "Large", Height = Dimension.Pixels(48) }),
                    ])
                ]),

                // Inputs
                Card("Input & TextInput",
                [
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 14, AlignItems = AlignItems.Center, Children =
                    [
                        Container(new ContainerProps { FlexGrow = 1, Children =
                        [
                            Input(new InputProps { Value = inputVal.Value, OnValueChanged = v => setInputVal(v), BackgroundColor = C.InputBg, TextColor = C.TextTitle, BorderColor = C.Border, FocusedBorderColor = C.Accent })
                        ]}),
                        Text(new TextProps { Text = $"{inputVal.Value.Length} chars", Color = C.TextSecondary, FontSize = 12 }),
                    ]}),
                    Container(new ContainerProps { Children =
                    [
                        TextInput(new TextInputProps { Value = textVal.Value, OnValueChanged = v => setTextVal(v), Width = Dimension.Percent(100), Placeholder = "Type something..." })
                    ]})
                ]),

                // Selection controls
                Card("Selection Controls",
                [
                    CardRow([
                        CheckBox(new CheckBoxProps { Label = "Remember me", IsChecked = checkOn.Value, OnToggle = v => setCheckOn(v), CheckColor = C.Accent, BorderColor = C.Border }),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, AlignItems = AlignItems.Center, Gap = 8, Children =
                        [
                            Switch(new SwitchProps { DefaultIsOn = switchOn.Value, OnToggle = v => setSwitchOn(v), OnColor = C.Success, OffColor = C.InputBg }),
                            Text(new TextProps { Text = "Notifications", Color = C.TextBody, FontWeight = "600", FontSize = 13 })
                        ]}),
                        Container(new ContainerProps { Width = Dimension.Pixels(180), Children =
                        [
                            ComboBox(new ComboBoxProps { Options = ["Red", "Green", "Blue", "Purple"], SelectedIndex = comboIdx.Value, OnSelectionChanged = v => setComboIdx(v), BackgroundColor = C.InputBg, TextColor = C.TextTitle, BorderColor = C.Border })
                        ]}),
                    ]),
                ]),

                // Radio group + Tabs
                Card("RadioGroup & Tabs",
                [
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 24, AlignItems = AlignItems.Start, Children =
                    [
                        Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 8, Children =
                        [
                            Text(new TextProps { Text = "RadioGroup (horizontal)", Color = C.TextSecondary, FontSize = 12, FontWeight = "700" }),
                            RadioGroup(new RadioGroupProps { Options = ["XS", "SM", "MD", "LG"], SelectedIndex = radioIdx.Value, OnSelectionChanged = v => setRadioIdx(v), Direction = LayoutDirection.Horizontal, SelectedColor = C.Accent, BorderColor = C.Border }),
                            Text(new TextProps { Text = $"Selected: {new[] { "XS", "SM", "MD", "LG" }[radioIdx.Value]}", Color = C.TextMuted, FontSize = 12, FontWeight = "600" })
                        ]}),
                        Container(new ContainerProps { FlexGrow = 1, Children =
                        [
                            Tabs(new TabProps
                            {
                                Titles = ["Overview", "Details", "Settings"],
                                Content = i => Container(new ContainerProps
                                {
                                    Height = Dimension.Pixels(108),
                                    BackgroundColor = i switch { 0 => C.AccentBg, 1 => C.Success.WithAlpha(35), _ => C.Warning.WithAlpha(55) },
                                    BorderRadius = 18,
                                    BorderColor = C.Border,
                                    BorderStyle = BorderStyle.Solid,
                                    BorderWidth = 2,
                                    JustifyContent = JustifyContent.Center,
                                    AlignItems = AlignItems.Center,
                                    Children = [Text(new TextProps { Text = i switch { 0 => "🌴 Overview panel", 1 => "✅ Details panel", _ => "⚙ Settings panel" }, Color = C.TextTitle, FontWeight = "900", FontSize = 18 })]
                                })
                            })
                        ]})
                    ]})
                ]),

                Card("More Animal Island Controls",
                [
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 18, Children =
                    [
                        NookTile("📷", "Camera", C.Purple, Color.White),
                        NookTile("🛍", "Market", Color.FromHex("#f8a6b2"), Color.White),
                        NookTile("🗺", "Map", Color.FromHex("#82d5bb"), Color.White),
                        NookTile("💬", "Chat", Color.FromHex("#d1da49"), C.TextTitle),
                    ]}),
                    SoftPanel([
                        Text(new TextProps { Text = "Compact settings cluster", Color = C.TextTitle, FontSize = 15, FontWeight = "800" }),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 14, AlignItems = AlignItems.Center, Children =
                        [
                            Badge(checkOn.Value ? "CHECKED" : "UNCHECKED", checkOn.Value ? C.AccentBg : C.InputBg, checkOn.Value ? C.Accent : C.TextMuted),
                            Badge(switchOn.Value ? "SYNC ON" : "SYNC OFF", switchOn.Value ? C.Success.WithAlpha(35) : C.InputBg, switchOn.Value ? C.Success : C.TextMuted),
                            Tag($"Combo: {new[] { "Red", "Green", "Blue", "Purple" }[comboIdx.Value]}", C.Warning.WithAlpha(55), C.TextTitle)
                        ]})
                    ])
                ]),
            ]
        });
    }

    // ═══════════════════════════════════════════════════════
    //  3. Animation Page
    // ═══════════════════════════════════════════════════════

    private const int AnimFast = 360;
    private const int AnimNormal = 720;
    private const int AnimSlow = 980;

    private static Element AnimationPage()
    {
        var (on, setOn, _) = State(false);
        Action toggle = () => setOn(!on.Value);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Animation"),
                    Subtitle("Click Play / Reverse to observe property, transform, and transform-origin transitions across all easing curves.")
                ]}),

                AnimationNotice(),

                // Hero card
                HeroCard(on.Value, toggle),

                // Easing theater
                EasingTheater(on.Value, toggle),

                // Transform scenes
                Card("Transform Lab",
                [
                    AnimationNote("说明：这些动画使用 Transform / TransformOrigin。修复后旋转、缩放、斜切都会围绕元素自身边界内的原点执行，不会再绕窗口原点飞出去。"),
                    SceneRow("Origin Dial", "Rotate around multiple TransformOrigin anchors", OriginDial(on.Value, toggle)),
                    SceneRow("Flip Tile", "Translate + Rotate + Scale + Origin", FlipTile(on.Value, toggle)),
                    SceneRow("Skew Ribbon", "Skew + color + shadow + translate", SkewRibbon(on.Value, toggle)),
                    SceneRow("Orbit Chips", "Independent transform chains", OrbitChips(on.Value, toggle)),
                ]),

                TransformGallery(on.Value, toggle),

                // Scene cards
                Card("Visual Scenes",
                [
                    SceneRow("Morph Card", "Color + Border + Radius + Padding + Size", MorphCard(on.Value, toggle)),
                    SceneRow("Slide Dock", "Margin + Gap displacement", SlideDock(on.Value, toggle)),
                    SceneRow("Accordion", "Height + MaxHeight + Padding", Accordion(on.Value, toggle)),
                    SceneRow("Constraint Panel", "MinWidth / MaxWidth / MinHeight / MaxHeight", ConstraintPanel(on.Value, toggle)),
                ]),

                // Supported properties
                SupportedPropertyTable(),
            ]
        });
    }

    private static Element AnimationNotice()
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = Color.FromHex("#f0e8d8"),
            BorderColor = Color.FromHex("#334155"),
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(16)),
            Direction = LayoutDirection.Vertical,
            Gap = 8,
            Children =
            [
                Text(new TextProps { Text = "Transform 原点说明", Color = C.TextTitle, FontSize = 15, FontWeight = "800" }),
                Text(new TextProps { Text = "TransformOrigin 表示元素自身边界内的锚点，例如 center、top-left、bottom-right；旋转/缩放/斜切都应该围绕这个局部锚点执行。", Color = C.TextSecondary, FontSize = 12, FontWeight = "500" }),
                Text(new TextProps { Text = "之前 Win32 GDI+ 矩阵把 +/- origin 顺序写反，导致元素绕窗口坐标系转动，看起来脱离自身位置乱飞；现在已修正为先移到元素原点、变换、再移回。", Color = Color.FromHex("#93C5FD"), FontSize = 12, FontWeight = "700" })
            ]
        });
    }

    private static Element AnimationNote(string text)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = Color.FromHex("#111C2F"),
            BorderRadius = 8,
            Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
            Children = [Text(new TextProps { Text = text, Color = Color.FromHex("#BFDBFE"), FontSize = 12, FontWeight = "600" })]
        });
    }

    private static Element InteractiveCard(string title, string note, IReadOnlyList<Element> children, Action toggle)
    {
        var list = new List<Element>
        {
            Text(new TextProps { Text = title, FontSize = 16, Color = C.TextTitle, FontWeight = "700" }),
            AnimationNote(note)
        };
        list.AddRange(children);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = C.CardBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 2,
            BorderRadius = 20,
            Padding = new Spacing(Dimension.Pixels(24)),
            Shadow = new BoxShadow(C.ShadowInput, 4, 10),
            Direction = LayoutDirection.Vertical,
            Gap = 16,
            AlignItems = AlignItems.Stretch,
            OnClick = _ => toggle(),
            Children = list
        });
    }

    private static Element HeroCard(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(on ? 200 : 150),
            BackgroundColor = on ? C.AccentBg : C.CardBg,
            BorderColor = on ? C.Accent : C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = on ? 2 : 1,
            BorderRadius = on ? 24 : 12,
            Padding = new Spacing(Dimension.Pixels(on ? 28 : 18)),
            Direction = LayoutDirection.Horizontal,
            JustifyContent = JustifyContent.SpaceBetween,
            AlignItems = AlignItems.Center,
            OnClick = _ => toggle(),
            Transitions = Trans(
                (nameof(ContainerProps.Height), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BackgroundColor), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BorderColor), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.Padding), AnimSlow, Easing.EaseInOut)),
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 10, FlexGrow = 1, Children =
                [
                    Text(new TextProps { Text = "EchoUI Motion", Color = C.TextTitle, FontSize = 26, FontWeight = "800" }),
                    Text(new TextProps { Text = "Click Play to animate color, size, spacing, and radius simultaneously.", Color = C.TextSecondary, FontSize = 14 }),
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 8, Children =
                    [
                        Badge("Color"), Badge("Spacing"), Badge("Size"), Badge("Radius"), Badge("Easing")
                    ]})
                ]}),
                Button(new ButtonProps
                {
                    Text = on ? "Reverse" : "Play",
                    Width = Dimension.Pixels(on ? 130 : 110),
                    Height = Dimension.Pixels(on ? 48 : 40),
                    BorderRadius = on ? 16 : 10,
                    BackgroundColor = on ? Color.FromHex("#7C3AED") : C.Accent,
                    TextColor = C.TextTitle,
                    OnClick = _ => toggle(),
                }),
                // Animated shape
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(on ? 120 : 80),
                    Height = Dimension.Pixels(on ? 80 : 120),
                    BackgroundColor = on ? Color.FromHex("#F97316") : Color.FromHex("#06B6D4"),
                    BorderColor = on ? Color.FromHex("#FDBA74") : Color.FromHex("#67E8F9"),
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = on ? 6 : 2,
                    BorderRadius = on ? 40 : 16,
                    Transform = MotionTransform(on ? 8 : -4, on ? -8 : 4, on ? 16 : -12, on ? 1.08f : 0.92f),
                    TransformOrigin = on ? TransformOrigin.Center : TransformOrigin.Center,
                    Transitions = Trans(
                        (nameof(ContainerProps.Width), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Height), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BackgroundColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderWidth), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut))
                })
            ]
        });
    }

    private static Element EasingTheater(bool on, Action toggle)
    {
        var easings = new[] { (Easing.Linear, Color.FromHex("#38BDF8")), (Easing.Ease, Color.FromHex("#A78BFA")), (Easing.EaseIn, Color.FromHex("#F97316")), (Easing.EaseOut, Color.FromHex("#22C55E")), (Easing.EaseInOut, Color.FromHex("#EC4899")) };

        return Card("Easing Theater", easings.Select(e =>
        {
            var (easing, color) = e;
            return (Element)Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Height = Dimension.Pixels(46),
                BackgroundColor = C.InputBg,
                BorderRadius = 8,
                Padding = new Spacing(Dimension.Pixels(12)),
                Direction = LayoutDirection.Horizontal,
                AlignItems = AlignItems.Center,
                Gap = 12,
                OnClick = _ => toggle(),
                Children =
                [
                    Container(new ContainerProps { Width = Dimension.Pixels(100), FlexShrink = 0, Children =
                    [
                        Text(new TextProps { Text = easing.ToString(), Color = C.TextTitle, FontWeight = "700", FontSize = 13 })
                    ]}),
                    Container(new ContainerProps { FlexGrow = 1, Height = Dimension.Pixels(24), BackgroundColor = C.Bg, BorderRadius = 12, Padding = new Spacing(Dimension.Pixels(3)), Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(on ? 340 : 40),
                            Height = Dimension.Pixels(18),
                            BackgroundColor = color,
                            BorderRadius = 9,
                            Transitions = Trans((nameof(ContainerProps.Width), AnimSlow, easing))
                        })
                    ]})
                ]
            });
        }).ToList());
    }

    private static Transform MotionTransform(float x, float y, float rotate, float scale) =>
        new Transform(
            new TranslateTransform(x, y),
            new RotateTransform(rotate),
            new ScaleTransform(scale, scale));

    private static Transform SkewMotionTransform(float x, float y, float rotate, float scale, float skewX, float skewY) =>
        new Transform(
            new TranslateTransform(x, y),
            new RotateTransform(rotate),
            new ScaleTransform(scale, scale),
            new SkewTransform(skewX, skewY));

    private static Element OriginDial(bool on, Action toggle)
    {
        var origins = new[]
        {
            ("Top Left", TransformOrigin.TopLeft, Color.FromHex("#38BDF8")),
            ("Top", TransformOrigin.TopCenter, Color.FromHex("#A78BFA")),
            ("Center", TransformOrigin.Center, Color.FromHex("#22C55E")),
            ("Right", TransformOrigin.RightCenter, Color.FromHex("#F97316")),
            ("Bottom Right", TransformOrigin.BottomRight, Color.FromHex("#EC4899")),
        };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(158),
            BackgroundColor = C.Bg,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(14)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            JustifyContent = JustifyContent.SpaceAround,
            Overflow = Overflow.Hidden,
            OnClick = _ => toggle(),
            Children = origins.Select(item =>
            {
                var (label, origin, color) = item;
                return (Element)Container(new ContainerProps
                {
                    Width = Dimension.Pixels(96),
                    Direction = LayoutDirection.Vertical,
                    Gap = 8,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(76),
                            Height = Dimension.Pixels(76),
                            BackgroundColor = Color.FromHex("#f0e8d8"),
                            BorderColor = C.Border,
                            BorderStyle = BorderStyle.Solid,
                            BorderWidth = 1,
                            BorderRadius = 18,
                            JustifyContent = JustifyContent.Center,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                Container(new ContainerProps
                                {
                                    Width = Dimension.Pixels(44),
                                    Height = Dimension.Pixels(44),
                                    BackgroundColor = color,
                                    BorderColor = C.TextTitle.WithAlpha(90),
                                    BorderStyle = BorderStyle.Solid,
                                    BorderWidth = 2,
                                    BorderRadius = on ? 18 : 8,
                                    Transform = MotionTransform(0, 0, on ? 185 : 0, on ? 1.12f : 0.86f),
                                    TransformOrigin = on ? origin : TransformOrigin.Center,
                                    Transitions = Trans(
                                        (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                                        (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut),
                                        (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut))
                                })
                            ]
                        }),
                        Text(new TextProps { Text = label, Color = C.TextSecondary, FontSize = 10, FontWeight = "700" })
                    ]
                });
            }).ToList()
        });
    }

    private static Element FlipTile(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(158),
            BackgroundColor = C.Bg,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(18)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            JustifyContent = JustifyContent.Center,
            Overflow = Overflow.Hidden,
            OnClick = _ => toggle(),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(250),
                    Height = Dimension.Pixels(96),
                    BackgroundColor = on ? Color.FromHex("#312E81") : Color.FromHex("#075985"),
                    BorderColor = on ? Color.FromHex("#C4B5FD") : Color.FromHex("#7DD3FC"),
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = 2,
                    BorderRadius = on ? 28 : 14,
                    Padding = new Spacing(Dimension.Pixels(18)),
                    Direction = LayoutDirection.Vertical,
                    Gap = 8,
                    Shadow = new BoxShadow((on ? Color.FromHex("#8B5CF6") : Color.FromHex("#06B6D4")).WithAlpha(95), on ? 20 : 10, on ? 34 : 18),
                    Transform = MotionTransform(on ? 56 : -42, on ? -8 : 12, on ? 11 : -9, on ? 1.08f : 0.9f),
                    TransformOrigin = on ? TransformOrigin.RightCenter : TransformOrigin.LeftCenter,
                    Transitions = Trans(
                        (nameof(ContainerProps.BackgroundColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Shadow), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut)),
                    Children =
                    [
                        Text(new TextProps { Text = on ? "Pivoted card" : "Resting card", Color = C.TextTitle, FontSize = 16, FontWeight = "800" }),
                        Text(new TextProps { Text = "Transform chain: translate → rotate → scale", Color = Color.FromHex("#DBEAFE"), FontSize = 11, FontWeight = "600" })
                    ]
                })
            ]
        });
    }

    private static Element SkewRibbon(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(150),
            BackgroundColor = C.Bg,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(18)),
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Overflow = Overflow.Hidden,
            OnClick = _ => toggle(),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(360),
                    Height = Dimension.Pixels(68),
                    BackgroundColor = on ? C.Peach : C.Accent,
                    BorderColor = on ? Color.FromHex("#FDA4AF") : Color.FromHex("#5EEAD4"),
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = 2,
                    BorderRadius = 18,
                    Shadow = new BoxShadow((on ? Color.FromHex("#FB7185") : Color.FromHex("#14B8A6")).WithAlpha(85), on ? 18 : 8, on ? 28 : 14),
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Transform = SkewMotionTransform(on ? 46 : -28, on ? -2 : 8, on ? -4 : 3, on ? 1.05f : 0.92f, on ? -14 : 10, on ? 2 : -2),
                    TransformOrigin = on ? TransformOrigin.BottomCenter : TransformOrigin.TopCenter,
                    Transitions = Trans(
                        (nameof(ContainerProps.BackgroundColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderColor), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Shadow), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut)),
                    Children = [Text(new TextProps { Text = "SKEW RIBBON", Color = C.TextTitle, FontSize = 18, FontWeight = "900" })]
                })
            ]
        });
    }

    private static Element OrbitChips(bool on, Action toggle)
    {
        var chips = new[]
        {
            ("A", Color.FromHex("#38BDF8"), -36f, -18f, -24f, 1.12f, TransformOrigin.BottomRight),
            ("B", C.Purple, 24f, 24f, 28f, 0.92f, TransformOrigin.TopLeft),
            ("C", Color.FromHex("#22C55E"), 42f, -16f, 42f, 1.08f, TransformOrigin.LeftCenter),
            ("D", Color.FromHex("#F97316"), -24f, 20f, -36f, 0.96f, TransformOrigin.RightCenter),
        };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(150),
            BackgroundColor = C.Bg,
            BorderRadius = 12,
            Padding = new Spacing(Dimension.Pixels(18)),
            Direction = LayoutDirection.Horizontal,
            Gap = 18,
            AlignItems = AlignItems.Center,
            JustifyContent = JustifyContent.Center,
            Overflow = Overflow.Hidden,
            OnClick = _ => toggle(),
            Children = chips.Select(chip =>
            {
                var (label, color, x, y, angle, scale, origin) = chip;
                return (Element)Container(new ContainerProps
                {
                    Width = Dimension.Pixels(64),
                    Height = Dimension.Pixels(64),
                    BackgroundColor = color,
                    BorderColor = C.TextTitle.WithAlpha(95),
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = 2,
                    BorderRadius = on ? 24 : 14,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Transform = MotionTransform(on ? x : 0, on ? y : 0, on ? angle : 0, on ? scale : 1f),
                    TransformOrigin = on ? origin : TransformOrigin.Center,
                    Transitions = Trans(
                        (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut)),
                    Children = [Text(new TextProps { Text = label, Color = C.TextTitle, FontSize = 18, FontWeight = "900" })]
                });
            }).ToList()
        });
    }

    private static Element TransformGallery(bool on, Action toggle) =>
        Card("Transform Gallery",
        [
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                Gap = 16,
                AlignItems = AlignItems.Center,
                Children =
                [
                    GalleryTile("Solar Tilt", "origin: bottom-right", Color.FromHex("#F97316"), Color.FromHex("#FDBA74"), on, toggle, TransformOrigin.BottomRight, MotionTransform(on ? 10 : -8, on ? -16 : 8, on ? -13 : 8, on ? 1.06f : 0.94f)),
                    GalleryTile("Neon Pivot", "origin: top-left", Color.FromHex("#8B5CF6"), Color.FromHex("#C4B5FD"), on, toggle, TransformOrigin.TopLeft, MotionTransform(on ? 0 : 18, on ? 16 : -8, on ? 18 : -10, on ? 0.96f : 1.08f)),
                    GalleryTile("Glass Skew", "skew + rotate", Color.FromHex("#06B6D4"), Color.FromHex("#67E8F9"), on, toggle, TransformOrigin.Center, SkewMotionTransform(on ? -12 : 10, on ? -10 : 10, on ? 6 : -5, on ? 1.04f : 0.96f, on ? 9 : -7, 0)),
                ]
            })
        ]);

    private static Element GalleryTile(string title, string subtitle, Color color, Color border, bool on, Action toggle, TransformOrigin origin, Transform transform)
    {
        return Container(new ContainerProps
        {
            FlexGrow = 1,
            Height = Dimension.Pixels(170),
            BackgroundColor = on ? color.WithAlpha(210) : C.InputBg,
            BorderColor = on ? border : C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = on ? 2 : 1,
            BorderRadius = on ? 24 : 12,
            Padding = new Spacing(Dimension.Pixels(18)),
            Direction = LayoutDirection.Vertical,
            JustifyContent = JustifyContent.SpaceBetween,
            Shadow = new BoxShadow(color.WithAlpha(on ? (byte)95 : (byte)35), on ? 18 : 6, on ? 30 : 12),
            Transform = transform,
            TransformOrigin = on ? origin : TransformOrigin.Center,
            OnClick = _ => toggle(),
            Transitions = Trans(
                (nameof(ContainerProps.BackgroundColor), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BorderColor), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BorderWidth), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.BorderRadius), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.Shadow), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.Transform), AnimSlow, Easing.EaseInOut),
                (nameof(ContainerProps.TransformOrigin), AnimSlow, Easing.EaseInOut)),
            Children =
            [
                Badge("TRANSFORM", color.WithAlpha(55), Color.White),
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 6, Children =
                [
                    Text(new TextProps { Text = title, Color = C.TextTitle, FontSize = 18, FontWeight = "900" }),
                    Text(new TextProps { Text = subtitle, Color = Color.FromHex("#DDEAFE"), FontSize = 12, FontWeight = "600" }),
                ]})
            ]
        });
    }

    private static Element SceneRow(string title, string subtitle, Element visual) =>
        Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            BackgroundColor = C.InputBg,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(14)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 14,
            Children =
            [
                Container(new ContainerProps { Width = Dimension.Pixels(180), FlexShrink = 0, Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    Text(new TextProps { Text = title, Color = C.TextTitle, FontSize = 14, FontWeight = "700" }),
                    Text(new TextProps { Text = subtitle, Color = C.TextSecondary, FontSize = 11 })
                ]}),
                Container(new ContainerProps { FlexGrow = 1, Children = [visual] })
            ]
        });

    private static Element MorphCard(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(120),
            BackgroundColor = C.Bg,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(14)),
            AlignItems = AlignItems.Center,
            Overflow = Overflow.Hidden,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(on ? 340 : 180),
                    Height = Dimension.Pixels(on ? 80 : 50),
                    BackgroundColor = on ? C.Peach : C.Accent,
                    BorderColor = on ? Color.FromHex("#FDA4AF") : Color.FromHex("#93C5FD"),
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = on ? 5 : 2,
                    BorderRadius = on ? 22 : 8,
                    Padding = new Spacing(Dimension.Pixels(on ? 20 : 10)),
                    Direction = LayoutDirection.Vertical,
                    Gap = on ? 8 : 4,
                    OnClick = _ => toggle(),
                    Transitions = Trans(
                        (nameof(ContainerProps.Width), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.Height), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.BackgroundColor), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderColor), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderWidth), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderRadius), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.Padding), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.Gap), AnimNormal, Easing.EaseInOut)),
                    Children =
                    [
                        Text(new TextProps { Text = "Interactive Card", Color = C.TextTitle, FontSize = 14, FontWeight = "800" }),
                        Text(new TextProps { Text = on ? "Expanded state" : "Compact state", Color = Color.FromHex("#E0E7FF"), FontSize = 11 })
                    ]
                })
            ]
        });
    }

    private static Element SlideDock(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(120),
            BackgroundColor = C.Bg,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(14)),
            AlignItems = AlignItems.Center,
            Overflow = Overflow.Hidden,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = on ? 22 : 4,
                    OnClick = _ => toggle(),
                    Transitions = Trans((nameof(ContainerProps.Gap), AnimNormal, Easing.EaseOut)),
                    Children =
                    [
                        DockDot(Color.FromHex("#EF4444"), on ? 0 : 0),
                        DockDot(Color.FromHex("#22C55E"), on ? 36 : 0),
                        DockDot(C.AppBlue, on ? 72 : 0),
                        DockDot(C.Purple, on ? 108 : 0),
                    ]
                })
            ]
        });
    }

    private static Element DockDot(Color color, float marginLeft) =>
        Container(new ContainerProps
        {
            Width = Dimension.Pixels(44), Height = Dimension.Pixels(44),
            Margin = new Spacing(Dimension.Pixels(marginLeft), Dimension.ZeroPixels, Dimension.ZeroPixels, Dimension.ZeroPixels),
            BackgroundColor = color, BorderRadius = 22,
            Transitions = Trans((nameof(ContainerProps.Margin), AnimNormal, Easing.EaseOut))
        });

    private static Element Accordion(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(120),
            BackgroundColor = C.Bg,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(14)),
            Overflow = Overflow.Hidden,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(360),
                    Height = Dimension.Pixels(on ? 90 : 40),
                    MaxHeight = Dimension.Pixels(on ? 90 : 40),
                    BackgroundColor = C.CardBg,
                    BorderColor = on ? Color.FromHex("#38BDF8") : C.Border,
                    BorderStyle = BorderStyle.Solid,
                    BorderWidth = 1,
                    BorderRadius = 10,
                    Padding = new Spacing(Dimension.Pixels(on ? 14 : 10)),
                    Direction = LayoutDirection.Vertical,
                    Gap = on ? 8 : 2,
                    Overflow = Overflow.Hidden,
                    OnClick = _ => toggle(),
                    Transitions = Trans(
                        (nameof(ContainerProps.Height), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.MaxHeight), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.BorderColor), AnimNormal, Easing.EaseInOut),
                        (nameof(ContainerProps.Padding), AnimNormal, Easing.EaseInOut)),
                    Children =
                    [
                        Text(new TextProps { Text = on ? "Expanded" : "Collapsed", Color = C.TextTitle, FontSize = 13, FontWeight = "700" }),
                        Text(new TextProps { Text = "Height + MaxHeight + Padding sync.", Color = C.TextSecondary, FontSize = 11 })
                    ]
                })
            ]
        });
    }

    private static Element ConstraintPanel(bool on, Action toggle)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(120),
            BackgroundColor = C.Bg,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(14)),
            Direction = LayoutDirection.Horizontal,
            AlignItems = AlignItems.Center,
            Gap = 14,
            OnClick = _ => toggle(),
            Overflow = Overflow.Hidden,
            Children =
            [
                ConstraintBox("Min", on ? 220 : 90, on ? 70 : 36, true),
                ConstraintBox("Max", on ? 220 : 90, on ? 70 : 36, false),
            ]
        });
    }

    private static Element ConstraintBox(string label, float w, float h, bool min)
    {
        return Container(new ContainerProps
        {
            Width = min ? null : Dimension.Pixels(240),
            Height = min ? null : Dimension.Pixels(100),
            MinWidth = min ? Dimension.Pixels(w) : null,
            MinHeight = min ? Dimension.Pixels(h) : null,
            MaxWidth = min ? null : Dimension.Pixels(w),
            MaxHeight = min ? null : Dimension.Pixels(h),
            Padding = new Spacing(Dimension.Pixels(10)),
            BackgroundColor = min ? Color.FromHex("#0EA5E9") : Color.FromHex("#10B981"),
            BorderRadius = 10,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            Transitions = Trans(
                (nameof(ContainerProps.MinWidth), AnimNormal, Easing.EaseInOut),
                (nameof(ContainerProps.MinHeight), AnimNormal, Easing.EaseInOut),
                (nameof(ContainerProps.MaxWidth), AnimNormal, Easing.EaseInOut),
                (nameof(ContainerProps.MaxHeight), AnimNormal, Easing.EaseInOut)),
            Children = [Text(new TextProps { Text = label, Color = C.TextTitle, FontWeight = "800" })]
        });
    }

    private static Element SupportedPropertyTable()
    {
        return Card("Supported Animation Properties",
        [
            Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 10, Children =
            [
                PropRow([PropBadge(nameof(ContainerProps.BackgroundColor), "Color"), PropBadge(nameof(ContainerProps.BorderColor), "Color"), PropBadge(nameof(ContainerProps.BorderWidth), "float"), PropBadge(nameof(ContainerProps.BorderRadius), "float")]),
                PropRow([PropBadge(nameof(ContainerProps.Margin), "Spacing"), PropBadge(nameof(ContainerProps.Padding), "Spacing"), PropBadge(nameof(ContainerProps.Gap), "float")]),
                PropRow([PropBadge(nameof(ContainerProps.Transform), "Transform"), PropBadge(nameof(ContainerProps.TransformOrigin), "Origin")]),
                PropRow([PropBadge(nameof(ContainerProps.Width), "Dim."), PropBadge(nameof(ContainerProps.Height), "Dim."), PropBadge(nameof(ContainerProps.MinWidth), "Dim."), PropBadge(nameof(ContainerProps.MinHeight), "Dim."), PropBadge(nameof(ContainerProps.MaxWidth), "Dim."), PropBadge(nameof(ContainerProps.MaxHeight), "Dim.")]),
            ]})
        ]);
    }

    private static Element PropRow(IReadOnlyList<Element> items) =>
        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 10, Children = items });

    private static Element PropBadge(string name, string type) =>
        Container(new ContainerProps
        {
            BackgroundColor = C.InputBg, BorderColor = C.Border, BorderStyle = BorderStyle.Solid, BorderWidth = 1, BorderRadius = 8,
            Padding = new Spacing(Dimension.Pixels(10), Dimension.Pixels(6)),
            Direction = LayoutDirection.Vertical, Gap = 2, Children =
            [
                Text(new TextProps { Text = name, Color = C.TextTitle, FontSize = 11, FontWeight = "700" }),
                Text(new TextProps { Text = type, Color = C.TextSecondary, FontSize = 10 })
            ]
        });

    // ═══════════════════════════════════════════════════════
    //  4. Layout Page
    // ═══════════════════════════════════════════════════════

    private static Element LayoutPage()
    {
        var (dirIdx, setDirIdx, _) = State(0);
        var (justifyIdx, setJustifyIdx, _) = State(0);
        var (alignIdx, setAlignIdx, _) = State(0);

        var dir = dirIdx == 0 ? LayoutDirection.Vertical : LayoutDirection.Horizontal;
        var justifies = new[] { JustifyContent.Start, JustifyContent.Center, JustifyContent.End, JustifyContent.SpaceBetween, JustifyContent.SpaceAround };
        var aligns = new[] { AlignItems.Start, AlignItems.Center, AlignItems.End, AlignItems.Stretch };

        var c1 = Color.FromHex("#EF4444"); var c2 = Color.FromHex("#22C55E");
        var c3 = C.AppBlue; var c4 = C.Purple;

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Layout"),
                    Subtitle("Flexbox layout visualization — change direction, justification, and alignment.")
                ]}),

                LayoutDemoCard("Direction", dirIdx, v => setDirIdx(v), ["Vertical", "Horizontal"], idx =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(80),
                        BackgroundColor = C.InputBg, BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = dir, Gap = 6,
                        Children =
                        [
                            Box(c1, dir == LayoutDirection.Horizontal ? Dimension.Pixels(44) : null, dir == LayoutDirection.Vertical ? Dimension.Pixels(44) : null),
                            Box(c2, dir == LayoutDirection.Horizontal ? Dimension.Pixels(60) : null, dir == LayoutDirection.Vertical ? Dimension.Pixels(60) : null),
                            Box(c3, dir == LayoutDirection.Horizontal ? Dimension.Pixels(50) : null, dir == LayoutDirection.Vertical ? Dimension.Pixels(50) : null),
                        ]
                    })),

                LayoutDemoCard("JustifyContent", justifyIdx, v => setJustifyIdx(v), ["Start", "Center", "End", "SpaceBetween", "SpaceAround"], idx =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(50),
                        BackgroundColor = C.InputBg, BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = justifies[justifyIdx], Gap = 4,
                        Children =
                        [
                            BoxSolid(24, c1), BoxSolid(24, c2), BoxSolid(24, c3)
                        ]
                    })),

                LayoutDemoCard("AlignItems", alignIdx, v => setAlignIdx(v), ["Start", "Center", "End", "Stretch"], idx =>
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100), Height = Dimension.Pixels(70),
                        BackgroundColor = C.InputBg, BorderRadius = 8,
                        Padding = new Spacing(Dimension.Pixels(10)),
                        Direction = LayoutDirection.Horizontal,
                        AlignItems = aligns[alignIdx], Gap = 4,
                        Children =
                        [
                            Container(new ContainerProps { Width = Dimension.Pixels(36), Height = Dimension.Pixels(22), BackgroundColor = c1, BorderRadius = 4 }),
                            Container(new ContainerProps { Width = Dimension.Pixels(36), Height = Dimension.Pixels(48), BackgroundColor = c2, BorderRadius = 4 }),
                            Container(new ContainerProps { Width = Dimension.Pixels(36), Height = Dimension.Pixels(34), BackgroundColor = c3, BorderRadius = 4 }),
                        ]
                    })),
            ]
        });
    }

    private static Element LayoutDemoCard(string label, int idx, Action<int> setIdx, string[] options, Func<int, Element> content)
    {
        return Card(null,
        [
            Container(new ContainerProps { Direction = LayoutDirection.Horizontal, AlignItems = AlignItems.Center, Gap = 10, Children =
            [
                Text(new TextProps { Text = $"{label}:", Color = C.TextTitle, FontWeight = "700", FontSize = 14 }),
                Container(new ContainerProps { Width = Dimension.Pixels(170), Children =
                [
                    ComboBox(new ComboBoxProps { Options = options, SelectedIndex = idx, OnSelectionChanged = v => setIdx(v), BackgroundColor = C.InputBg, TextColor = C.TextTitle, BorderColor = C.Border })
                ]})
            ]}),
            content(idx)
        ]);
    }

    private static Element Box(Color color, Dimension? w, Dimension? h) =>
        Container(new ContainerProps
        {
            Width = w ?? Dimension.Pixels(36), Height = h ?? Dimension.Pixels(36),
            BackgroundColor = color, BorderRadius = 6,
            JustifyContent = JustifyContent.Center, AlignItems = AlignItems.Center,
            Children = [Text(new TextProps { Text = "#", Color = C.TextTitle, FontSize = 12, FontWeight = "700" })]
        });

    private static Element BoxSolid(float size, Color color) =>
        Container(new ContainerProps { Width = Dimension.Pixels(size), Height = Dimension.Pixels(size), BackgroundColor = color, BorderRadius = 4 });

    // ═══════════════════════════════════════════════════════
    //  5. Cards Page
    // ═══════════════════════════════════════════════════════

    private static Element CardsPage()
    {
        var animals = new[] {
            ("Frog",  "🐸", Color.FromHex("#6fba2c"), "Green, hoppy, loves ponds"),
            ("Rabbit","🐰", Color.FromHex("#f5c31c"), "Fluffy, fast, loves carrots"),
            ("Bear",  "🐻", Color.FromHex("#c4b89e"), "Big, warm, loves honey"),
            ("Owl",   "🦉", Color.FromHex("#19c8b9"), "Wise, nocturnal, hoots"),
            ("Fox",   "🦊", Color.FromHex("#e05a5a"), "Clever, orange, sneaky"),
        };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Cards"),
                    Subtitle("Warm, rounded card components with shadows and hover feedback.")
                ]}),

                Card("Animal Cards",
                [
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 14, Children = animals.Select(a => AnimalCard(a.Item1, a.Item2, a.Item3, a.Item4)).ToList() })
                ]),

                Card("Horizontal Cards",
                [
                    AnimalHCard("🐸", "Frog", Color.FromHex("#6fba2c"), "A friendly little hopper who loves rainy days and lily pads."),
                    AnimalHCard("🐰", "Rabbit", Color.FromHex("#f5c31c"), "Fast and fluffy, always first at the carrot patch."),
                    AnimalHCard("🐻", "Bear", Color.FromHex("#c4b89e"), "Gentle giant who gives the warmest hugs in the forest."),
                ]),

                Card("Clickable Cards",
                [
                    Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 14, Children =
                    [
                        ClickableCard("🌱", "Plant a Tree", C.Success, "Help reforest the island"),
                        ClickableCard("🏗", "Build a House", C.Accent, "Create your dream home"),
                        ClickableCard("🏃", "Go Explore", C.Warning, "Discover hidden treasures"),
                    ]})
                ]),
            ]
        });
    }

    private static Element AnimalCard(string title, string emoji, Color accent, string desc)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(180),
            Padding = new Spacing(Dimension.Pixels(20)),
            BackgroundColor = C.CardBg,
            BorderRadius = 16,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            Direction = LayoutDirection.Vertical,
            Gap = 10,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(52), Height = Dimension.Pixels(52),
                    BackgroundColor = accent.WithAlpha(25), BorderRadius = 26,
                    JustifyContent = JustifyContent.Center, AlignItems = AlignItems.Center,
                    Children = [Text(new TextProps { Text = emoji, FontSize = 24 })]
                }),
                Text(new TextProps { Text = title, FontSize = 15, Color = C.TextTitle, FontWeight = "700" }),
                Text(new TextProps { Text = desc, FontSize = 11, Color = C.TextSecondary, NoWrap = false })
            ]
        });
    }

    private static Element AnimalHCard(string emoji, string title, Color accent, string desc)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(18)),
            BackgroundColor = C.CardBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 12,
            Direction = LayoutDirection.Horizontal,
            Gap = 14,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps { Width = Dimension.Pixels(44), Height = Dimension.Pixels(44), BackgroundColor = accent.WithAlpha(25), BorderRadius = 22, JustifyContent = JustifyContent.Center, AlignItems = AlignItems.Center, FlexShrink = 0, Children =
                [
                    Text(new TextProps { Text = emoji, FontSize = 22 })
                ]}),
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 3, FlexGrow = 1, Children =
                [
                    Text(new TextProps { Text = title, FontSize = 15, Color = C.TextTitle, FontWeight = "700" }),
                    Text(new TextProps { Text = desc, FontSize = 11, Color = C.TextSecondary, NoWrap = false })
                ]}),
                Text(new TextProps { Text = "➡", FontSize = 16, Color = C.TextMuted })
            ]
        });
    }

    private static Element ClickableCard(string emoji, string title, Color accent, string desc)
    {
        var (hov, setHov, _) = State(false);

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(190),
            Padding = new Spacing(Dimension.Pixels(20)),
            BackgroundColor = C.CardBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 14,
            Direction = LayoutDirection.Vertical,
            Gap = 10,
            AlignItems = AlignItems.Center,
            OnMouseEnter = () => setHov(true),
            OnMouseLeave = () => setHov(false),
            OnClick = _ => { },
            Transitions = Trans(
                (nameof(ContainerProps.BorderColor), 180, Easing.EaseOut)),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(48), Height = Dimension.Pixels(48),
                    BackgroundColor = accent.WithAlpha((byte)(hov.Value ? 45 : 25)),
                    BorderRadius = 24,
                    JustifyContent = JustifyContent.Center, AlignItems = AlignItems.Center,
                    Transitions = Trans((nameof(ContainerProps.BackgroundColor), 180, Easing.EaseOut)),
                    Children = [Text(new TextProps { Text = emoji, FontSize = 22 })]
                }),
                Text(new TextProps { Text = title, FontSize = 14, Color = C.TextTitle, FontWeight = "700" }),
                Text(new TextProps { Text = desc, FontSize = 11, Color = C.TextSecondary, NoWrap = false })
            ]
        });
    }

    // ═══════════════════════════════════════════════════════
    //  6. Counter Page
    // ═══════════════════════════════════════════════════════

    private static Element CounterPage()
    {
        var (count, setCount, updateCount) = State(0);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Counter"),
                    Subtitle("Reactive state management with conditional styling — the simplest EchoUI component.")
                ]}),

                Card("State Demo",
                [
                    Container(new ContainerProps { AlignItems = AlignItems.Center, Gap = 16, Children =
                    [
                        Text(new TextProps
                        {
                            Text = $"{count.Value}",
                            FontSize = 56,
                            FontWeight = "900",
                            Color = count.Value == 0 ? Color.White : (count.Value < 0 ? C.Error : C.Success)
                        }),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 12, AlignItems = AlignItems.Center, Children =
                        [
                            Button(new ButtonProps { Text = "−", Width = Dimension.Pixels(50), Height = Dimension.Pixels(42), OnClick = _ => updateCount(v => v - 1) }),
                            Button(new ButtonProps { Text = "Reset", Width = Dimension.Pixels(90), Height = Dimension.Pixels(42), BackgroundColor = C.InputBg, TextColor = C.TextTitle, OnClick = _ => setCount(0) }),
                            Button(new ButtonProps { Text = "+", Width = Dimension.Pixels(50), Height = Dimension.Pixels(42), OnClick = _ => updateCount(v => v + 1) }),
                        ]}),
                        Container(new ContainerProps
                        {
                            Width = Dimension.Percent(100),
                            Height = Dimension.Pixels(4),
                            BackgroundColor = count.Value == 0 ? C.Border : (count.Value < 0 ? C.Error : C.Success),
                            BorderRadius = 2,
                            Transitions = Trans((nameof(ContainerProps.BackgroundColor), 250, Easing.EaseInOut))
                        }),
                        Text(new TextProps
                        {
                            Text = count.Value switch { 0 => "Zero — click + or − to start", < 0 => $"Negative ({count.Value})", _ => $"Positive (+{count.Value})" },
                            FontSize = 13,
                            Color = count.Value switch { 0 => C.TextSecondary, < 0 => Color.FromHex("#FCA5A5"), _ => Color.FromHex("#86EFAC") }
                        })
                    ]})
                ]),

                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 8, Children =
                [
                    Text(new TextProps { Text = "Code", FontSize = 14, Color = C.TextSecondary, FontWeight = "600" }),
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        BackgroundColor = Color.FromHex("#2b2118"),
                        BorderColor = C.Border,
                        BorderStyle = BorderStyle.Solid,
                        BorderWidth = 1,
                        BorderRadius = 10,
                        Padding = new Spacing(Dimension.Pixels(20)),
                        Children =
                        [
                            Text(new TextProps
                            {
                                Text = CodeSample,
                                Color = Color.FromHex("#94A3B8"),
                                FontSize = 12,
                                FontFamily = "Consolas",
                                NoWrap = false
                            })
                        ]
                    })
                ]})
            ]
        });
    }

    private const string CodeSample = """
var (count, setCount, updateCount) = State(0);

return Container([
    Text($"count: {count.Value}",
        Color: count == 0 ? Black : (count < 0 ? Red : Green)),
    Container(
        Direction: Horizontal, Gap: 5,
        Children: [
            Button("-", OnClick: _ => updateCount(v => v - 1)),
            Button("Reset", OnClick: _ => setCount(0)),
            Button("+", OnClick: _ => updateCount(v => v + 1)),
        ]
    ),
]);
""";

    // ═══════════════════════════════════════════════════════
    //  7. Markdown Page
    // ═══════════════════════════════════════════════════════

    private const string SampleMarkdown = """
# Welcome to EchoUI Markdown

This is a demonstration of rendering Markdown content directly within the EchoUI framework.

## Features

- **Headings** — h1 through h6
- **Lists** — ordered and unordered
- **Code blocks** with syntax highlighting
- **Blockquotes** for callouts

## Code Block

```csharp
public static Element App() {
    var (count, setCount, _) = Hooks.State(0);

    return Container(new ContainerProps {
        Children: [
            Text(new TextProps { Text = $"Counter: {count.Value}" }),
            Button(new ButtonProps {
                Text = "Click Me!",
                OnClick = () => setCount(count.Value + 1)
            })
        ]
    });
}
```

> **Tip:** EchoUI uses a React-inspired component model.
> Function components + hooks = declarative UI in pure C#.

---

### Quick Start

1. Install the EchoUI NuGet package
2. Create a component function
3. Mount it with a renderer

Built with ❤️ using **EchoUI** + **Markdig**.
""";

    private static Element MarkdownPage()
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Markdown"),
                    Subtitle("Render Markdown content using Markdig → EchoUI element tree.")
                ]}),

                Card(null,
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Percent(100),
                        BackgroundColor = C.InputBg,
                        BorderRadius = 10,
                        Padding = new Spacing(Dimension.Pixels(24)),
                        Overflow = Overflow.Auto,
                        Children =
                        [
                            EchoUI.Demo.Elements.MarkdownRenderer(new MarkdownProps { Content = SampleMarkdown })
                        ]
                    })
                ])
            ]
        });
    }

    // ═══════════════════════════════════════════════════════
    //  Diagnostics Page
    // ═══════════════════════════════════════════════════════

    private static Element DiagnosticsPage()
    {
        var (shouldThrow, setShouldThrow, _) = State(false);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            AlignItems = AlignItems.Stretch,
            Children =
            [
                Container(new ContainerProps { Direction = LayoutDirection.Vertical, Gap = 4, Children =
                [
                    SectionTitle("Diagnostics"),
                    Subtitle("Click the button to intentionally throw during UI conversion and verify EchoUI prints the element stack.")
                ]}),

                Card("UI Error Diagnostics",
                [
                    Text(new TextProps
                    {
                        Text = "这个页面会在多层组件 / 容器内部故意抛出异常。控制台和 Debug 输出应包含 EchoUIRenderException、原始异常，以及从根组件到出错元素的元素栈。",
                        Color = C.TextSecondary,
                        FontSize = 13,
                        NoWrap = false
                    }),
                    Button(new ButtonProps
                    {
                        Text = shouldThrow.Value ? "Exception armed" : "Throw during render",
                        BackgroundColor = C.Error,
                        TextColor = C.TextTitle,
                        Width = Dimension.Pixels(180),
                        Height = Dimension.Pixels(40),
                        OnClick = _ => setShouldThrow(true)
                    }),
                    Create((Component)DiagnosticsOuter, new Props
                    {
                        Key = "diagnostics-outer",
                        Children = [Create((Component)DiagnosticsMiddle, new DiagnosticMiddleProps { Key = "middle-component", ShouldThrow = shouldThrow.Value })]
                    })
                ])
            ]
        });
    }

    private static Element DiagnosticsOuter(Props props)
    {
        return Container(new ContainerProps
        {
            Key = "outer-container",
            BackgroundColor = C.InputBg,
            BorderColor = C.Border,
            BorderStyle = BorderStyle.Solid,
            BorderWidth = 1,
            BorderRadius = 10,
            Padding = new Spacing(Dimension.Pixels(16)),
            Direction = LayoutDirection.Vertical,
            Gap = 10,
            Children =
            [
                Text(new TextProps { Text = "Outer diagnostic wrapper", Color = C.TextTitle, FontWeight = "700" }),
                .. props.Children
            ]
        });
    }

    private sealed record DiagnosticMiddleProps : Props
    {
        public bool ShouldThrow { get; init; }
    }

    private static Element DiagnosticsMiddle(Props props)
    {
        var middleProps = (DiagnosticMiddleProps)props;
        return Container(new ContainerProps
        {
            Key = "middle-container",
            BackgroundColor = Color.FromHex("#f0e8d8"),
            BorderRadius = 8,
            Padding = new Spacing(Dimension.Pixels(14)),
            Children = [Create((Component)DiagnosticsBomb, new DiagnosticBombProps { Key = "bomb-component", ShouldThrow = middleProps.ShouldThrow })]
        });
    }

    private sealed record DiagnosticBombProps : Props
    {
        public bool ShouldThrow { get; init; }
    }

    private static Element DiagnosticsBomb(Props props)
    {
        var bombProps = (DiagnosticBombProps)props;
        if (bombProps.ShouldThrow)
        {
            throw new InvalidOperationException("Intentional EchoUI diagnostics test exception.");
        }

        return Text(new TextProps
        {
            Text = "Diagnostics component is healthy. Click the button above to trigger the test exception.",
            Color = C.Success,
            FontSize = 13,
            NoWrap = false
        });
    }

    // ═══════════════════════════════════════════════════════
    //  Color Tokens (dark theme)
    // ═══════════════════════════════════════════════════════

    private static class C
    {
        public static readonly Color Bg = Color.FromHex("#f8f8f0");
        public static readonly Color SidebarBg = Color.FromHex("#fffaf0");
        public static readonly Color CardBg = Color.FromHex("#f7f3df");
        public static readonly Color InputBg = Color.FromHex("#f0e8d8");
        public static readonly Color Border = Color.FromHex("#c4b89e");
        public static readonly Color NavActiveBg = Color.FromHex("#B7C6E5");
        public static readonly Color NavInactiveBg = Color.Transparent;

        public static readonly Color Accent = Color.FromHex("#19c8b9");
        public static readonly Color AccentHover = Color.FromHex("#3dd4c6");
        public static readonly Color AccentBg = Color.FromHex("#e6f9f6");
        public static readonly Color Success = Color.FromHex("#6fba2c");
        public static readonly Color Warning = Color.FromHex("#f5c31c");
        public static readonly Color Error = Color.FromHex("#e05a5a");
        public static readonly Color Peach = Color.FromHex("#e18c6f");
        public static readonly Color Purple = Color.FromHex("#b77dee");
        public static readonly Color AppBlue = Color.FromHex("#889df0");
        public static readonly Color Shadow = Color.FromHex("#bdaea0");
        public static readonly Color ShadowInput = Color.FromHex("#d4c9b4");

        public static readonly Color TextBody = Color.FromHex("#725d42");
        public static readonly Color TextTitle = Color.FromHex("#794f27");
        public static readonly Color TextSecondary = Color.FromHex("#9f927d");
        public static readonly Color TextMuted = Color.FromHex("#8a7b66");
        public static readonly Color TextFaint = Color.FromHex("#c4b89e");
    }
}