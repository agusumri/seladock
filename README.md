# SelaDock

An elegant, lightweight dock for Windows — a macOS-style app bar at the bottom of the
screen. It is a **separate app** from the Sela notch, built on the same stack (.NET 10 WPF,
self-contained single-file, Win32 interop, tray icon).

## What it does

- A dark, translucent, rounded dock pinned to the bottom of the primary screen, meant to
  replace the Windows taskbar.
- **Start button** opens the real Windows Start menu.
- **Pinned apps**:
  - On first run, the dock imports the apps you already pinned to the Windows taskbar.
  - Right-click an icon → *Pin* / *Unpin*.
  - **Drag an .exe or shortcut onto the dock** to pin it.
  - **Drag a pinned icon** left/right to reorder; order persists.
- **Running windows**: shows top-level (alt-tab style) windows, grouped per app.
- **Click to focus / minimize**: click toggles the window; multiple windows cycle.
- **Running + active indicators**: a dot under running apps, highlighted when active.
- **"^" tray flyout**: a dark popup listing apps running in the background (the tray role),
  each clickable to bring it forward. See the note on Windows 11 tray icons below.
- **System indicators**: network, volume, and battery glyphs, each opening the genuine
  Windows panel (Quick Settings, sound, battery).
- **Clock**: time and date, updated once a minute; click opens the calendar.
- **Dock size**: Auto (fits screen width) / Small / Medium / Large, from the tray menu.
- **Hide the Windows taskbar** while running (fully hidden, not auto-hide, so Start no longer
  makes it reappear). Restored on exit.
- **Auto-hide the dock**: optional, from the tray menu.
- **Dark menus**: the tray menu and right-click menus match the dock, not the light Windows style.

### Windows 11 tray icons — important limitation

On Windows 11 (build ~22000+), third-party tray icons (Discord, Steam, etc.) are no longer
Win32 windows; the shell renders them with XAML Islands inside Explorer. There is **no supported
way to move or re-host those icons** in another app's window. So the "^" flyout lists background
*processes* the dock can activate, rather than re-hosting the actual tray icons. Battery, volume,
and network keep their own dock buttons that open the real Windows panels.

## Why it stays light

- **Event-driven, not polling.** Window changes come from a `SetWinEventHook` out-of-context
  hook, so the dock does no work while the desktop is idle.
- **Icon cache.** Each app/window icon is extracted at most once and frozen for sharing.
- **Minute-aligned clock.** The timer wakes roughly once a minute, not every second.
- **Pure-XAML visuals.** GPU-composited; hover magnification is a cheap transform, no bitmaps.

Idle CPU measured at effectively ~0, private working set around 55 MB for the self-contained build.

## Build & run

```powershell
dotnet build src/SelaDock/SelaDock.csproj
dotnet run   --project src/SelaDock/SelaDock.csproj
```

## Publish a single self-contained exe

```powershell
dotnet publish src/SelaDock/SelaDock.csproj -p:PublishProfile=win-x64
# output: dist/publish/SelaDock.exe
```

## Project layout

```
src/SelaDock/
  Interop/Native.cs          Win32 P/Invoke surface (enumerate, focus, icons, appbar, winevent hook)
  Model/WindowInfo.cs        A top-level window record
  Services/
    WindowService.cs         Enumerate windows + event-driven Changed notifications
    IconService.cs           Window/exe icon extraction with caching
    AppBarService.cs         Reserve screen space (SHAppBarMessage)
    Settings.cs              JSON settings under %APPDATA%\SelaDock
    SingleInstance.cs        One-dock-at-a-time mutex
    TrayIcon.cs              Notification-area menu
  ViewModel/
    DockItem.cs              One dock button (pinned / running / active state)
    DockViewModel.cs         Merge pinned apps + running windows into the item list
    ClockViewModel.cs        Minute-aligned clock
  Views/
    DockWindow.xaml(.cs)     The dock window, hover magnification, auto-hide, placement
    BoolToVisibilityConverter.cs
  Theme.xaml                 Palette + tooltip style
```

## Roadmap ideas

- Window thumbnail previews on hover (DWM thumbnails).
- Drag-to-reorder pinned apps.
- Multi-monitor placement and a settings window.
- Optionally replace the Windows taskbar while running.
