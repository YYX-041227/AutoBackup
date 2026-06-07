using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AutoBackup
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly BackupService _backupService = new BackupService();
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly DispatcherTimer _backupTimer = new DispatcherTimer();
        private readonly ObservableCollection<string> _logLines = new ObservableCollection<string>();

        private DateTime? _lastBackupTime;
        private bool _isBackingUp;
        private bool _allowClose;
        private bool _isExitRequested;
        private bool _closePromptOpen;

        public MainWindow()
        {
            InitializeComponent();
            FindControls();
            InitializeComboBoxes();
            WireEvents();
            LoadSettings();
            InitializeTimers();
            LogMessage("程序已启动");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FindControls()
        {
            SourceFolderTextBox = this.FindControl<TextBox>(nameof(SourceFolderTextBox))!;
            TargetFolderTextBox = this.FindControl<TextBox>(nameof(TargetFolderTextBox))!;
            BrowseSourceButton = this.FindControl<Button>(nameof(BrowseSourceButton))!;
            BrowseTargetButton = this.FindControl<Button>(nameof(BrowseTargetButton))!;
            CompareFilesCheckBox = this.FindControl<ToggleSwitch>(nameof(CompareFilesCheckBox))!;
            VersionedBackupCheckBox = this.FindControl<ToggleSwitch>(nameof(VersionedBackupCheckBox))!;
            VersionedSettingsPanel = this.FindControl<Border>(nameof(VersionedSettingsPanel))!;
            MaxCapacityTextBox = this.FindControl<TextBox>(nameof(MaxCapacityTextBox))!;
            RetentionDaysTextBox = this.FindControl<TextBox>(nameof(RetentionDaysTextBox))!;
            NamingFormatTextBox = this.FindControl<TextBox>(nameof(NamingFormatTextBox))!;
            AutoCleanupCheckBox = this.FindControl<ToggleSwitch>(nameof(AutoCleanupCheckBox))!;
            ScheduledBackupCheckBox = this.FindControl<ToggleSwitch>(nameof(ScheduledBackupCheckBox))!;
            ScheduleSettingsPanel = this.FindControl<Border>(nameof(ScheduleSettingsPanel))!;
            FrequencyComboBox = this.FindControl<ComboBox>(nameof(FrequencyComboBox))!;
            IntervalTextBox = this.FindControl<TextBox>(nameof(IntervalTextBox))!;
            IntervalLabel = this.FindControl<TextBlock>(nameof(IntervalLabel))!;
            TimeSettingsGrid = this.FindControl<Grid>(nameof(TimeSettingsGrid))!;
            HourComboBox = this.FindControl<ComboBox>(nameof(HourComboBox))!;
            MinuteComboBox = this.FindControl<ComboBox>(nameof(MinuteComboBox))!;
            AmPmComboBox = this.FindControl<ComboBox>(nameof(AmPmComboBox))!;
            DayOfWeekPanel = this.FindControl<StackPanel>(nameof(DayOfWeekPanel))!;
            DayOfWeekComboBox = this.FindControl<ComboBox>(nameof(DayOfWeekComboBox))!;
            DayOfMonthPanel = this.FindControl<StackPanel>(nameof(DayOfMonthPanel))!;
            DayOfMonthComboBox = this.FindControl<ComboBox>(nameof(DayOfMonthComboBox))!;
            MinimizeToTrayCheckBox = this.FindControl<ToggleSwitch>(nameof(MinimizeToTrayCheckBox))!;
            StatusTextBlock = this.FindControl<TextBlock>(nameof(StatusTextBlock))!;
            TimeStatusTextBlock = this.FindControl<TextBlock>(nameof(TimeStatusTextBlock))!;
            SaveSettingsButton = this.FindControl<Button>(nameof(SaveSettingsButton))!;
            BackupButton = this.FindControl<Button>(nameof(BackupButton))!;
            LogListBox = this.FindControl<ListBox>(nameof(LogListBox))!;
        }

        private void InitializeComboBoxes()
        {
            FrequencyComboBox.ItemsSource = new[]
            {
                "每隔 N 分钟",
                "每隔 N 小时",
                "每天固定时间",
                "每周固定时间",
                "每月固定日期"
            };

            HourComboBox.ItemsSource = Enumerable.Range(1, 12).Select(value => value.ToString("00")).ToArray();
            MinuteComboBox.ItemsSource = Enumerable.Range(0, 60).Select(value => value.ToString("00")).ToArray();
            AmPmComboBox.ItemsSource = new[] { "上午", "下午" };
            DayOfWeekComboBox.ItemsSource = new[] { "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
            DayOfMonthComboBox.ItemsSource = Enumerable.Range(1, 31).Select(value => $"{value}日").ToArray();
            LogListBox.ItemsSource = _logLines;
        }

        private void WireEvents()
        {
            BrowseSourceButton.Click += async (_, _) => await PickFolderAsync(SourceFolderTextBox, "选择源文件夹");
            BrowseTargetButton.Click += async (_, _) => await PickFolderAsync(TargetFolderTextBox, "选择目标文件夹");
            SaveSettingsButton.Click += (_, _) => SaveSettings();
            BackupButton.Click += async (_, _) => await PerformBackupAsync();

            VersionedBackupCheckBox.IsCheckedChanged += (_, _) => UpdatePanels();
            ScheduledBackupCheckBox.IsCheckedChanged += (_, _) => UpdatePanels();
            FrequencyComboBox.SelectionChanged += (_, _) => UpdatePanels();
            Opened += (_, _) => Dispatcher.UIThread.Post(BringWindowToFront, DispatcherPriority.Background);
            Closing += MainWindow_Closing;
            Closed += (_, _) => App.BringToFrontRequested -= OnBringToFrontRequested;
            App.BringToFrontRequested += OnBringToFrontRequested;
        }

        private void InitializeTimers()
        {
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => UpdateTimeDisplay();
            _clockTimer.Start();

            _backupTimer.Interval = TimeSpan.FromSeconds(30);
            _backupTimer.Tick += async (_, _) =>
            {
                BackupSettings settings = BuildSettingsFromUi();
                if (ShouldRunScheduledBackup(settings))
                {
                    await PerformBackupAsync();
                    _lastBackupTime = DateTime.Now;
                }
            };

            UpdateTimeDisplay();
            ApplyScheduleState(BuildSettingsFromUi());
        }

        private async Task PickFolderAsync(TextBox targetTextBox, string title)
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                LogMessage("无法打开文件夹选择器：窗口尚未准备好。");
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                targetTextBox.Text = folders[0].Path.LocalPath;
            }
        }

        private void LoadSettings()
        {
            try
            {
                BackupSettings settings = _settingsService.Load();

                SourceFolderTextBox.Text = settings.SourceFolder;
                TargetFolderTextBox.Text = settings.TargetFolder;
                CompareFilesCheckBox.IsChecked = settings.CompareFiles;
                VersionedBackupCheckBox.IsChecked = settings.VersionedBackup;
                MaxCapacityTextBox.Text = settings.MaxCapacityGB.ToString(CultureInfo.InvariantCulture);
                RetentionDaysTextBox.Text = settings.RetentionDays.ToString(CultureInfo.InvariantCulture);
                NamingFormatTextBox.Text = settings.NamingFormat;
                AutoCleanupCheckBox.IsChecked = settings.AutoCleanup;
                ScheduledBackupCheckBox.IsChecked = settings.ScheduledBackup;
                MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
                IntervalTextBox.Text = settings.Interval;

                SetSelectedIndex(FrequencyComboBox, settings.Frequency, 0);
                SetSelectedIndex(HourComboBox, settings.HourIndex, 11);
                SetSelectedIndex(MinuteComboBox, settings.MinuteIndex, 0);
                SetSelectedIndex(AmPmComboBox, settings.AmPmIndex, 0);
                SetSelectedIndex(DayOfWeekComboBox, settings.DayOfWeekIndex, 0);
                SetSelectedIndex(DayOfMonthComboBox, settings.DayOfMonthIndex, 0);

                UpdatePanels(animate: false);
                LogMessage($"设置已加载：{_settingsService.SettingsFilePath}");
            }
            catch (Exception ex)
            {
                LogMessage($"加载设置失败：{ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                BackupSettings settings = BuildSettingsFromUi();
                _settingsService.Save(settings);
                ApplyScheduleState(settings);

                UpdatePanels(animate: false);
                UpdateStatusText("设置已保存");
                LogMessage("设置已保存");
            }
            catch (Exception ex)
            {
                UpdateStatusText("保存失败");
                LogMessage($"保存设置失败：{ex.Message}");
            }
        }

        private async Task PerformBackupAsync()
        {
            if (_isBackingUp)
            {
                LogMessage("已有备份正在执行，本次请求已跳过。");
                return;
            }

            _isBackingUp = true;
            BackupButton.IsEnabled = false;
            UpdateStatusText("备份中...");
            LogMessage("开始备份");

            try
            {
                BackupRequest request = BuildBackupRequestFromUi();
                BackupResult result = await Task.Run(() => _backupService.Execute(request));

                UpdateStatusText(result.StatusText);
                foreach (string message in result.Messages)
                {
                    LogMessage(message);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText("备份失败");
                LogMessage($"备份失败：{ex.Message}");
            }
            finally
            {
                BackupButton.IsEnabled = true;
                _isBackingUp = false;
            }
        }

        private BackupSettings BuildSettingsFromUi()
        {
            return new BackupSettings
            {
                SourceFolder = SourceFolderTextBox.Text?.Trim() ?? "",
                TargetFolder = TargetFolderTextBox.Text?.Trim() ?? "",
                CompareFiles = CompareFilesCheckBox.IsChecked == true,
                ScheduledBackup = ScheduledBackupCheckBox.IsChecked == true,
                MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true,
                Frequency = FrequencyComboBox.SelectedIndex < 0 ? 0 : FrequencyComboBox.SelectedIndex,
                HourIndex = HourComboBox.SelectedIndex < 0 ? 11 : HourComboBox.SelectedIndex,
                MinuteIndex = MinuteComboBox.SelectedIndex < 0 ? 0 : MinuteComboBox.SelectedIndex,
                AmPmIndex = AmPmComboBox.SelectedIndex < 0 ? 0 : AmPmComboBox.SelectedIndex,
                Interval = string.IsNullOrWhiteSpace(IntervalTextBox.Text) ? "10" : IntervalTextBox.Text.Trim(),
                DayOfWeekIndex = DayOfWeekComboBox.SelectedIndex,
                DayOfMonthIndex = DayOfMonthComboBox.SelectedIndex,
                VersionedBackup = VersionedBackupCheckBox.IsChecked == true,
                MaxCapacityGB = ReadDouble(MaxCapacityTextBox.Text, 10.0),
                RetentionDays = ReadInt(RetentionDaysTextBox.Text, 30),
                NamingFormat = string.IsNullOrWhiteSpace(NamingFormatTextBox.Text)
                    ? "yyyy-MM-dd_HH-mm-ss"
                    : NamingFormatTextBox.Text.Trim(),
                AutoCleanup = AutoCleanupCheckBox.IsChecked == true
            };
        }

        private BackupRequest BuildBackupRequestFromUi()
        {
            BackupSettings settings = BuildSettingsFromUi();
            return new BackupRequest
            {
                SourceFolder = settings.SourceFolder,
                TargetFolder = settings.TargetFolder,
                CompareFiles = settings.CompareFiles,
                VersionedBackup = settings.VersionedBackup,
                MaxCapacityGB = settings.MaxCapacityGB,
                RetentionDays = settings.RetentionDays,
                NamingFormat = settings.NamingFormat,
                AutoCleanup = settings.AutoCleanup
            };
        }

        private bool ShouldRunScheduledBackup(BackupSettings settings)
        {
            if (!settings.ScheduledBackup)
            {
                return false;
            }

            DateTime now = DateTime.Now;

            // Interval schedules: skip if a backup is already in progress.
            // The interval counter will naturally retry on the next tick.
            if (settings.Frequency <= 1)
            {
                if (_isBackingUp)
                {
                    return false;
                }

                if (settings.Frequency == 0)
                {
                    int minutes = ReadInt(settings.Interval, 10);
                    return !_lastBackupTime.HasValue || (now - _lastBackupTime.Value).TotalMinutes >= minutes;
                }

                // Frequency == 1
                int hours = ReadInt(settings.Interval, 1);
                return !_lastBackupTime.HasValue || (now - _lastBackupTime.Value).TotalHours >= hours;
            }

            // Fixed-time schedules (daily / weekly / monthly):
            // Check time match and de-duplication BEFORE the _isBackingUp guard.
            // If we bail out early due to _isBackingUp, the minute window is
            // permanently lost because the next 30 s tick lands outside the
            // target minute.  We still record _lastBackupTime so the same
            // minute won't fire twice; the backup that is already running
            // covers this time slot.
            (int scheduledHour, int scheduledMinute) = GetSelectedTime(settings);
            if (now.Hour != scheduledHour || now.Minute != scheduledMinute)
            {
                return false;
            }

            if (AlreadyRanThisMinute(now))
            {
                return false;
            }

            _lastBackupTime = now;

            if (_isBackingUp)
            {
                LogMessage("固定时间备份窗口已到达，但上次备份仍在执行，本次合并跳过。");
                return false;
            }

            if (settings.Frequency == 2)
            {
                return true;
            }

            if (settings.Frequency == 3)
            {
                return (int)now.DayOfWeek == GetDayOfWeekNumber(settings.DayOfWeekIndex);
            }

            // Frequency == 4
            return now.Day == Math.Clamp(settings.DayOfMonthIndex + 1, 1, 31);
        }

        private void ApplyScheduleState(BackupSettings settings)
        {
            if (!settings.ScheduledBackup)
            {
                _backupTimer.Stop();
                _lastBackupTime = null;
                return;
            }

            _backupTimer.Start();

            // For interval schedules, start counting from when the user saves settings.
            // For fixed-time schedules, keep null so the next matching time can run normally.
            _lastBackupTime = settings.Frequency <= 1 ? DateTime.Now : null;
        }

        private bool AlreadyRanThisMinute(DateTime now)
        {
            return _lastBackupTime.HasValue &&
                _lastBackupTime.Value.Year == now.Year &&
                _lastBackupTime.Value.Month == now.Month &&
                _lastBackupTime.Value.Day == now.Day &&
                _lastBackupTime.Value.Hour == now.Hour &&
                _lastBackupTime.Value.Minute == now.Minute;
        }

        private static (int Hour, int Minute) GetSelectedTime(BackupSettings settings)
        {
            int hour = Math.Clamp(settings.HourIndex + 1, 1, 12);
            int minute = Math.Clamp(settings.MinuteIndex, 0, 59);

            if (settings.AmPmIndex == 1 && hour < 12)
            {
                hour += 12;
            }
            else if (settings.AmPmIndex == 0 && hour == 12)
            {
                hour = 0;
            }

            return (hour, minute);
        }

        private static int GetDayOfWeekNumber(int selectedIndex)
        {
            return selectedIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 6,
                6 => 0,
                _ => 1
            };
        }

        private readonly Dictionary<Control, bool> _panelAnimationTargets = new Dictionary<Control, bool>();
        private readonly Dictionary<Control, int> _panelAnimationVersions = new Dictionary<Control, int>();

        private void UpdatePanels(bool animate = true)
        {
            bool versioned = VersionedBackupCheckBox.IsChecked == true;
            bool scheduled = ScheduledBackupCheckBox.IsChecked == true;

            SetPanelVisibility(VersionedSettingsPanel, versioned, animate);
            SetPanelVisibility(ScheduleSettingsPanel, scheduled, animate);

            int frequency = FrequencyComboBox.SelectedIndex < 0 ? 0 : FrequencyComboBox.SelectedIndex;
            bool intervalMode = frequency <= 1;
            IntervalLabel.Text = frequency == 1 ? "间隔小时" : "间隔分钟";
            IntervalTextBox.IsVisible = intervalMode;
            TimeSettingsGrid.IsVisible = !intervalMode;
            DayOfWeekPanel.IsVisible = frequency == 3;
            DayOfMonthPanel.IsVisible = frequency == 4;

            if (intervalMode && string.IsNullOrWhiteSpace(IntervalTextBox.Text))
            {
                IntervalTextBox.Text = frequency == 1 ? "1" : "10";
            }
        }

        private void SetPanelVisibility(Border panel, bool show, bool animate)
        {
            if (!animate)
            {
                SetPanelImmediate(panel, show);
                return;
            }

            bool currentTarget = _panelAnimationTargets.TryGetValue(panel, out bool target)
                ? target
                : panel.IsVisible;

            if (show == currentTarget)
            {
                return;
            }

            _panelAnimationTargets[panel] = show;
            int version = _panelAnimationVersions.TryGetValue(panel, out int currentVersion)
                ? currentVersion + 1
                : 1;
            _panelAnimationVersions[panel] = version;

            if (show)
            {
                panel.IsEnabled = true;
                panel.Opacity = 0;
                panel.IsVisible = true;
                panel.Height = double.NaN;
                panel.Measure(new Size(panel.Bounds.Width > 0 ? panel.Bounds.Width : double.PositiveInfinity, double.PositiveInfinity));
                double targetHeight = panel.DesiredSize.Height;

                panel.Height = 0;

                var slideIn = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(250),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                    FillMode = FillMode.Forward,
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0),
                            Setters =
                            {
                                new Setter(Border.HeightProperty, 0.0),
                                new Setter(Border.OpacityProperty, 0.0)
                            }
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1),
                            Setters =
                            {
                                new Setter(Border.HeightProperty, targetHeight),
                                new Setter(Border.OpacityProperty, 1.0)
                            }
                        }
                    }
                };

                Dispatcher.UIThread.Post(async () =>
                {
                    await slideIn.RunAsync(panel);
                    if (!IsLatestPanelAnimation(panel, version, show))
                    {
                        return;
                    }

                    panel.Opacity = 1;
                    panel.Height = double.NaN;
                });
            }
            else
            {
                panel.IsEnabled = false;
                double currentHeight = panel.Bounds.Height;
                if (currentHeight <= 0)
                {
                    panel.Measure(new Size(panel.Bounds.Width > 0 ? panel.Bounds.Width : double.PositiveInfinity, double.PositiveInfinity));
                    currentHeight = panel.DesiredSize.Height;
                }

                panel.Height = currentHeight;

                var slideOut = new Animation
                {
                    Duration = TimeSpan.FromMilliseconds(200),
                    Easing = new Avalonia.Animation.Easings.CubicEaseIn(),
                    FillMode = FillMode.Forward,
                    Children =
                    {
                        new KeyFrame
                        {
                            Cue = new Cue(0),
                            Setters =
                            {
                                new Setter(Border.HeightProperty, currentHeight),
                                new Setter(Border.OpacityProperty, 1.0)
                            }
                        },
                        new KeyFrame
                        {
                            Cue = new Cue(1),
                            Setters =
                            {
                                new Setter(Border.HeightProperty, 0.0),
                                new Setter(Border.OpacityProperty, 0.0)
                            }
                        }
                    }
                };

                Dispatcher.UIThread.Post(async () =>
                {
                    await slideOut.RunAsync(panel);
                    if (!IsLatestPanelAnimation(panel, version, show))
                    {
                        return;
                    }

                    panel.IsVisible = false;
                    panel.Height = double.NaN;
                    panel.Opacity = 1;
                });
            }
        }

        private void SetPanelImmediate(Border panel, bool show)
        {
            _panelAnimationTargets[panel] = show;
            int version = _panelAnimationVersions.TryGetValue(panel, out int currentVersion)
                ? currentVersion + 1
                : 1;
            _panelAnimationVersions[panel] = version;

            panel.IsVisible = show;
            panel.IsEnabled = show;
            panel.Height = double.NaN;
            panel.Opacity = 1;
        }

        private bool IsLatestPanelAnimation(Border panel, int version, bool target)
        {
            return _panelAnimationVersions.TryGetValue(panel, out int currentVersion) &&
                currentVersion == version &&
                _panelAnimationTargets.TryGetValue(panel, out bool currentTarget) &&
                currentTarget == target;
        }

        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose)
            {
                _clockTimer.Stop();
                _backupTimer.Stop();
                return;
            }

            if (_closePromptOpen)
            {
                e.Cancel = true;
                return;
            }

            if (_isExitRequested)
            {
                if (ScheduledBackupCheckBox.IsChecked == true)
                {
                    e.Cancel = true;
                    _isExitRequested = false;
                    ShowCloseConfirmationAsync();
                    return;
                }

                _allowClose = true;
                _clockTimer.Stop();
                _backupTimer.Stop();
                return;
            }

            if (MinimizeToTrayCheckBox.IsChecked == true)
            {
                e.Cancel = true;
                _isExitRequested = false;
                HideToBackground();
                return;
            }

            if (ScheduledBackupCheckBox.IsChecked == true)
            {
                e.Cancel = true;
                ShowCloseConfirmationAsync();
            }
        }

        private void UpdateTimeDisplay()
        {
            TimeStatusTextBlock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        }

        private void OnBringToFrontRequested()
        {
            BringWindowToFront();
            LogMessage("已从后台恢复窗口。");
        }

        private void BringWindowToFront()
        {
            ((App?)Application.Current)?.SetTrayIconVisible(false);
            ShowInTaskbar = true;

            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Topmost = true;
            Activate();
            Topmost = false;
            Focus();
        }

        public void RestoreFromBackground()
        {
            BringWindowToFront();
        }

        public Task RunBackupFromTrayAsync()
        {
            BringWindowToFront();
            return PerformBackupAsync();
        }

        public void RequestExitFromTray()
        {
            RequestExit();
        }

        private void UpdateStatusText(string text)
        {
            StatusTextBlock.Text = text;
        }

        private void LogMessage(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logLines.Add(line);
            if (_logLines.Count > 300)
            {
                while (_logLines.Count > 300)
                {
                    _logLines.RemoveAt(0);
                }
            }

            int lastIndex = _logLines.Count - 1;
            Dispatcher.UIThread.Post(() => LogListBox.ScrollIntoView(lastIndex), DispatcherPriority.Background);
        }

        private async void ShowCloseConfirmationAsync()
        {
            if (_closePromptOpen)
            {
                return;
            }

            _closePromptOpen = true;

            try
            {
                bool shouldClose = await ShowConfirmDialogAsync(
                    "关闭软件",
                    "当前已开启定时备份。彻底关闭软件后，自动备份将停止执行。是否仍然退出？",
                    "退出软件",
                    "继续运行");

                if (shouldClose)
                {
                    _allowClose = true;
                    Close();
                }
            }
            finally
            {
                _closePromptOpen = false;
            }
        }

        private void RequestExit()
        {
            BringWindowToFront();
            _isExitRequested = true;
            Close();
        }

        private void HideToBackground()
        {
            ((App?)Application.Current)?.SetTrayIconVisible(true);
            ShowInTaskbar = false;
            WindowState = WindowState.Normal;
            Hide();
            LogMessage("窗口已隐藏到系统托盘，程序仍在后台运行。");
        }

        private async Task<bool> ShowConfirmDialogAsync(string title, string message, string confirmText, string cancelText)
        {
            Window dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 190,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.White,
                Content = BuildConfirmDialogContent(message, confirmText, cancelText)
            };

            if (dialog.Content is Grid grid &&
                grid.Children[1] is StackPanel buttonPanel &&
                buttonPanel.Children[0] is Button confirmButton &&
                buttonPanel.Children[1] is Button cancelButton)
            {
                confirmButton.Click += (_, _) => dialog.Close(true);
                cancelButton.Click += (_, _) => dialog.Close(false);
            }

            bool? result = await dialog.ShowDialog<bool?>(this);
            return result == true;
        }

        private static Grid BuildConfirmDialogContent(string message, string confirmText, string cancelText)
        {
            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
            };

            Button confirmButton = new Button
            {
                Content = confirmText,
                MinWidth = 96,
                Background = new SolidColorBrush(Color.Parse("#2563EB")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#2563EB"))
            };

            Button cancelButton = new Button
            {
                Content = cancelText,
                MinWidth = 96,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.Parse("#1E3A8A")),
                BorderBrush = new SolidColorBrush(Color.Parse("#C7D7F5"))
            };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Children = { confirmButton, cancelButton }
            };

            Grid.SetRow(buttons, 1);

            return new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Margin = new Thickness(22, 20),
                RowSpacing = 18,
                Children =
                {
                    messageBlock,
                    buttons
                }
            };
        }

        private static void SetSelectedIndex(ComboBox comboBox, int index, int fallback)
        {
            int maxIndex = comboBox.ItemCount - 1;
            comboBox.SelectedIndex = index >= 0 && index <= maxIndex ? index : Math.Clamp(fallback, 0, maxIndex);
        }

        private static int ReadInt(string? value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static double ReadDouble(string? value, double fallback)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0
                ? parsed
                : fallback;
        }
    }
}
