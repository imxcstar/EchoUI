using EchoUI.Core;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

namespace EchoUI.Tools.Serial;

public static class SerialPage
{
    private class PortHolder
    {
        public SerialPort? Port;
    }

    public static Element Create(Props props)
    {
        // Global State for the page
        var (ports, setPorts, _) = State(SerialPort.GetPortNames().ToList());
        var (selectedPortIndex, setSelectedPortIndex, _) = State(0);
        
        var (baudRateIndex, setBaudRateIndex, _) = State(5); // Default 9600
        var baudRates = new List<string> { "300", "600", "1200", "2400", "4800", "9600", "14400", "19200", "38400", "57600", "115200" };

        var (dataBitsIndex, setDataBitsIndex, _) = State(3); // 8
        var dataBitsOptions = new List<string> { "5", "6", "7", "8" };

        var (stopBitsIndex, setStopBitsIndex, _) = State(1); // One (Index 1)
        var stopBitsOptions = Enum.GetNames(typeof(StopBits)).ToList(); 

        var (parityIndex, setParityIndex, _) = State(0); // None
        var parityOptions = Enum.GetNames(typeof(Parity)).ToList(); 

        var (isOpen, setIsOpen, _) = State(false);
        var (receivedData, setReceivedData, updateReceivedData) = State(new StringBuilder());
        var (sendText, setSendText, _) = State("");
        var (hexDisplay, setHexDisplay, _) = State(false);
        var (hexSend, setHexSend, _) = State(false);
        
        var portHolder = Memo(() => new PortHolder(), new object[] { });

        // Effect for Serial Port Logic
        Effect(() =>
        {
            if (!isOpen.Value) return null;

            string portName = "";
            int baud = 9600;
            
            if (ports.Value.Count == 0 || selectedPortIndex.Value < 0)
            {
                setIsOpen(false);
                return null;
            }

            int pIndex = selectedPortIndex.Value;
            // Bound check for safety
            if (pIndex >= ports.Value.Count) pIndex = 0;
            // If ports empty, already handled above
            portName = ports.Value[pIndex];
            
            if (baudRateIndex.Value >= 0 && baudRateIndex.Value < baudRates.Count)
                baud = int.Parse(baudRates[baudRateIndex.Value]);

            SerialPort? serialPort = null;
            try
            {
                serialPort = new SerialPort(portName, baud);
                serialPort.DataBits = int.Parse(dataBitsOptions[dataBitsIndex.Value]);
                serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopBitsOptions[stopBitsIndex.Value]);
                serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), parityOptions[parityIndex.Value]);
                
                serialPort.Open();
                portHolder.Port = serialPort; 
            }
            catch(Exception ex)
            {
                setIsOpen(false);
                updateReceivedData(sb => sb.AppendLine($"Error: {ex.Message}"));
                return null;
            }

            var syncContext = SynchronizationContext.Current;
            
