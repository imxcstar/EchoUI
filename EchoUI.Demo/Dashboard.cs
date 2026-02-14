using EchoUI.Core;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

namespace EchoUI.Demo
{
    public static class Dashboard
    {
        public static Element Create(Props props)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Height = Dimension.ViewportHeight(100),
                Direction = LayoutDirection.Horizontal,
                BackgroundColor = Color.FromHex("#f3f4f6"), // Light gray background
                Children =
                [
                    // Sidebar
                    Sidebar(),

                    // Main Content
                    Container(new ContainerProps
                    {
                        FlexGrow = 1,
                        FlexShrink = 1,
                        // Remove fixed width/height to let flex handle it, or keep height 100%
                        Height = Dimension.Percent(100),
                        Padding = new Spacing(Dimension.Pixels(30)),
                        Direction = LayoutDirection.Vertical,
                        Gap = 30,
                        Children =
                        [
                            // Header
                            Header(),

                            // Stats Row
                            Container(new ContainerProps
                            {
                                Direction = LayoutDirection.Horizontal,
                                Width = Dimension.Percent(100),
                                Gap = 20,
                                Children = 
                                [
                                    StatCard("Total Users", "12,345", Color.FromHex("#4f46e5")),
                                    StatCard("Active Sessions", "842", Color.FromHex("#10b981")),
                                    StatCard("Server Load", "24%", Color.FromHex("#f59e0b"))
                                ]
                            }),

                            // Form Section
                            UserForm()
                        ]
                    })
                ]
            });
        }

        private static Element Sidebar()
        {
            var (activeItem, setActiveItem, _) = State("Dashboard");

            return Container(new ContainerProps
            {
                Width = Dimension.Pixels(250),
                Height = Dimension.Percent(100),
                FlexShrink = 0,
                BackgroundColor = Color.FromHex("#1f2937"), // Dark gray
                Padding = new Spacing(Dimension.Pixels(20)),
                Direction = LayoutDirection.Vertical,
                Gap = 10,
                Children =
                [
                    // Logo
                    Text(new TextProps 
                    { 
                        Text = "EchoUI Demo", 
                        Color = Color.White, 
                        FontSize = 24, 
                        FontFamily = "Segoe UI" 
                    }),
                    
                    Container(new ContainerProps { Height = Dimension.Pixels(30) }), // Spacer

                    // Menu Items
                    MenuItem("Dashboard", activeItem.Value == "Dashboard", () => setActiveItem("Dashboard")),
                    MenuItem("Analytics", activeItem.Value == "Analytics", () => setActiveItem("Analytics")),
                    MenuItem("Settings", activeItem.Value == "Settings", () => setActiveItem("Settings")),
                    MenuItem("Documentation", activeItem.Value == "Documentation", () => setActiveItem("Documentation")),
                ]
            });
        }

        private static Element MenuItem(string title, bool active, Action onClick)
        {
            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                Padding = new Spacing(Dimension.Pixels(12)),
                BackgroundColor = active ? Color.FromHex("#374151") : null,
                BorderRadius = 6,
                OnClick = _ => onClick(),
                Children = [
                    Text(new TextProps 
                    { 
                        Text = title, 
                        Color = active ? Color.White : Color.FromHex("#9ca3af"),
                        FontSize = 14
                    })
                ]
            });
        }

        private static Element Header()
        {
            return Container(new ContainerProps
            {
                Children = [
                    Text(new TextProps 
                    { 
                        Text = "Dashboard Overview", 
                        Color = Color.FromHex("#111827"), 
                        FontSize = 28, 
                        FontFamily = "Segoe UI" 
                    }),
                    Text(new TextProps 
                    { 
                        Text = "Welcome back, Administrator.", 
                        Color = Color.FromHex("#6b7280"), 
                        FontSize = 14 
                    })
                ]
            });
        }

        private static Element StatCard(string title, string value, Color accentColor)
        {
            return Container(new ContainerProps
            {
                // Flex grow logic not fully exposed in Dimension yet, simulate with % or generic
                Width = Dimension.Percent(33), 
                BackgroundColor = Color.White,
                Padding = new Spacing(Dimension.Pixels(20)),
                BorderRadius = 8,
                // Shadow not supported directly, simulate with border/color
                BorderWidth = 1,
                BorderColor = Color.FromHex("#e5e7eb"),
                Children = 
                [
                    Text(new TextProps { Text = title, Color = Color.FromHex("#6b7280"), FontSize = 14 }),
                    Container(new ContainerProps { Height = Dimension.Pixels(8) }),
                    Text(new TextProps { Text = value, Color = Color.FromHex("#111827"), FontSize = 30 }),
                    Container(new ContainerProps 
                    { 
                        Height = Dimension.Pixels(4), 
                        Width = Dimension.Percent(100), 
                        BackgroundColor = accentColor,
                        Margin = new Spacing(Dimension.Pixels(0), Dimension.Pixels(10), Dimension.Pixels(0), Dimension.Pixels(0)),
                        BorderRadius = 2
                    })
                ]
            });
        }

        private static Element UserForm()
        {
            var (username, setUsername, _) = State("");
            var (roleIndex, setRoleIndex, _) = State(0);

            return Container(new ContainerProps
            {
                Width = Dimension.Percent(100),
                BackgroundColor = Color.White,
                Padding = new Spacing(Dimension.Pixels(24)),
                BorderRadius = 8,
                BorderWidth = 1,
                BorderColor = Color.FromHex("#e5e7eb"),
                Gap = 20,
                Children =
                [
                    Text(new TextProps { Text = "Add New User", FontSize = 18, Color = Color.Black }),

                    // Grid-like layout for form
                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        Gap = 20,
                        Children =
                        [
                            // Left Column
                            Container(new ContainerProps
                            {
                                Width = Dimension.Percent(50),
                                Gap = 15,
                                Children = 
                                [
                                    Label("Username"),
                                    Container(new ContainerProps 
                                    { 
                                        Height = Dimension.Pixels(38),
                                        BorderWidth = 1,
                                        BorderColor = Color.FromHex("#d1d5db"),
                                        BorderRadius = 4,
                                        Padding = new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
                                        Children = [
                                            Input(new InputProps { Value = username.Value, OnValueChanged = v => setUsername(v) })
                                        ]
                                    }),

                                    Label("Role"),
                                    ComboBox(new ComboBoxProps
                                    {
                                        Options = ["Administrator", "Editor", "Viewer", "Guest"],
                                        SelectedIndex = roleIndex.Value,
                                        OnSelectionChanged = v => setRoleIndex(v),
                                        BorderColor = Color.FromHex("#d1d5db"),
                                        BackgroundColor = Color.White
                                    })
                                ]
                            }),

                            // Right Column
                            Container(new ContainerProps
                            {
                                Width = Dimension.Percent(50),
                                Gap = 15,
                                Children = 
                                [
                                    Label("Status"),
                                    RadioGroup(new RadioGroupProps
                                    {
                                        Options = ["Active", "Pending", "Suspended"],
                                        Direction = LayoutDirection.Horizontal,
                                        SelectedColor = Color.FromHex("#4f46e5")
                                    }),

                                    Label("Settings"),
                                    Container(new ContainerProps
                                    {
                                        Direction = LayoutDirection.Horizontal,
                                        AlignItems = AlignItems.Center,
                                        Gap = 10,
                                        Children = 
                                        [
                                            Switch(new SwitchProps { DefaultIsOn = true, OnColor = Color.FromHex("#4f46e5") }),
                                            Text(new TextProps { Text = "Send Welcome Email", Color = Color.FromHex("#374151") })
                                        ]
                                    })
                                ]
                            })
                        ]
                    }),

                    Container(new ContainerProps
                    {
                        Direction = LayoutDirection.Horizontal,
                        JustifyContent = JustifyContent.End,
                        Children = 
                        [
                            Button(new ButtonProps
                            {
                                Text = "Create User",
                                BackgroundColor = Color.FromHex("#4f46e5"),
                                TextColor = Color.White,
                                Width = Dimension.Pixels(120),
                                Height = Dimension.Pixels(40),
                                BorderRadius = 4,
                                OnClick = _ => { /* Submit logic */ }
                            })
                        ]
                    })
                ]
            });
        }

        private static Element Label(string text)
        {
            return Text(new TextProps { Text = text, Color = Color.FromHex("#374151"), FontSize = 14 });
        }
    }
}
