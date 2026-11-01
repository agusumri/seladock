using System.IO;
using System.Windows;
using System.Windows.Threading;
using SelaDock.Services;
using SelaDock.Views;

namespace SelaDock;

public partial class App : Application
{
    private TrayIcon? _tray;
    private DockWindow? _dock;
    private TaskbarService? _taskbar;
    private Settings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Record unhandled failures so a silent exit leaves a trace to diagnose.
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash(args.ExceptionObject as Exception);

        // One instance only: a second launch should no-op rather than stack two docks.
        if (!SingleInstance.Acquire())
        {
            Shutdown();
            return;
        }

        _settings = Settings.Load();

        // First run: seed the dock with the apps already pinned to the Windows taskbar, so nothing
        // the user pinned appears to vanish once the taskbar is hidden.
        if (!_settings.ImportedTaskbarPins)
        {
            foreach (var path in TaskbarPins.Read())
                if (!_settings.PinnedApps.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _settings.PinnedApps.Add(path);
            _settings.ImportedTaskbarPins = true;
            _settings.Save();
        }

        // Hide the taskbar before the dock window is created/shown, so it never flashes at startup.
        _taskbar = new TaskbarService();
        if (_settings.HideTaskbar) _taskbar.Hide();

        _dock = new DockWindow(_settings, _taskbar);
        _dock.Show();

        _tray = new TrayIcon(
            toggleAutoHide: () =>
            {
                _settings.AutoHide = !_settings.AutoHide;
                _settings.Save();
                _dock.ApplyAutoHide();
            },
            isAutoHide: () => _settings.AutoHide,
            setSize: size =>
            {
                _settings.Size = size;
                _settings.Save();
                _dock.ApplySize();
            },
            currentSize: () => _settings.Size,
            exit: () => Shutdown());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _dock?.CloseDock();
        _taskbar?.Dispose(); // restore the Windows taskbar
        SingleInstance.Release();
        base.OnExit(e);
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelaDock");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:u}  {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // Nothing more we can do if we can't even write the log.
        }
    }
}
