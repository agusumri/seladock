using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SelaDock.Services;
using SelaDock.ViewModel;

namespace SelaDock.Views;

public partial class DockWindow : Window
{
    private readonly Settings _settings;
    private readonly WindowService _windows = new();
    private readonly IconService _icons = new();
    private readonly DockViewModel _dock;
    private readonly ClockViewModel _clock = new();
    private readonly SystemInfoService _sys = new();
    private readonly DockChrome _chrome;
    private AppBarService? _appBar;
    private readonly TaskbarService _taskbar;

    private int StripHeight => (int)Math.Round(_chrome.PlateSize + 32); // logical px reserved at bottom

    public DockWindow(Settings settings, TaskbarService taskbar)
    {
        _settings = settings;
        _taskbar = taskbar;
        _chrome = new DockChrome(_sys);
        InitializeComponent();
        DataContext = _chrome;

        _dock = new DockViewModel(_windows, _icons, _settings);
        AppList.ItemsSource = _dock.Items;

        _clock.PropertyChanged += OnClockChanged;
        ClockTime.Text = _clock.Time;
        ClockDate.Text = _clock.Date;

        _sys.PropertyChanged += OnSysChanged;
        UpdateSysGlyphs();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _chrome.IconSize = _settings.ResolveIconSize(SystemParameters.PrimaryScreenWidth);
        PlaceAcrossBottom();
        MakeNonActivating();

        _appBar = new AppBarService(this);
        ApplyReserve();
        ApplyAutoHide();

        // Reserving the app-bar makes Explorer recompute the work area, which can briefly re-show
        // the taskbar it was told to hide. Re-assert the hide once layout has settled.
        if (_settings.HideTaskbar) RehideTaskbarSoon();
    }

    private void PlaceAcrossBottom()
    {
        Left = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = StripHeight;
        Top = SystemParameters.PrimaryScreenHeight - StripHeight;
    }

    /// <summary>Non-activating tool window so clicking the dock never steals focus from apps.</summary>
    private void MakeNonActivating()
    {
        var hWnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Interop.Native.GetWindowLongPtr(hWnd, Interop.Native.GWL_EXSTYLE);
        ex |= Interop.Native.WS_EX_NOACTIVATE | Interop.Native.WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hWnd, Interop.Native.GWL_EXSTYLE, (nint)ex);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    public void ApplyReserve()
    {
        if (_settings.ReserveSpace && !_settings.AutoHide)
            _appBar?.Reserve(StripHeight);
    }

    /// <summary>Re-resolve size after a settings change and relayout.</summary>
    public void ApplySize()
    {
        _chrome.IconSize = _settings.ResolveIconSize(SystemParameters.PrimaryScreenWidth);
        PlaceAcrossBottom();
        _appBar?.Dispose();
        _appBar = new AppBarService(this);
        ApplyReserve();
    }

    // ---- Auto-hide -----------------------------------------------------------------

    public void ApplyAutoHide()
    {
        if (_settings.AutoHide)
        {
            _appBar?.Dispose();
            _appBar = new AppBarService(this);
            Slide(hidden: true);
            Root.MouseEnter += ShowOnHover;
        }
        else
        {
            Root.MouseEnter -= ShowOnHover;
            Slide(hidden: false);
            ApplyReserve();
        }
    }