            void DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                var sp = (SerialPort)sender;
                try 
                {
                    int bytesToRead = sp.BytesToRead;
                    byte[] buffer = new byte[bytesToRead];
                    sp.Read(buffer, 0, bytesToRead);
                    
                    if (buffer.Length > 0)
                    {
                        string textToAdd;
                        // Determine display format based on CURRENT state when data arrives
                        // Note: hexDisplay.Value inside this callback might be stale if closure captured old state logic?
                        // Actually, DataReceived runs on threadpool. accessing hexDisplay.Value (which is Ref<bool>) is valid but thread-safety?
                        // Ref<T> is just a class wrapper. Reading bool is atomic.
                        // However, we want the latest value. hexDisplay Ref is stable.
                        if (hexDisplay.Value)
                        {
                            textToAdd = BitConverter.ToString(buffer).Replace("-", " ") + " ";
                        }
                        else
                        {
                            textToAdd = Encoding.UTF8.GetString(buffer);
                        }
                        syncContext?.Post(_ => updateReceivedData(sb => sb.Append(textToAdd)), null);
                    }
                }
                catch { }
            }

            serialPort.DataReceived += DataReceived;
            updateReceivedData(sb => sb.AppendLine($"Connected to {portName} at {baud} ({dataBitsOptions[dataBitsIndex.Value]}{parityOptions[parityIndex.Value][0]}{stopBitsOptions[stopBitsIndex.Value]})."));

            return () =>
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.DataReceived -= DataReceived;
                    serialPort.Close();
                    syncContext?.Post(_ => updateReceivedData(sb => sb.AppendLine($"Disconnected.")), null);
                }
                if (serialPort != null) serialPort.Dispose();
                portHolder.Port = null;
            };
        }, new object[] { isOpen.Value, selectedPortIndex.Value, baudRateIndex.Value, ports.Value, dataBitsIndex.Value, stopBitsIndex.Value, parityIndex.Value }); 
        // Note: hexDisplay removed from Effect deps so toggling it doesn't reconnect!
        // But that means existing data isn't re-rendered. New data will follow new format.
        // User might expect "View changes" but that requires storing raw Buffer. 
        // For now, "New data follows format" is acceptable for simple tool.
        // If I want to change view of OLD data, I would need a different architecture.

        void SendData()
        {
            var text = sendText.Value;
            if (string.IsNullOrEmpty(text)) return;
            
            var port = portHolder.Port;
            if (port != null && port.IsOpen)
            {
                try
                {
                    if (hexSend.Value)
                    {
                        // Parse Hex
                        string[] hexValues = text.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        byte[] bytes = hexValues.Select(h => Convert.ToByte(h, 16)).ToArray();
                        port.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        port.Write(text);
                    }
                    setSendText("");
                }
                catch (Exception ex)
                {
                    updateReceivedData(sb => sb.AppendLine($"Send Error: {ex.Message}"));
                }
            }
        }

        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            Direction = LayoutDirection.Horizontal,
            BackgroundColor = Color.FromHex("#F3F3F3"),
            Children = 
            [
                // Sidebar
                Container(new ContainerProps
                {
                    Width = Dimension.Pixels(250),
                    Height = Dimension.Percent(100),
                    BackgroundColor = Color.White,
                    Padding = new Spacing(Dimension.Pixels(20)),
                    Gap = 10,
                    BorderWidth = 1,
                    BorderColor = Color.LightGray, 
                    Children = [
                        Text(new TextProps { Text = "Setup", FontSize = 18, FontWeight = "Bold", Color = Color.FromHex("#333333") }),
                        
                        Text(new TextProps { Text = "Port", FontSize = 12, Color = Color.Gray }),
                        Container(new ContainerProps {
                             Direction = LayoutDirection.Horizontal, Gap=5, Children=[
                                 Container(new ContainerProps { Width=Dimension.Percent(80), Children=[
                                     ComboBox(new ComboBoxProps { Options = ports.Value.Count > 0 ? ports.Value : new List<string>{"None"}, SelectedIndex = selectedPortIndex.Value, OnSelectionChanged = idx => setSelectedPortIndex(idx) })
                                 ]}),
                                 Button(new ButtonProps { Text="R", Width=Dimension.Pixels(30), BackgroundColor=Color.LightGray, OnClick=_=> setPorts(SerialPort.GetPortNames().ToList()) })
                             ]
                        }),

                        Text(new TextProps { Text = "Baud Rate", FontSize = 12, Color = Color.Gray }),
                        ComboBox(new ComboBoxProps { Options = baudRates, SelectedIndex = baudRateIndex.Value, OnSelectionChanged = idx => setBaudRateIndex(idx) }),

                        Text(new TextProps { Text = "Data Bits", FontSize = 12, Color = Color.Gray }),
                        ComboBox(new ComboBoxProps { Options = dataBitsOptions, SelectedIndex = dataBitsIndex.Value, OnSelectionChanged = idx => setDataBitsIndex(idx) }),

                        Text(new TextProps { Text = "Stop Bits", FontSize = 12, Color = Color.Gray }),
                        ComboBox(new ComboBoxProps { Options = stopBitsOptions, SelectedIndex = stopBitsIndex.Value, OnSelectionChanged = idx => setStopBitsIndex(idx) }),

                        Text(new TextProps { Text = "Parity", FontSize = 12, Color = Color.Gray }),
                        ComboBox(new ComboBoxProps { Options = parityOptions, SelectedIndex = parityIndex.Value, OnSelectionChanged = idx => setParityIndex(idx) }),

                        Container(new ContainerProps { Height = Dimension.Pixels(10) }),

                        Button(new ButtonProps {
                            Text = isOpen.Value ? "Close" : "Open",
                            BackgroundColor = isOpen.Value ? Color.FromHex("#EF4444") : Color.FromHex("#10B981"),
                            TextColor = Color.White,
                            Height = Dimension.Pixels(40),
                            BorderRadius = 4,
                            OnClick = _ => setIsOpen(!isOpen.Value)
                        })
                    ]
                }),
                
                // Main Content
                Container(new ContainerProps
                {
                    FlexGrow = 1,
                    Height = Dimension.Percent(100),
                    Padding = new Spacing(Dimension.Pixels(10)),
                    Gap = 10,
                    Children = [
                         // Controls
                         Container(new ContainerProps {
                             Direction = LayoutDirection.Horizontal,
                             Gap = 10,
                             AlignItems = AlignItems.Center,
                             Children = [
                                 Button(new ButtonProps { Text = "Clear", Width = Dimension.Pixels(60), OnClick = _ => setReceivedData(new StringBuilder()) }),
                                 CheckBox(new CheckBoxProps { Label = "Hex Display", IsChecked = hexDisplay.Value, OnToggle = v => setHexDisplay(v) })
                             ]
                         }),

                         // Received Data Area
                         Container(new ContainerProps {
                             FlexGrow = 1,
                             Width = Dimension.Percent(100),
                             BackgroundColor = Color.White,
                             BorderWidth = 1,
                             BorderColor = Color.LightGray,
                             BorderRadius = 4,
                             Padding = new Spacing(Dimension.Pixels(5)),
                             Overflow = Overflow.Scroll,
                             Children = [
                                 Text(new TextProps { 
                                     Text = receivedData.Value.ToString(), 
                                     FontFamily = "Consolas, monospace",
                                     FontSize = 13
                                 })
                             ]
                         }),

                         // Send Area
                         Container(new ContainerProps {
                             Height = Dimension.Pixels(40),
                             Direction = LayoutDirection.Horizontal,
                             Gap = 5,
                             Children = [
                                 CheckBox(new CheckBoxProps { Label = "Hex", IsChecked = hexSend.Value, OnToggle = v => setHexSend(v) }),
                                 Container(new ContainerProps {
                                     FlexGrow = 1,
                                     BackgroundColor = Color.White,
                                     BorderWidth = 1,
                                     BorderColor = Color.LightGray,
                                     BorderRadius = 4,
                                     Padding = new Spacing(Dimension.Pixels(5)),
                                     Children = [
                                         Input(new InputProps {
                                             Value = sendText.Value,
                                             OnValueChanged = v => setSendText(v),
                                             BackgroundColor = Color.Transparent
                                         })
                                     ]
                                 }),
                                 Button(new ButtonProps {
                                     Text = "Send",
                                     Width = Dimension.Pixels(60),
                                     BackgroundColor = Color.FromHex("#3B82F6"),
                                     TextColor = Color.White,
                                     BorderRadius = 4,
                                     OnClick = _ => SendData()
                                 })
                             ]
                         })
                    ]
                })
            ]
        });
    }
}
