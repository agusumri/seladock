using System.Runtime.InteropServices;
using System.Text;

namespace SelaDock.Interop;

/// <summary>
/// Thin P/Invoke surface used by the dock. Kept in one place so the rest of the app stays
/// free of marshalling details. Only the handful of Win32 calls the dock actually needs are here.
/// </summary>
internal static class Native
{
    // ---- Window enumeration & query -------------------------------------------------

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd); // minimized

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextW(nint hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetWindow(nint hWnd, uint cmd);

    public const uint GW_OWNER = 4;

    [DllImport("user32.dll")]
    public static extern int GetWindowLongW(nint hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hWnd, int index);

    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const long WS_VISIBLE = 0x10000000;
    public const long WS_EX_TOOLWINDOW = 0x00000080;
    public const long WS_EX_APPWINDOW = 0x00040000;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    // Cloaked (UWP suspended / virtual-desktop hidden) windows should not appear in the dock.
    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hWnd, int attr, out int value, int size);

    public const int DWMWA_CLOAKED = 14;

    // ---- Activation -----------------------------------------------------------------

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int cmd);

    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    // ---- Icons ----------------------------------------------------------------------

    [DllImport("user32.dll")]
    public static extern nint SendMessageTimeoutW(nint hWnd, uint msg, nint wParam, nint lParam,
        uint flags, uint timeout, out nint result);

    public const uint WM_GETICON = 0x007F;
    public const nint ICON_SMALL2 = 2;
    public const nint ICON_BIG = 1;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll")]
    public static extern nint GetClassLongPtrW(nint hWnd, int index);

    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(nint hIcon);

    // ---- Shell icons (sharp, high-resolution icons from the exe) --------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern nint SHGetFileInfoW(string path, uint fileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint flags);

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_SYSICONINDEX = 0x000004000;
    public const uint SHGFI_LARGEICON = 0x000000000;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    // IImageList jumbo (256px) / extra-large (48px) for crisp dock icons.
    [DllImport("shell32.dll", EntryPoint = "#727")]
    public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    public const int SHIL_EXTRALARGE = 2; // 48px
    public const int SHIL_JUMBO = 4;      // 256px

    public static Guid IID_IImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IImageList
    {
        [PreserveSig] int Add(nint hbmImage, nint hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, nint hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, nint hbmImage, nint hbmMask);
        [PreserveSig] int AddMasked(nint hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(nint pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out nint picon);
    }

    public const int ILD_TRANSPARENT = 0x00000001;

    // ---- WinEvent hook (event-driven updates, no polling) ---------------------------

    public delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hWnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc,
        WinEventProc callback, uint idProcess, uint idThread, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_CREATE = 0x8000;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const int OBJID_WINDOW = 0;

    // ---- AppBar (reserve screen space so maximized windows don't cover the dock) -----

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    [DllImport("shell32.dll")]
    public static extern nint SHAppBarMessage(uint message, ref APPBARDATA data);

    public const uint ABM_NEW = 0x00000000;
    public const uint ABM_REMOVE = 0x00000001;
    public const uint ABM_QUERYPOS = 0x00000002;
    public const uint ABM_SETPOS = 0x00000003;
    public const uint ABE_BOTTOM = 3;

    // ---- Message helpers ------------------------------------------------------------

    [DllImport("kernel32.dll")]
    public static extern uint RegisterWindowMessageW(string name);

    // ---- Taskbar (find it, so we can ask Windows to auto-hide it) --------------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint FindWindowW(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern nint FindWindowExW(nint parent, nint childAfter, string? className, string? windowName);

    public const uint ABM_GETSTATE = 0x00000004;
    public const uint ABM_SETSTATE = 0x0000000A;
    public const int ABS_AUTOHIDE = 0x0000001;
    public const int ABS_ALWAYSONTOP = 0x0000002;

    // ---- System info: battery --------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;      // 0 offline, 1 online, 255 unknown
        public byte BatteryFlag;       // 128 no battery, 8 charging, ...
        public byte BatteryLifePercent; // 0-100, 255 unknown
        public byte Reserved1;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    // ---- System info: audio volume (Core Audio) is wrapped in a managed helper -------
    // (See SystemInfoService; it uses the IMMDevice/IAudioEndpointVolume COM API.)

    // ---- Opening the real Windows flyouts / Start / tray overflow --------------------
    // Done by synthesizing the standard shortcuts via keybd_event in SystemActions.
}
