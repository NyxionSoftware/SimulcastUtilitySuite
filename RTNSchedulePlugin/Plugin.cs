using RTNSchedulePlugin.Models;
using RTNSchedulePlugin.Services;
using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RTNSchedulePlugin
{
    public sealed class Plugin : ISimulcastPlugin, IDisposable
    {
        private readonly List<RcnScheduleItem> _scheduleItems = [];

        private IPluginContext? _pluginContext;
        private IReceiverControllerService? _receiverController;
        private Receiver? _selectedReceiver;

        private FrameworkElement? _pluginContent;
        private ItemsControl? _pluginContentItemsControl;

        private DataGrid? _scheduleGrid;
        private TextBlock? _statusText;
        private TextBlock? _scheduleTitle;
        private Button? _refreshButton;
        private CheckBox? _useRtnInformationCheckBox;

        private DateTime _lastReceiverRefreshTime = DateTime.MinValue;
        private bool _disposed;

        public string Name => "RTN Schedule Plugin";

        public string Description => "Displays the current RTN simulcast schedule and provides receiver channel controls.";

        public void OnPluginInitialized()
        {

        }

        public void OnPluginContextInitialized(IPluginContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _pluginContext = context;
            _receiverController = context.ReceiverControllerService;
            _selectedReceiver = _receiverController.SelectedReceiver;

            _receiverController.SelectedReceiverChanged += ReceiverController_SelectedReceiverChanged;
            _receiverController.ReceiverUpdated += ReceiverController_ReceiverUpdated;
            _receiverController.ReceiverEPGRefreshed += _receiverController_ReceiverEPGRefreshed;

            context.Dispatcher.Invoke(() =>
            {
                AddPluginContent();
                AddUseRtnInformationCheckBox();

            });
        }

        private void _receiverController_ReceiverEPGRefreshed(object? sender, ReceiverEventArgs e)
        {
            if (_selectedReceiver is null)
                return;

            if (!ReferenceEquals(e.Receiver, _selectedReceiver) &&
                e.Receiver.ReceiverId != _selectedReceiver.ReceiverId)
            {
                return;
            }

            _selectedReceiver = e.Receiver;

            ApplyRtnInformationToSelectedReceiver();
        }

        private void AddPluginContent()
        {
            if (_pluginContext is null)
                return;

            _pluginContentItemsControl =
                _pluginContext.MainWindow.FindName("PluginContentItemsControl")
                as ItemsControl;

            if (_pluginContentItemsControl is null)
            {
                Debug.WriteLine(
                    $"{Name}: Could not find PluginContentItemsControl.");

                return;
            }

            if (_pluginContent is not null)
                return;

            _pluginContent = CreateScheduleCard();

            _pluginContentItemsControl.Items.Add(_pluginContent);
        }

        private FrameworkElement CreateScheduleCard()
        {
            Border card = new()
            {
                Margin = new Thickness(0, 0, 0, 18)
            };

            card.SetResourceReference(
                FrameworkElement.StyleProperty,
                "CardBorderStyle");

            Grid layout = new();

            layout.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            layout.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            layout.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            FrameworkElement header = CreateHeader();
            FrameworkElement statusArea = CreateStatusArea();
            FrameworkElement scheduleGrid = CreateScheduleGrid();

            Grid.SetRow(header, 0);
            Grid.SetRow(statusArea, 1);
            Grid.SetRow(scheduleGrid, 2);

            layout.Children.Add(header);
            layout.Children.Add(statusArea);
            layout.Children.Add(scheduleGrid);

            card.Child = layout;
            card.Loaded += ScheduleCard_Loaded;

            return card;
        }

        private async void ScheduleCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
                element.Loaded -= ScheduleCard_Loaded;

            await LoadScheduleAsync();
        }

        private FrameworkElement CreateHeader()
        {
            Grid header = new()
            {
                Margin = new Thickness(0, 0, 0, 14)
            };

            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            StackPanel textPanel = new();

            TextBlock title = new()
            {
                Text = "RCN Simulcast Schedule"
            };

            title.SetResourceReference(
                FrameworkElement.StyleProperty,
                "SectionTitleTextStyle");

            _scheduleTitle = new TextBlock
            {
                Text = "Loading today's schedule...",
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0)
            };

            _scheduleTitle.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            textPanel.Children.Add(title);
            textPanel.Children.Add(_scheduleTitle);

            _refreshButton = new Button
            {
                Content = "Refresh",
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 90
            };

            _refreshButton.SetResourceReference(
                FrameworkElement.StyleProperty,
                "SecondaryButtonStyle");

            _refreshButton.Click += RefreshButton_Click;

            Grid.SetColumn(textPanel, 0);
            Grid.SetColumn(_refreshButton, 1);

            header.Children.Add(textPanel);
            header.Children.Add(_refreshButton);

            return header;
        }

        private FrameworkElement CreateStatusArea()
        {
            _statusText = new TextBlock
            {
                Text = "Loading schedule...",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _statusText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "SecondaryTextBrush");

            return _statusText;
        }

        private FrameworkElement CreateScheduleGrid()
        {
            _scheduleGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                CanUserResizeRows = false,
                CanUserResizeColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                MaxHeight = 350,
                MinHeight = 100,
                ItemsSource = _scheduleItems
            };

            TryApplyHostStyle(
                _scheduleGrid,
                "PluginDataGridStyle");

            _scheduleGrid.RowStyle = CreateRowStyle();

            Style centeredCellStyle = CreateCenteredCellStyle();

            Style centeredHeaderStyle = new(
                typeof(DataGridColumnHeader),
                _scheduleGrid.ColumnHeaderStyle);

            centeredHeaderStyle.Setters.Add(new Setter(
                Control.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Center));

            _scheduleGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "CHANNEL #",
                Binding = new Binding(nameof(RcnScheduleItem.ChannelNumber)),
                Width = new DataGridLength(105),
                HeaderStyle = centeredHeaderStyle,
                ElementStyle = centeredCellStyle
            });

            _scheduleGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "EVENT NAME",
                Binding = new Binding(nameof(RcnScheduleItem.EventName)),
                Width = new DataGridLength(
                    1,
                    DataGridLengthUnitType.Star)
            });

            _scheduleGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "START TIME",
                Binding = new Binding(nameof(RcnScheduleItem.FormattedStartTime)),
                Width = new DataGridLength(135),
                HeaderStyle = centeredHeaderStyle,
                ElementStyle = centeredCellStyle
            });

            _scheduleGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "END TIME",
                Binding = new Binding(nameof(RcnScheduleItem.FormattedEndTime)),
                Width = new DataGridLength(135),
                HeaderStyle = centeredHeaderStyle,
                ElementStyle = centeredCellStyle
            });

            ScrollViewer.SetVerticalScrollBarVisibility(
                _scheduleGrid,
                ScrollBarVisibility.Auto);

            ScrollViewer.SetHorizontalScrollBarVisibility(
                _scheduleGrid,
                ScrollBarVisibility.Disabled);

            return _scheduleGrid;
        }

        private Style CreateRowStyle()
        {
            Style style = new(typeof(DataGridRow));

            EventSetter rightClickSetter = new(
                DataGridRow.PreviewMouseRightButtonDownEvent,
                new MouseButtonEventHandler(DataGridRow_RightClick));

            style.Setters.Add(rightClickSetter);

            ContextMenu contextMenu = new();

            TryApplyHostStyle(
                contextMenu,
                "ModernContextMenuStyle");

            MenuItem copyChannelItem = new()
            {
                Header = "Copy Channel"
            };

            TryApplyHostStyle(
                copyChannelItem,
                "ModernMenuItemStyle");

            copyChannelItem.Click += CopyChannel_Click;

            MenuItem setChannelItem = new()
            {
                Header = "Set Channel"
            };

            TryApplyHostStyle(
                setChannelItem,
                "ModernMenuItemStyle");

            setChannelItem.Click += SetChannel_Click;

            contextMenu.Items.Add(copyChannelItem);
            contextMenu.Items.Add(setChannelItem);

            style.Setters.Add(new Setter(
                FrameworkElement.ContextMenuProperty,
                contextMenu));

            return style;
        }

        private static Style CreateCenteredCellStyle()
        {
            Style style = new(typeof(TextBlock));

            style.Setters.Add(new Setter(
                TextBlock.TextAlignmentProperty,
                TextAlignment.Center));

            style.Setters.Add(new Setter(
                FrameworkElement.HorizontalAlignmentProperty,
                HorizontalAlignment.Stretch));

            return style;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
                window.Loaded -= MainWindow_Loaded;

            AddUseRtnInformationCheckBox();
        }

        private static T? FindVisualChildByName<T>(
    DependencyObject parent,
    string name)
    where T : FrameworkElement
        {
            int childCount =
                System.Windows.Media.VisualTreeHelper
                    .GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child =
                    System.Windows.Media.VisualTreeHelper
                        .GetChild(parent, i);

                if (child is T element &&
                    element.Name == name)
                {
                    return element;
                }

                T? result =
                    FindVisualChildByName<T>(
                        child,
                        name);

                if (result is not null)
                    return result;
            }

            return null;
        }

        private static T? FindVisualParent<T>(
            DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject? current =
                System.Windows.Media.VisualTreeHelper
                    .GetParent(child);

            while (current is not null)
            {
                if (current is T matchingParent)
                    return matchingParent;

                current =
                    System.Windows.Media.VisualTreeHelper
                        .GetParent(current);
            }

            return null;
        }

        private void AddUseRtnInformationCheckBox()
        {
            if (_pluginContext is null)
                return;

            Window mainWindow = _pluginContext.MainWindow;

            if (!mainWindow.IsLoaded)
            {
                mainWindow.Loaded -= MainWindow_Loaded;
                mainWindow.Loaded += MainWindow_Loaded;
                return;
            }

            _pluginContext.Dispatcher.BeginInvoke(() =>
            {
                ProgressBar? progressBar = FindVisualChildByName<ProgressBar>(mainWindow, "NowPlayingProgressBar");

                if (progressBar is null)
                {
                    Debug.WriteLine(
                        $"{Name}: Could not find NowPlayingProgressBar.");

                    return;
                }

                Grid? nowPlayingGrid = FindVisualParent<Grid>(progressBar);

                if (nowPlayingGrid is null)
                {
                    Debug.WriteLine(
                        $"{Name}: Could not find a parent Grid for NowPlayingProgressBar.");

                    return;
                }

                CheckBox? existingCheckBox =
                    nowPlayingGrid.Children
                        .OfType<CheckBox>()
                        .FirstOrDefault(
                            checkBox =>
                                checkBox.Name ==
                                "UseRtnInformationCheckBox");

                if (existingCheckBox is not null)
                {
                    _useRtnInformationCheckBox = existingCheckBox;
                    return;
                }

                int rowIndex = nowPlayingGrid.RowDefinitions.Count;

                nowPlayingGrid.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = GridLength.Auto
                    });

                _useRtnInformationCheckBox = new CheckBox
                {
                    Name = "UseRtnInformationCheckBox",
                    Content = "Use RTN Information",
                    Margin = new Thickness(0, 15, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = false
                };

                _useRtnInformationCheckBox.SetResourceReference(
                    FrameworkElement.StyleProperty,
                    "ModernCheckBoxStyle");

                _useRtnInformationCheckBox.Checked +=
                    UseRtnInformationCheckBox_CheckedChanged;

                _useRtnInformationCheckBox.Unchecked +=
                    UseRtnInformationCheckBox_CheckedChanged;

                Grid.SetRow(
                    _useRtnInformationCheckBox,
                    rowIndex);

                Grid.SetColumnSpan(
                    _useRtnInformationCheckBox,
                    Math.Max(1, nowPlayingGrid.ColumnDefinitions.Count));

                nowPlayingGrid.Children.Add(
                    _useRtnInformationCheckBox);
            }, DispatcherPriority.Loaded);
        }

        private void ReceiverController_SelectedReceiverChanged(
            object? sender,
            ReceiverEventArgs e)
        {
            _selectedReceiver = e.Receiver;

            ApplyRtnInformationToSelectedReceiver();
        }

        private void ReceiverController_ReceiverUpdated(object? sender, ReceiverUpdatedEventArgs e)
        {
            if (_selectedReceiver is null)
                return;

            if (!ReferenceEquals(e.Receiver, _selectedReceiver) &&
                e.Receiver.ReceiverId != _selectedReceiver.ReceiverId)
            {
                return;
            }

            _selectedReceiver = e.Receiver;

            ApplyRtnInformationToSelectedReceiver();
        }

        private static TimeSpan GetDistanceFromEvent(
            RcnScheduleItem item,
            DateTime currentTime)
        {
            DateTime startTime = item.StartTime!.Value;

            DateTime endTime = item.Duration.HasValue
                ? startTime.Add(item.Duration.Value)
                : startTime;

            // The event is currently airing.
            if (currentTime >= startTime && currentTime < endTime)
                return TimeSpan.Zero;

            // The event has not started yet.
            if (currentTime < startTime)
                return startTime - currentTime;

            // The event has already ended.
            return currentTime - endTime;
        }

        private void ApplyRtnInformationToSelectedReceiver()
        {
            if (_useRtnInformationCheckBox?.IsChecked != true)
                return;

            Receiver? receiver = _selectedReceiver;

            if (receiver is null)
                return;

            int rcnChannel = receiver.Channel - 100;

            RcnScheduleItem? scheduleItem = _scheduleItems
                .Where(item =>
                    int.TryParse(item.ChannelNumber, out int channel) &&
                    channel == rcnChannel &&
                    item.StartTime.HasValue)
                .OrderBy(item => GetDistanceFromEvent(item, DateTime.Now))
                .ThenBy(item => item.StartTime)
                .FirstOrDefault();

            if (scheduleItem is null)
            {
                receiver.ChannelName = "Unknown Event";
                receiver.ChannelStartTime = null;
                receiver.ChannelDuration = null;
                receiver.ChannelEndTime = null;

                return;
            }

            receiver.ChannelName = scheduleItem.EventName;

            if (scheduleItem.StartTime is DateTime startTime)
                receiver.ChannelStartTime = startTime.ToUniversalTime();
            else
                receiver.ChannelStartTime = null;

            receiver.ChannelDuration = scheduleItem.Duration;

            if (scheduleItem.StartTime is DateTime eventStart &&
                scheduleItem.Duration is TimeSpan duration)
            {
                receiver.ChannelEndTime = eventStart
                    .Add(duration)
                    .ToUniversalTime();
            }
            else
            {
                receiver.ChannelEndTime = null;
            }
        }

        private async Task RestoreReceiverInformationAsync()
        {
            if (_receiverController is null || _selectedReceiver is null)
                return;

            try
            {
                await _receiverController.RefreshReceiverAsync(_selectedReceiver, RefreshBehavior.WaitForRefreshWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to restore receiver information: {ex}");
            }
        }

        private async void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadScheduleAsync();
        }

        private async void UseRtnInformationCheckBox_CheckedChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_receiverController is null)
                return;

            if (_useRtnInformationCheckBox?.IsChecked == true)
            {
                ApplyRtnInformationToSelectedReceiver();

                if (DateTime.UtcNow - _lastReceiverRefreshTime < TimeSpan.FromSeconds(15))
                    return;

                _lastReceiverRefreshTime = DateTime.UtcNow;

                try
                {
                    await _receiverController.RefreshAllReceiversAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unable to refresh receivers: {ex}");
                }

                return;
            }
            else
            {
                await RestoreReceiverInformationAsync();
            }
        }

        private void DataGridRow_RightClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
                return;

            row.IsSelected = true;
            row.Focus();

            if (row.ContextMenu is not null)
                row.ContextMenu.DataContext = row.Item;
        }

        private void CopyChannel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryGetScheduleItem(
                    sender,
                    out RcnScheduleItem? item))
            {
                return;
            }

            Clipboard.SetText(item.ChannelNumber);
        }

        private async void SetChannel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryGetScheduleItem(
                    sender,
                    out RcnScheduleItem? item))
            {
                return;
            }

            if (_receiverController is null)
                return;

            Receiver? receiver =
                _receiverController.SelectedReceiver;

            if (receiver is null)
            {
                MessageBox.Show(
                    "Select a receiver before setting the channel.",
                    "No Receiver Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryConvertChannel(
                    item.ChannelNumber,
                    out int serviceId))
            {
                MessageBox.Show(
                    $"'{item.ChannelNumber}' is not a valid channel.",
                    "Invalid Channel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                CommandResult<HELLO_DISCOVERY_RESPONSE> response =
                    await _receiverController
                        .SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(
                            receiver,
                            new FORCE_CH_SWITCH(serviceId),
                            TimeSpan.FromSeconds(6));

                if (!response.IsSuccess)
                {
                    MessageBox.Show(
                        "The receiver did not accept the channel change.",
                        "Channel Change Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                await _receiverController.RefreshReceiverAsync(
                    receiver,
                    RefreshBehavior.WaitForRefreshWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to set the channel.\n\n{ex.Message}",
                    "Channel Change Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadScheduleAsync()
        {
            if (_scheduleGrid is null ||
                _statusText is null ||
                _scheduleTitle is null ||
                _refreshButton is null)
            {
                return;
            }

            try
            {
                _refreshButton.IsEnabled = false;
                _refreshButton.Content = "Loading...";

                _statusText.Text =
                    "Downloading the latest schedule...";

                RcnScheduleResult result =
                    await RcnScheduleService
                        .LoadScheduleAsync();

                _scheduleItems.Clear();
                _scheduleItems.AddRange(result.Items);



                _scheduleGrid.Items.Refresh();

                _scheduleTitle.Text = result.Title;

                _statusText.Text = result.Items.Count == 0
                    ? "No scheduled events were found."
                    : $"{result.Items.Count} scheduled events • Times shown in local time";

                ApplyRtnInformationToSelectedReceiver();
            }
            catch (HttpRequestException ex)
            {
                _statusText.Text =
                    $"Unable to download the RCN schedule: {ex.Message}";
            }
            catch (Exception ex)
            {
                _statusText.Text =
                    $"Unable to load the RCN schedule: {ex.Message}";

                Debug.WriteLine(
                    $"RCN Schedule Plugin error: {ex}");
            }
            finally
            {
                _refreshButton.IsEnabled = true;
                _refreshButton.Content = "Refresh";
            }
        }

        private static bool TryGetScheduleItem(
            object sender,
            out RcnScheduleItem? item)
        {
            item = null;

            if (sender is not MenuItem menuItem ||
                menuItem.Parent is not ContextMenu contextMenu ||
                contextMenu.DataContext is not RcnScheduleItem scheduleItem)
            {
                return false;
            }

            item = scheduleItem;

            return true;
        }

        private static bool TryConvertChannel(
            string channelNumber,
            out int serviceId)
        {
            serviceId = 0;

            if (!int.TryParse(
                    channelNumber,
                    out int channel))
            {
                return false;
            }

            if (channel is >= 1 and <= 50)
            {
                serviceId = channel + 100;
                return true;
            }

            if (channel is >= 100 and <= 150)
            {
                serviceId = channel;
                return true;
            }

            return false;
        }

        private static void TryApplyHostStyle(
            FrameworkElement element,
            object resourceKey)
        {
            element.SetResourceReference(
                FrameworkElement.StyleProperty,
                resourceKey);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_receiverController is not null)
            {
                _receiverController.SelectedReceiverChanged -= ReceiverController_SelectedReceiverChanged;
                _receiverController.ReceiverUpdated -= ReceiverController_ReceiverUpdated;
            }

            if (_useRtnInformationCheckBox is not null)
            {
                _useRtnInformationCheckBox.Checked -= UseRtnInformationCheckBox_CheckedChanged;
                _useRtnInformationCheckBox.Unchecked -= UseRtnInformationCheckBox_CheckedChanged;

                if (_useRtnInformationCheckBox.Parent is Panel parent)
                    parent.Children.Remove(_useRtnInformationCheckBox);
            }

            if (_refreshButton is not null)
                _refreshButton.Click -= RefreshButton_Click;

            if (_pluginContentItemsControl is not null &&
                _pluginContent is not null)
            {
                _pluginContentItemsControl.Items.Remove(_pluginContent);
            }

            _pluginContent = null;
            _pluginContentItemsControl = null;
            _pluginContext = null;
            _receiverController = null;
            _selectedReceiver = null;
        }
    }
}