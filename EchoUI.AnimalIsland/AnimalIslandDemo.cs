using EchoUI.Core;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

/// <summary>
/// Animal Island UI styled demo — showcases all EchoUI components
/// styled per the Animal Island Design Tokens.
/// </summary>
public static class AnimalIslandDemo
{
    public static Element? Render(Props _)
    {
        var (tabIdx, setTabIdx, _) = State(0);
        var tabTitles = new[] { "Components", "Cards", "Playground" };

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            BackgroundColor = DesignTokens.BgMain,
            Direction = LayoutDirection.Vertical,
            Children =
            [
                NavBar(),
                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    FlexShrink = 1,
                    Width = Dimension.Percent(100),
                    AlignItems = AlignItems.Stretch,
                    Padding = new Spacing(Dimension.Pixels(0), Dimension.Pixels(10)),
                    Children =
                    [
                        Tabs(new TabProps
                        {
                            Titles = tabTitles,
                            InitialIndex = 0,
                            OnTabChanged = v => setTabIdx(v),
                            ActiveTabBackgroundColor = DesignTokens.PrimaryBg,
                            ActiveTabTextColor = DesignTokens.Primary,
                            InactiveTabTextColor = DesignTokens.TextMuted,
                            Content = i => tabTitles[i] switch
                            {
                                "Components" => ComponentsPage(),
                                "Cards" => CardsPage(),
                                _ => PlaygroundPage(),
                            }
                        })
                    ]
                })
            ]
        });
    }

    private static Element NavBar()
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexShrink = 0,
            Padding = new Spacing(Dimension.Pixels(24), Dimension.Pixels(16)),
            Direction = LayoutDirection.Horizontal,
            JustifyContent = JustifyContent.SpaceBetween,
            AlignItems = AlignItems.Center,
            BackgroundColor = DesignTokens.BgContent,
            BorderWidth = 0,
            BorderRadius = 0,
            Children =
            [
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    AlignItems = AlignItems.Center,
                    Gap = 12,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Width = Dimension.Pixels(36),
                            Height = Dimension.Pixels(36),
                            BackgroundColor = DesignTokens.Primary,
                            BorderRadius = 10,
                        }),
                        Text(new TextProps
                        {
                            Text = "Animal Island UI",
                            FontSize = 24,
                            Color = DesignTokens.TextTitle,
                            FontWeight = "800"
                        })
                    ]
                }),
                Text(new TextProps
                {
                    Text = "Warm . Rounded . Playful",
                    FontSize = 13,
                    Color = DesignTokens.TextSecondary,
                    FontWeight = "500"
                })
            ]
        });
    }

    private static Element ComponentsPage()
    {
        var (btnDisabled, setBtnDisabled, _) = State(true);
        var (inputVal, setInputVal, _) = State("");
        var (textInputVal, setTextInputVal, _) = State("");
        var (checkState, setCheckState, _) = State(true);
        var (switchState, setSwitchState, _) = State(false);
        var (comboIdx, setComboIdx, _) = State(0);
        var (radioIdx, setRadioIdx, _) = State(0);
        var (progress, setProgress, _) = State(42);

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            FlexShrink = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 28,
            Overflow = Overflow.Auto,
            BackgroundColor = DesignTokens.BgMain,
            Children =
            [
                SectionTitle("Buttons"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 14,
                    Children =
                    [
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 14,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                AIButton(new ButtonProps { Text = "Small", Size = "small" }),
                                AIButton(new ButtonProps { Text = "Middle" }),
                                AIButton(new ButtonProps { Text = "Large", Size = "large" })
                            ]
                        }),
                        Container(new ContainerProps
                        {
                            Direction = LayoutDirection.Horizontal,
                            Gap = 14,
                            AlignItems = AlignItems.Center,
                            Children =
                            [
                                AIButton(new ButtonProps { Text = "Primary" }),
                                AIButton(new ButtonProps { Text = "Success", BackgroundColor = DesignTokens.Success }),
                                AIButton(new ButtonProps { Text = "Warning", BackgroundColor = DesignTokens.Warning }),
                                AIButton(new ButtonProps { Text = "Error", BackgroundColor = DesignTokens.Error, HoverColor = DesignTokens.ErrorActive }),
                                AIButton(new ButtonProps { Text = "Disabled", Disabled = true }),
                            ]
                        }),
                    ]
                }),

                SectionTitle("Inputs"),
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Direction = LayoutDirection.Vertical,
                    Gap = 12,
                    Children =
                    [
                        AIInput(new InputProps { Value = inputVal.Value, OnValueChanged = v => setInputVal(v), BackgroundColor = DesignTokens.BgContent, TextColor = DesignTokens.TextBody, BorderColor = DesignTokens.Border, FocusedBorderColor = DesignTokens.BorderFocus }),
                        AITextInput(new TextInputProps { Value = textInputVal.Value, OnValueChanged = v => setTextInputVal(v), Placeholder = "Type here...", Width = Dimension.Percent(100) }),
                    ]
                }),

                SectionTitle("Selection Controls"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 30,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        CheckBox(new CheckBoxProps { Label = "Remember me", IsChecked = checkState.Value, OnToggle = v => setCheckState(v), CheckColor = DesignTokens.Primary, BorderColor = DesignTokens.Border }),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, AlignItems = AlignItems.Center, Gap = 8, Children = [ Text(new TextProps { Text = "Notify", Color = DesignTokens.TextBody, FontWeight = "600" }), Switch(new SwitchProps { DefaultIsOn = switchState.Value, OnToggle = v => setSwitchState(v), OnColor = DesignTokens.Success, OffColor = DesignTokens.ShadowInput }) ] }),
                        Container(new ContainerProps { Width = Dimension.Pixels(180), Children = [ ComboBox(new ComboBoxProps { Options = ["Frog", "Rabbit", "Bear", "Owl", "Fox"], SelectedIndex = comboIdx.Value, OnSelectionChanged = v => setComboIdx(v), BackgroundColor = DesignTokens.BgContent, TextColor = DesignTokens.TextBody, BorderColor = DesignTokens.Border }) ] })
                    ]
                }),

                SectionTitle("Radio Group"),
                RadioGroup(new RadioGroupProps
                {
                    Options = ["Puppy", "Kitten", "Bunny"],
                    SelectedIndex = radioIdx.Value,
                    OnSelectionChanged = v => setRadioIdx(v),
                    Direction = LayoutDirection.Horizontal,
                    SelectedColor = DesignTokens.Primary,
                    BorderColor = DesignTokens.Border
                }),

                SectionTitle("Avatars"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 16,
                    AlignItems = AlignItems.Center,
                    // EchoUI flexbox is single-line; items overflow horizontally
                    Children =
                    [
                        AIAvatar("\U0001F438", DesignTokens.Success.WithAlpha(20), 48),
                        AIAvatar("\U0001F430", Color.FromHex("#f5c31c").WithAlpha(25), 48),
                        AIAvatar("\U0001F43B", Color.FromHex("#c4b89e").WithAlpha(30), 48),
                        AIAvatar("\U0001F989", DesignTokens.PrimaryBg, 48),
                        AIAvatar("\U0001F98A", DesignTokens.Error.WithAlpha(20), 48),
                        AIAvatar("\U00002B50", DesignTokens.Warning.WithAlpha(30), 56, true),
                        AIAvatar("\U0001F436", DesignTokens.Success.WithAlpha(15), 36),
                        AIAvatar("\U0001F431", DesignTokens.PrimaryBg, 64),
                    ]
                }),

                SectionTitle("Tags"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 10,
                    AlignItems = AlignItems.Center,
                    // EchoUI flexbox is single-line; items overflow horizontally
                    Children =
                    [
                        AITag("Frog", DesignTokens.Success.WithAlpha(20), DesignTokens.Success),
                        AITag("Rabbit", Color.FromHex("#f5c31c").WithAlpha(25), Color.FromHex("#996f00")),
                        AITag("Bear", Color.FromHex("#c4b89e").WithAlpha(30), Color.FromHex("#795a3e")),
                        AITag("Owl", DesignTokens.PrimaryBg, DesignTokens.Primary),
                        AITag("Fox", DesignTokens.Error.WithAlpha(20), DesignTokens.Error),
                        AITag("New!", DesignTokens.Warning.WithAlpha(25), Color.FromHex("#996f00")),
                        AITag("Disabled", DesignTokens.BgDisabled, DesignTokens.TextDisabled),
                        AITag("Success", DesignTokens.Success.WithAlpha(15), DesignTokens.Success),
                        AITag("Beta", DesignTokens.Primary, DesignTokens.TextInverse),
                    ]
                }),

                SectionTitle("Progress"),
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(440),
                    Direction = LayoutDirection.Vertical,
                    Gap = 16,
                    Children =
                    [
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, JustifyContent = JustifyContent.SpaceBetween, Children = [ Text(new TextProps { Text = "Loading...", FontSize = 12, Color = DesignTokens.TextSecondary }), Text(new TextProps { Text = "25%", FontSize = 12, Color = DesignTokens.Success, FontWeight = "700" }) ] }),
                        AIProgressBar(25, DesignTokens.Success),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, JustifyContent = JustifyContent.SpaceBetween, Children = [ Text(new TextProps { Text = "Syncing", FontSize = 12, Color = DesignTokens.TextSecondary }), Text(new TextProps { Text = "50%", FontSize = 12, Color = DesignTokens.Primary, FontWeight = "700" }) ] }),
                        AIProgressBar(50, DesignTokens.Primary, 10),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, JustifyContent = JustifyContent.SpaceBetween, Children = [ Text(new TextProps { Text = "Uploading", FontSize = 12, Color = DesignTokens.TextSecondary }), Text(new TextProps { Text = "75%", FontSize = 12, Color = DesignTokens.Warning, FontWeight = "700" }) ] }),
                        AIProgressBar(75, DesignTokens.Warning, 6),
                        Container(new ContainerProps { Direction = LayoutDirection.Horizontal, JustifyContent = JustifyContent.SpaceBetween, Children = [ Text(new TextProps { Text = "Complete", FontSize = 12, Color = DesignTokens.TextSecondary }), Text(new TextProps { Text = "100%", FontSize = 12, Color = DesignTokens.Success, FontWeight = "700" }) ] }),
                        AIProgressBar(100, DesignTokens.Success, 12),
                    ]
                }),

                SectionTitle("Tabs"),
                Text(new TextProps
                {
                    Text = "(See tab navigation at top of page -- built-in Tabs component)",
                    FontSize = 12,
                    Color = DesignTokens.TextDisabled,
                    FontWeight = "500"
                }),
            ]
        });
    }

    private static Element CardsPage()
    {
        var animals = new[] {
            (Name: "Frog", Emoji: "\U0001F438", Color: Color.FromHex("#6fba2c"), Desc: "Green, hoppy, loves ponds"),
            (Name: "Rabbit", Emoji: "\U0001F430", Color: Color.FromHex("#f5c31c"), Desc: "Fluffy, fast, loves carrots"),
            (Name: "Bear", Emoji: "\U0001F43B", Color: Color.FromHex("#c4b89e"), Desc: "Big, warm, loves honey"),
            (Name: "Owl", Emoji: "\U0001F989", Color: Color.FromHex("#19c8b9"), Desc: "Wise, nocturnal, hoots"),
            (Name: "Fox", Emoji: "\U0001F98A", Color: Color.FromHex("#e05a5a"), Desc: "Clever, orange, sneaky"),
        };

        var cards = new List<Element>();
        foreach (var a in animals)
        {
            cards.Add(AICard(a.Name, a.Emoji, a.Color, a.Desc));
        }

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            FlexShrink = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            Overflow = Overflow.Auto,
            Children =
            [
                SectionTitle("Animal Cards"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 18,
                    // EchoUI flexbox is single-line; items overflow horizontally
                    Padding = new Spacing(Dimension.Pixels(4)),
                    Children = cards
                }),

                SectionTitle("Horizontal Cards"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 14,
                    Padding = new Spacing(Dimension.Pixels(4)),
                    Children =
                    [
                        AIHorizontalCard("\U0001F438", "Frog", Color.FromHex("#6fba2c"), "A friendly little hopper who loves rainy days and lily pads."),
                        AIHorizontalCard("\U0001F430", "Rabbit", Color.FromHex("#f5c31c"), "Fast and fluffy, always first at the carrot patch."),
                        AIHorizontalCard("\U0001F43B", "Bear", Color.FromHex("#c4b89e"), "Gentle giant who gives the warmest hugs in the forest."),
                    ]
                }),

                SectionTitle("Clickable Cards (with hover feedback)"),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Horizontal,
                    Gap = 18,
                    // EchoUI flexbox is single-line; items overflow horizontally
                    Children =
                    [
                        AIClickableCard("\U0001F331", "Plant a Tree", DesignTokens.Success, "Help reforest the island"),
                        AIClickableCard("\U0001F3D7", "Build a House", DesignTokens.Primary, "Create your dream home"),
                        AIClickableCard("\U0001F3C3", "Go Explore", DesignTokens.Warning, "Discover hidden treasures"),
                    ]
                }),
            ]
        });
    }

    private static Element PlaygroundPage()
    {
        var (count, setCount, updateCount) = State(0);
        var (modalVisible, setModalVisible, _) = State(false);

        var children = new List<Element>
        {
            SectionTitle("Counter Demo"),
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Vertical,
                AlignItems = AlignItems.Center,
                Gap = 20,
                Padding = new Spacing(Dimension.Pixels(24)),
                BackgroundColor = DesignTokens.BgContent,
                BorderRadius = DesignTokens.RadiusLg,
                Children =
                [
                    Text(new TextProps
                    {
                        Text = $"{count.Value}",
                        FontSize = 56,
                        FontWeight = "900",
                        Color = count.Value == 0 ? DesignTokens.TextBody : (count.Value < 0 ? DesignTokens.Error : DesignTokens.Success)
                    }),
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Gap = 14,
                        AlignItems = AlignItems.Center,
                        Children =
                        [
                            AIButton(new ButtonProps { Text = "-", Size = "small", Width = Dimension.Pixels(56), OnClick = _ => updateCount(v => v - 1) }),
                            AIButton(new ButtonProps { Text = "Reset", BackgroundColor = DesignTokens.Primary, HoverColor = DesignTokens.PrimaryHover, OnClick = _ => setCount(0) }),
                            AIButton(new ButtonProps { Text = "+", Size = "small", Width = Dimension.Pixels(56), OnClick = _ => updateCount(v => v + 1) }),
                        ]
                    }),
                    Container(new ContainerProps
                    {
                        Width = Dimension.Pixels(200),
                        Height = Dimension.Pixels(6),
                        BackgroundColor = count.Value == 0 ? DesignTokens.Border : (count.Value < 0 ? DesignTokens.Error : DesignTokens.Success),
                        BorderRadius = 3,
                        Transitions =
                        [
                            [nameof(ContainerProps.BackgroundColor), new Transition(200, Easing.EaseInOut)]
                        ]
                    }),
                    Text(new TextProps
                    {
                        Text = count.Value == 0 ? "Tap + or - to start!" : $"You clicked {(count.Value < 0 ? -count.Value : count.Value)} time{((count.Value == 1 || count.Value == -1) ? "" : "s")}",
                        FontSize = 13,
                        Color = DesignTokens.TextSecondary,
                        FontWeight = "500"
                    })
                ]
            }),

            SectionTitle("Modal / Dialog"),
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal,
                Gap = 14,
                AlignItems = AlignItems.Center,
                Children =
                [
                    AIButton(new ButtonProps { Text = "Open Modal", OnClick = _ => setModalVisible(true) }),
                    Text(new TextProps { Text = modalVisible.Value ? "(modal is open)" : "(click to demo overlay)", FontSize = 12, Color = DesignTokens.TextSecondary, FontWeight = "500" }),
                ]
            }),
        };

        // Modal overlay — only added to tree when visible; never put null in Children
        if (modalVisible.Value)
        {
            children.Add(Container(new ContainerProps
            {
                Key = "modal-overlay",
                Float = true,
                Width = Dimension.Percent(100),
                Height = Dimension.Percent(100),
                BackgroundColor = new Color(0, 0, 0, 100),
                JustifyContent = JustifyContent.Center,
                AlignItems = AlignItems.Center,
                OnClick = _ => setModalVisible(false),
                Children =
                [
                    Container(new ContainerProps
                    {
                        Width = Dimension.Pixels(380),
                        Padding = new Spacing(Dimension.Pixels(32)),
                        Direction = LayoutDirection.Vertical,
                        Gap = 16,
                        AlignItems = AlignItems.Center,
                        BackgroundColor = DesignTokens.BgContent,
                        BorderRadius = DesignTokens.RadiusLg,
                        Shadow = new BoxShadow(new Color(0, 0, 0, 60), 8f),
                        OnClick = null,
                        Children =
                        [
                            Text(new TextProps { Text = "\U0001F44B", FontSize = 48 }),
                            Text(new TextProps { Text = "Welcome!", FontSize = 22, Color = DesignTokens.TextTitle, FontWeight = "800" }),
                            Text(new TextProps { Text = "This is an Animal Island themed dialog. Click outside or press Cancel to dismiss.", FontSize = 13, Color = DesignTokens.TextSecondary, FontWeight = "500", NoWrap = false }),
                            Container(new ContainerProps { Direction = LayoutDirection.Horizontal, Gap = 12, Children = [
                                AIButton(new ButtonProps { Text = "Cancel", BackgroundColor = DesignTokens.BgDisabled, TextColor = DesignTokens.TextBody, HoverColor = DesignTokens.Border, OnClick = _ => setModalVisible(false) }),
                                AIButton(new ButtonProps { Text = "Confirm", OnClick = _ => setModalVisible(false) }),
                            ]})
                        ]
                    })
                ]
            }));
        }

        // 后半部 children（常亮部分）
        children.AddRange(new List<Element>
        {
            SectionTitle("Color Palette"),
            // Row 1: Brand & Status
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal, Gap = 10, Padding = new Spacing(Dimension.Pixels(2)),
                Children =
                [
                    ColorSwatch("Primary", DesignTokens.Primary),
                    ColorSwatch("Hover", DesignTokens.PrimaryHover),
                    ColorSwatch("Active", DesignTokens.PrimaryActive),
                    ColorSwatch("Bg", DesignTokens.PrimaryBg),
                    ColorSwatch("Success", DesignTokens.Success),
                    ColorSwatch("Warning", DesignTokens.Warning),
                    ColorSwatch("Error", DesignTokens.Error),
                    ColorSwatch("Active", DesignTokens.ErrorActive),
                ]
            }),
            // Row 2: Text colors
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal, Gap = 10, Padding = new Spacing(Dimension.Pixels(2)),
                Children =
                [
                    ColorSwatch("Title", DesignTokens.TextTitle),
                    ColorSwatch("Body", DesignTokens.TextBody),
                    ColorSwatch("Secondary", DesignTokens.TextSecondary),
                    ColorSwatch("Disabled", DesignTokens.TextDisabled),
                    ColorSwatch("Inverse", DesignTokens.TextInverse),
                ]
            }),
            // Row 3: Background & Border
            Container(new ContainerProps
            {
                Direction = LayoutDirection.Horizontal, Gap = 10, Padding = new Spacing(Dimension.Pixels(2)),
                Children =
                [
                    ColorSwatch("Bg Main", DesignTokens.BgMain),
                    ColorSwatch("Bg Content", DesignTokens.BgContent),
                    ColorSwatch("Bg Disabled", DesignTokens.BgDisabled),
                    ColorSwatch("Border", DesignTokens.Border),
                    ColorSwatch("Hover", DesignTokens.BorderHover),
                    ColorSwatch("Focus", DesignTokens.BorderFocus),
                    ColorSwatch("Shadow Btn", DesignTokens.ShadowBtn),
                    ColorSwatch("Shadow Input", DesignTokens.ShadowInput),
                ]
            }),

            SectionTitle("About"),
            AICard("Animal Island UI", "\U0001F33F", DesignTokens.Primary,
                "A warm, rounded, playful UI design system built with EchoUI. " +
                "Inspired by soft natural colors, 3D button shadows, and friendly " +
                "interactions -- no harsh angles, no cold blacks.")
        });

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            FlexGrow = 1,
            Padding = new Spacing(Dimension.Pixels(30)),
            Direction = LayoutDirection.Vertical,
            Gap = 24,
            Overflow = Overflow.Auto,
            BackgroundColor = DesignTokens.BgMain,
            Children = children
        });
    }

    // ──────────────── Section Title ────────────────

    private static Element SectionTitle(string text)
    {
        return Text(new TextProps
        {
            Text = text,
            FontSize = 18,
            Color = DesignTokens.TextTitle,
            FontWeight = "700"
        });
    }

    // ──────────────── AIButton ────────────────

    public static Element AIButton(ButtonProps props)
    {
        var (isHovered, setIsHovered, _) = State(false);
        var (isPressed, setIsPressed, _) = State(false);

        var sz = DesignTokens.PrimaryButton(props.Size ?? "middle");
        var disabled = props.Disabled == true;

        var baseBg = props.BackgroundColor ?? DesignTokens.Primary;
        var bg = disabled ? DesignTokens.BgDisabled : baseBg;
        var textColor = props.TextColor ?? (disabled ? DesignTokens.TextDisabled : DesignTokens.TextInverse);
        var radius = props.BorderRadius ?? sz.Radius;

        var shadowBase = DesignTokens.ShadowBtn;
        float shadowH = 5f;

        if (!disabled)
        {
            if (isPressed.Value)
            {
                bg = props.PressedColor ?? DesignTokens.PrimaryActive;
                shadowH = 2f;
            }
            else if (isHovered.Value)
            {
                bg = props.HoverColor ?? DesignTokens.PrimaryHover;
                shadowH = 6f;
            }
        }

        var autoWidth = Hooks.MeasureText(new TextMeasurementRequest
        {
            Text = props.Text,
            FontSize = sz.FontSize,
            FontWeight = "600"
        }).Width + sz.PaddingX * 2 + 24f;

        var btnWidth = props.Width ?? Dimension.Pixels(autoWidth);
        var btnHeight = props.Height ?? Dimension.Pixels(sz.Height);
        const float maxShadow = 6f;

        var transitions = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
        {
            [nameof(ContainerProps.BackgroundColor)] = new(140, Easing.EaseOut),
            [nameof(ContainerProps.Shadow)] = new(140, Easing.EaseOut),
            [nameof(ContainerProps.Margin)] = new(140, Easing.EaseOut),
        });

        // translateY: 只在内部 margin 偏移，外层 layout 固定不挤压同级
        var topMargin = isPressed.Value
            ? Dimension.Pixels(2f)
            : isHovered.Value && !disabled
                ? Dimension.Pixels(-1f)
                : Dimension.Pixels(0f);

        // 外层容器：固定 layout footprint
        return Container(new ContainerProps
        {
            Key = props.Key,
            Width = btnWidth,
            Height = Dimension.Pixels(sz.Height + maxShadow),
            MinWidth = Dimension.Pixels(72),
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = btnHeight,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Padding = new Spacing(Dimension.Pixels(sz.PaddingX), Dimension.Pixels(0)),
                    BackgroundColor = bg,
                    BorderRadius = radius,
                    Shadow = new BoxShadow(shadowBase, shadowH),
                    Cursor = disabled ? "not-allowed" : null,
                    Opacity = disabled ? 0.5f : 1f,
                    Margin = new Spacing(topMargin, Dimension.Pixels(0), Dimension.Pixels(0), Dimension.Pixels(0)),
                    Transitions = transitions,
                    OnMouseEnter = disabled ? null : () => setIsHovered(true),
                    OnMouseLeave = () => { setIsHovered(false); setIsPressed(false); },
                    OnMouseDown = disabled ? null : () => setIsPressed(true),
                    OnMouseUp = disabled ? null : () => setIsPressed(false),
                    OnClick = disabled ? null : (Action<MouseButton>)(btn => props.OnClick?.Invoke(btn)),
                    Children =
                    [
                        Text(new TextProps
                        {
                            Text = props.Text,
                            Color = textColor,
                            FontSize = sz.FontSize,
                            FontWeight = "600",
                            NoWrap = true
                        })
                    ]
                })
            ]
        });
    }

    // ──────────────── AIInput ────────────────

    public static Element AIInput(InputProps props)
    {
        const float inputH = 40f;
        const float shadowH = 3f;

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(280),
            Height = Dimension.Pixels(inputH + shadowH),
            BackgroundColor = DesignTokens.ShadowInput,
            BorderRadius = 50,
            Direction = LayoutDirection.Vertical,
            JustifyContent = JustifyContent.Start,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Pixels(inputH),
                    FlexShrink = 0,
                    FlexGrow = 0,
                    BackgroundColor = props.BackgroundColor ?? DesignTokens.BgContent,
                    BorderStyle = BorderStyle.Solid,
                    BorderColor = props.BorderColor ?? DesignTokens.Border,
                    BorderWidth = 2.5f,
                    BorderRadius = 50,
                    Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(0)),
                    Children =
                    [
                        new Element(ElementCoreName.Input, new InputProps
                        {
                            Value = props.Value,
                            OnValueChanged = props.OnValueChanged,
                            BackgroundColor = Color.Transparent,
                            TextColor = DesignTokens.TextBody,
                            BorderColor = DesignTokens.Border,
                            FocusedBorderColor = DesignTokens.BorderFocus,
                        })
                    ]
                })
            ]
        });
    }

    // ──────────────── AITextInput ────────────────

    public static Element AITextInput(TextInputProps props)
    {
        const float inputH = 40f;
        const float shadowH = 3f;

        return Container(new ContainerProps
        {
            Width = props.Width ?? Dimension.Pixels(280),
            Height = Dimension.Pixels(inputH + shadowH),
            BackgroundColor = DesignTokens.ShadowInput,
            BorderRadius = 50,
            Direction = LayoutDirection.Vertical,
            JustifyContent = JustifyContent.Start,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(100),
                    Height = Dimension.Pixels(inputH),
                    FlexShrink = 0,
                    FlexGrow = 0,
                    Children =
                    [
                        TextInput(new TextInputProps
                        {
                            Value = props.Value,
                            OnValueChanged = props.OnValueChanged,
                            Placeholder = props.Placeholder,
                            Width = Dimension.Percent(100),
                            Height = Dimension.Pixels(inputH),
                            BackgroundColor = DesignTokens.BgContent,
                            TextColor = DesignTokens.TextBody,
                            PlaceholderColor = DesignTokens.TextDisabled,
                            BorderColor = DesignTokens.Border,
                            FocusedBorderColor = DesignTokens.BorderFocus,
                            CaretColor = DesignTokens.Primary,
                            BorderRadius = 50,
                            Padding = new Spacing(Dimension.Pixels(18), Dimension.Pixels(10)),
                            FontSize = 14,
                            FontWeight = "500"
                        })
                    ]
                })
            ]
        });
    }

    // ──────────────── AICard ────────────────

    public static Element AICard(string title, string emoji, Color accent, string description)
    {
        var shadowCol = new Color(107, 92, 67, 107);
        const float shadowH = 4f;

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(200),
            Padding = new Spacing(Dimension.Pixels(24)),
            BackgroundColor = DesignTokens.BgContent,
            BorderRadius = 20,
            Shadow = new BoxShadow(shadowCol, shadowH),
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(64),
                    Height = Dimension.Pixels(64),
                    BackgroundColor = accent.WithAlpha(25),
                    BorderRadius = 32,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Children =
                    [
                        Text(new TextProps { Text = emoji, FontSize = 28 })
                    ]
                }),
                Text(new TextProps
                {
                    Text = title,
                    FontSize = 16,
                    Color = DesignTokens.TextTitle,
                    FontWeight = "700"
                }),
                Text(new TextProps
                {
                    Text = description,
                    FontSize = 12,
                    Color = DesignTokens.TextSecondary,
                    FontWeight = "500",
                    NoWrap = false
                })
            ]
        });
    }

    // ──────────────── AIHorizontalCard ────────────────

    public static Element AIHorizontalCard(string emoji, string title, Color accent, string description)
    {
        var shadowCol = new Color(107, 92, 67, 85);
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(20)),
            BackgroundColor = DesignTokens.BgContent,
            BorderRadius = DesignTokens.RadiusBase,
            Shadow = new BoxShadow(shadowCol, 3f),
            Direction = LayoutDirection.Horizontal,
            Gap = 18,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(52),
                    Height = Dimension.Pixels(52),
                    BackgroundColor = accent.WithAlpha(25),
                    BorderRadius = 26,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    FlexShrink = 0,
                    Children =
                    [
                        Text(new TextProps { Text = emoji, FontSize = 26 })
                    ]
                }),
                Container(new ContainerProps
                {
                    Direction = LayoutDirection.Vertical,
                    Gap = 4,
                    FlexGrow = 1,
                    Children =
                    [
                        Text(new TextProps { Text = title, FontSize = 16, Color = DesignTokens.TextTitle, FontWeight = "700" }),
                        Text(new TextProps { Text = description, FontSize = 12, Color = DesignTokens.TextSecondary, FontWeight = "500", NoWrap = false }),
                    ]
                }),
                Text(new TextProps { Text = "\U000027A1", FontSize = 18, Color = DesignTokens.TextDisabled })
            ]
        });
    }

    // ──────────────── AIClickableCard ────────────────

    public static Element AIClickableCard(string emoji, string title, Color accent, string description)
    {
        var (isHov, setHov, _) = State(false);
        var shadowCol = new Color(107, 92, 67, 90);
        var hoverShadow = isHov.Value ? 6f : 3f;
        var hoverBg = isHov.Value ? accent.WithAlpha(35) : accent.WithAlpha(25);
        var transition = new ValueDictionary<string, Transition>(new Dictionary<string, Transition>
        {
            [nameof(ContainerProps.Shadow)] = new(180, Easing.EaseOut),
            [nameof(ContainerProps.BackgroundColor)] = new(180, Easing.EaseOut),
        });

        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(220),
            Padding = new Spacing(Dimension.Pixels(24)),
            BackgroundColor = DesignTokens.BgContent,
            BorderRadius = DesignTokens.RadiusLg,
            Shadow = new BoxShadow(shadowCol, hoverShadow),
            Direction = LayoutDirection.Vertical,
            Gap = 12,
            AlignItems = AlignItems.Center,
            Transitions = transition,
            OnMouseEnter = () => setHov(true),
            OnMouseLeave = () => setHov(false),
            OnClick = _ => { },
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(56),
                    Height = Dimension.Pixels(56),
                    BackgroundColor = hoverBg,
                    BorderRadius = 28,
                    JustifyContent = JustifyContent.Center,
                    AlignItems = AlignItems.Center,
                    Transitions = transition,
                    Children =
                    [
                        Text(new TextProps { Text = emoji, FontSize = 26 })
                    ]
                }),
                Text(new TextProps { Text = title, FontSize = 16, Color = DesignTokens.TextTitle, FontWeight = "700" }),
                Text(new TextProps { Text = description, FontSize = 12, Color = DesignTokens.TextSecondary, FontWeight = "500", NoWrap = false }),
            ]
        });
    }

    // ──────────────── AIAvatar ────────────────

    public static Element AIAvatar(string emoji, Color? backgroundColor = null, float size = 48, bool hasBorder = false)
    {
        var bg = backgroundColor ?? DesignTokens.PrimaryBg;
        var borderWidth = hasBorder ? 2.5f : 0f;
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(size),
            Height = Dimension.Pixels(size),
            BackgroundColor = bg,
            BorderRadius = size / 2,
            BorderWidth = borderWidth,
            BorderColor = DesignTokens.Border,
            BorderStyle = borderWidth > 0 ? BorderStyle.Solid : BorderStyle.None,
            JustifyContent = JustifyContent.Center,
            AlignItems = AlignItems.Center,
            FlexShrink = 0,
            Children =
            [
                Text(new TextProps { Text = emoji, FontSize = size * 0.45f })
            ]
        });
    }

    // ──────────────── AITag ────────────────

    public static Element AITag(string label, Color? backgroundColor = null, Color? textColor = null)
    {
        var bg = backgroundColor ?? DesignTokens.PrimaryBg;
        var fg = textColor ?? DesignTokens.Primary;
        return Container(new ContainerProps
        {
            Padding = new Spacing(Dimension.Pixels(12), Dimension.Pixels(8)),
            BackgroundColor = bg,
            BorderRadius = DesignTokens.RadiusPill,
            Children =
            [
                Text(new TextProps
                {
                    Text = label,
                    FontSize = 12,
                    Color = fg,
                    FontWeight = "600",
                    NoWrap = true
                })
            ]
        });
    }

    // ──────────────── AIProgressBar ────────────────

    public static Element AIProgressBar(float value, Color? color = null, float height = 8)
    {
        var barColor = color ?? DesignTokens.Primary;
        var clamped = Math.Clamp(value, 0, 100);
        var trackBg = DesignTokens.BgDisabled;

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Pixels(height),
            BackgroundColor = trackBg,
            BorderRadius = height / 2,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Percent(clamped),
                    Height = Dimension.Pixels(height),
                    BackgroundColor = barColor,
                    BorderRadius = height / 2,
                    Transitions =
                    [
                        [nameof(ContainerProps.Width), new Transition(300, Easing.EaseOut)]
                    ],
                })
            ]
        });
    }

    // ──────────────── ColorSwatch ────────────────

    private static Element ColorSwatch(string label, Color color)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Pixels(72),
            Direction = LayoutDirection.Vertical,
            Gap = 6,
            AlignItems = AlignItems.Center,
            Children =
            [
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(48),
                    Height = Dimension.Pixels(48),
                    BackgroundColor = color,
                    BorderRadius = DesignTokens.RadiusSm,
                    BorderColor = DesignTokens.Border,
                    BorderWidth = 1f,
                    BorderStyle = BorderStyle.Solid,
                }),
                Text(new TextProps
                {
                    Text = label,
                    FontSize = 10,
                    Color = DesignTokens.TextSecondary,
                    FontWeight = "500",
                    NoWrap = false
                })
            ]
        });
    }
}
