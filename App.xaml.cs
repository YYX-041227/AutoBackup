using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace AutoBackup
{
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = @"Local\AutoBackup_SingleInstance";
        private const string BringToFrontSignalName = @"Local\AutoBackup_BringToFront";
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _bringToFrontSignal;
        private static RegisteredWaitHandle? _bringToFrontRegistration;

        public static bool IsAutoBackupMode { get; private set; }
        public static event Action? BringToFrontRequested;

        public static bool InitializeSingleInstance(string[] args)
        {
            IsAutoBackupMode = args.Any(arg => string.Equals(arg, "--autobackup", StringComparison.OrdinalIgnoreCase));
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);

            if (!createdNew)
            {
                if (!IsAutoBackupMode)
                {
                    SignalBringToFront();
                }

                return false;
            }

            _bringToFrontSignal = new EventWaitHandle(false, EventResetMode.AutoReset, BringToFrontSignalName);
            _bringToFrontRegistration = ThreadPool.RegisterWaitForSingleObject(
                _bringToFrontSignal,
                static (_, _) => Dispatcher.UIThread.Post(() => BringToFrontRequested?.Invoke()),
                null,
                Timeout.Infinite,
                false);

            return true;
        }

        public static void ReleaseSingleInstance()
        {
            try
            {
                _bringToFrontRegistration?.Unregister(null);
                _bringToFrontRegistration = null;
                _bringToFrontSignal?.Dispose();
                _bringToFrontSignal = null;
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
            catch
            {
            }
        }

        private static void SignalBringToFront()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                using EventWaitHandle signal = EventWaitHandle.OpenExisting(BringToFrontSignalName);
                signal.Set();
            }
            catch
            {
            }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            TrayIcon? trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
            if (trayIcon != null)
            {
                trayIcon.Clicked += TrayIcon_OnClicked;
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (IsAutoBackupMode)
                {
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    Dispatcher.UIThread.Post(async () => await RunAutoBackupAndShutdownAsync(desktop));
                }
                else
                {
                    desktop.MainWindow = new MainWindow();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        public void SetTrayIconVisible(bool isVisible)
        {
            TrayIcon? trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
            if (trayIcon != null)
            {
                trayIcon.IsVisible = isVisible;
            }
        }

        public void BringMainWindowToFront()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RestoreFromBackground();
            }
        }

        private async void TrayBackup_OnClick(object? sender, EventArgs e)
        {
            try
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow is MainWindow mainWindow)
                {
                    await mainWindow.RunBackupFromTrayAsync();
                }
            }
            catch (Exception ex)
            {
                LogToCrashFile(ex);
            }
        }

        private void TrayExit_OnClick(object? sender, EventArgs e)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RequestExitFromTray();
            }
        }

        private void TrayIcon_OnClicked(object? sender, EventArgs e)
        {
            BringMainWindowToFront();
        }

        private void TrayShowWindow_OnClick(object? sender, EventArgs e)
        {
            BringMainWindowToFront();
        }

        private static async Task RunAutoBackupAndShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                SettingsService settingsService = new SettingsService();
                BackupSettings settings = settingsService.Load();

                BackupRequest request = new BackupRequest
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

                BackupService backupService = new BackupService();
                BackupResult result = await Task.Run(() => backupService.Execute(request));

                LogToFile(settingsService.SettingsFilePath, result);
            }
            catch (Exception ex)
            {
                LogToCrashFile(ex);
            }
            finally
            {
                desktop.Shutdown();
            }
        }

        private static void LogToFile(string settingsPath, BackupResult result)
        {
            try
            {
                string? dir = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrEmpty(dir))
                {
                    return;
                }

                string logPath = Path.Combine(dir, "autobackup.log");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string status = result.Success ? "成功" : "失败";
                File.AppendAllText(logPath,
                    $"{timestamp} | {status} | 复制 {result.CopiedFiles} / 跳过 {result.SkippedFiles}{Environment.NewLine}");
            }
            catch
            {
                // Best-effort logging.
            }
        }

        private static void LogToCrashFile(Exception ex)
        {
            try
            {
                string appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AutoBackup");
                Directory.CreateDirectory(appData);
                string logPath = Path.Combine(appData, "autobackup_crash.log");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(logPath,
                    $"{timestamp} | 崩溃 | {ex}{Environment.NewLine}");
            }
            catch
            {
                // Absolutely last resort.
            }
        }
    }
}