    private void ShowOnHover(object? s, MouseEventArgs e) => Slide(hidden: false);

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_settings.AutoHide) Slide(hidden: true);
    }

    private void Slide(bool hidden)
    {
        var to = hidden ? StripHeight : 0d;
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        PillSlide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, anim);
    }

    // ---- Clock & system indicators -------------------------------------------------

    private void OnClockChanged(object? sender, PropertyChangedEventArgs e)
    {
        ClockTime.Text = _clock.Time;
        ClockDate.Text = _clock.Date;
    }

    private void OnSysChanged(object? sender, PropertyChangedEventArgs e) => UpdateSysGlyphs();

    private void UpdateSysGlyphs()
    {
        NetGlyph.Text = _sys.NetworkGlyph;
        VolGlyph.Text = _sys.VolumeGlyph;
        BattGlyph.Text = _sys.BatteryGlyph;
        BattBtn.Visibility = _sys.HasBattery ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- System button clicks (open the genuine Windows UI) ------------------------

    private void Start_Click(object sender, MouseButtonEventArgs e)
    {
        SystemActions.OpenStart();
        RehideTaskbarSoon();
    }

    private void Tray_Click(object sender, MouseButtonEventArgs e) => ShowTrayFlyout((UIElement)sender);

    /// <summary>
    /// The "^" button: a dark popup listing apps running in the background (the tray role). Each
    /// entry activates that app's window. Built fresh each open, so there's no cost while closed.
    /// </summary>
    private void ShowTrayFlyout(UIElement anchor)
    {
        // Exclude apps already on the dock so nothing (e.g. Chrome) shows in both places.
        var apps = _windows.EnumerateBackgroundApps(_dock.DockExecutablePaths());

        var panel = new System.Windows.Controls.WrapPanel
        {
            Orientation = Orientation.Horizontal,
            MaxWidth = 232,
            Margin = new Thickness(2),
        };

        if (apps.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No background apps",
                Foreground = (System.Windows.Media.Brush)FindResource("ClockSub"),
                Margin = new Thickness(8, 6, 8, 6),
                FontSize = 12,
            });
        }
        else
        {
            foreach (var app in apps)
                panel.Children.Add(BuildTrayEntry(app));
        }

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = anchor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
            Child = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("MenuBg"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("MenuBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 0, 8),
                Child = panel,
            },
        };
        popup.IsOpen = true;
    }

    private FrameworkElement BuildTrayEntry(Model.WindowInfo app)
    {
        var icon = _icons.ForWindow(app.Handle, app.ProcessPath);
        var btn = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(8),
            Background = System.Windows.Media.Brushes.Transparent,
            Margin = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = app.Title,
            Child = new Image
            {
                Width = 18,
                Height = 18,
                Source = icon,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(
            (Image)btn.Child, System.Windows.Media.BitmapScalingMode.HighQuality);
        btn.MouseEnter += (_, _) => btn.Background = (System.Windows.Media.Brush)FindResource("MenuHover");
        btn.MouseLeave += (_, _) => btn.Background = System.Windows.Media.Brushes.Transparent;
        btn.MouseLeftButtonUp += (_, _) =>
        {
            if (app.Handle != nint.Zero) WindowService.Activate(app.Handle);
            else TryShowProcessWindow(app.ProcessId);
        };
        return btn;
    }

    private static void TryShowProcessWindow(uint pid)
    {
        // No cached window handle (fully background): best effort — re-launch brings most tray apps
        // to the foreground, or does nothing harmful if they refuse a second instance.
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            if (p.MainWindowHandle != nint.Zero) WindowService.Activate(p.MainWindowHandle);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Process gone: nothing to do.
        }
    }

    private void Net_Click(object sender, MouseButtonEventArgs e) => SystemActions.OpenNetworkSettings();

    private void Vol_Click(object sender, MouseButtonEventArgs e)
    {
        SystemActions.OpenQuickSettings();
        RehideTaskbarSoon();
    }

    private void Batt_Click(object sender, MouseButtonEventArgs e) => SystemActions.OpenBatterySettings();

    private void Clock_Click(object sender, MouseButtonEventArgs e)
    {
        SystemActions.OpenCalendar();
        RehideTaskbarSoon();
    }

    /// <summary>Opening Start / Quick Settings makes Windows re-show the taskbar; hide it again once
    /// the flyout has appeared so it never flashes under the dock.</summary>
    private void RehideTaskbarSoon()
    {
        if (!_settings.HideTaskbar) return;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            _taskbar.Hide();
        };
        t.Start();
    }

    // ---- App item interaction ------------------------------------------------------

    private Point _pressPoint;
    private DockItem? _pressItem;
    private bool _dragging;

    private void App_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _pressItem = (sender as FrameworkElement)?.DataContext as DockItem;
        _dragging = false;
    }

    private void App_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressItem is null || _dragging) return;
        if (!_pressItem.IsPinned) return; // only pinned items reorder by drag

        var now = e.GetPosition(this);
        if (Math.Abs(now.X - _pressPoint.X) < 12 && Math.Abs(now.Y - _pressPoint.Y) < 12) return;

        // Begin a reorder drag; the click handler is suppressed for this interaction.
        _dragging = true;
        var data = new DataObject("SelaDockPin", _pressItem);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        _dragging = false;
        _pressItem = null;
    }

    private void App_Click(object sender, MouseButtonEventArgs e)
    {
        if (_dragging) { _dragging = false; return; } // was a drag, not a click
        if (((FrameworkElement)sender).DataContext is DockItem item)
            _dock.Invoke(item);
    }

    private void App_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DockItem item) return;

        var menu = new ContextMenu();
        if (item.IsPinned)
        {
            var unpin = new MenuItem { Header = "Unpin from dock" };
            unpin.Click += (_, _) => _dock.Unpin(item);
            menu.Items.Add(unpin);
        }
        else if (!string.IsNullOrEmpty(item.ProcessPath))
        {
            var pin = new MenuItem { Header = "Pin to dock" };
            pin.Click += (_, _) => _dock.Pin(item);
            menu.Items.Add(pin);
        }

        if (item.Windows.Count > 0)
        {
            var min = new MenuItem { Header = "Minimize" };
            min.Click += (_, _) => WindowService.ActivateOrMinimize(item.Windows[0]);
            menu.Items.Add(min);
        }

        if (menu.Items.Count > 0)
        {
            menu.PlacementTarget = (UIElement)sender;
            menu.IsOpen = true;
        }
    }

    // ---- Drag & drop: add external apps, reorder pins ------------------------------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("SelaDockPin"))
            e.Effects = DragDropEffects.Move;            // reordering an existing pin
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;            // adding a new app
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var dropIndex = DropIndexFromPoint(e.GetPosition(AppList));

        if (e.Data.GetData("SelaDockPin") is DockItem dragged)
        {
            _dock.MovePin(dragged, dropIndex);
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop)
                 && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            // Insert dropped apps at the drop position, preserving order.
            int insertAt = dropIndex;
            foreach (var f in files)
            {
                _dock.PinPath(f, insertAt);
                if (insertAt >= 0) insertAt++;
            }
        }
        e.Handled = true;
    }

    /// <summary>Which pinned slot a drop at <paramref name="p"/> (in AppList coords) targets.</summary>
    private int DropIndexFromPoint(Point p)
    {
        int index = 0;
        for (int i = 0; i < AppList.Items.Count; i++)
        {
            if (AppList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement fe) continue;
            if (_dock.Items[i] is not { IsPinned: true }) break; // past the pinned region
            var topLeft = fe.TransformToAncestor(AppList).Transform(new Point(0, 0));
            var mid = topLeft.X + fe.ActualWidth / 2;
            if (p.X > mid) index = i + 1;
        }
        return Math.Clamp(index, 0, _dock.PinnedCount);
    }

    // ---- Lifetime ------------------------------------------------------------------

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        CloseDock();
    }

    private bool _closed;

    public void CloseDock()
    {
        if (_closed) return;
        _closed = true;
        // Note: the TaskbarService is owned and restored by App (so the taskbar comes back exactly
        // once, even if the window closes before the app exits).
        _appBar?.Dispose();
        _windows.Dispose();
        _clock.Dispose();
        _sys.Dispose();
    }
}
