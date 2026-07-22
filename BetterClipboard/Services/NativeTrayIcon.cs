using System.Runtime.InteropServices;
using System.ComponentModel;

namespace BetterClipboard.Services;

public sealed class NativeTrayIcon : IDisposable
{
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifInfo = 0x00000010;
    private const uint NiifInfo = 0x00000001;
    private const uint NotifyIconVersion4 = 4;
    private const int WmContextMenu = 0x007B;
    private const int WmLeftButtonDoubleClick = 0x0203;
    private const int WmRightButtonUp = 0x0205;

    public const int CallbackMessage = 0x8001;

    private static readonly int TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    private readonly IntPtr _windowHandle;
    private readonly IntPtr _iconHandle;
    private readonly bool _ownsIconHandle;
    private readonly string _toolTip;
    private readonly Action _openAction;
    private readonly Action _menuAction;
    private bool _disposed;

    public NativeTrayIcon(
        IntPtr windowHandle,
        string toolTip,
        Action openAction,
        Action menuAction)
    {
        _windowHandle = windowHandle;
        (_iconHandle, _ownsIconHandle) = LoadApplicationIcon();
        _toolTip = toolTip;
        _openAction = openAction;
        _menuAction = menuAction;
        AddIcon(throwOnFailure: true);
    }

    public bool HandleWindowMessage(int message, IntPtr lParam)
    {
        if (_disposed)
        {
            return false;
        }

        if (message == TaskbarCreatedMessage)
        {
            AddIcon(throwOnFailure: false);
            return true;
        }

        if (message != CallbackMessage)
        {
            return false;
        }

        var notification = unchecked((int)(lParam.ToInt64() & 0xFFFF));
        switch (notification)
        {
            case WmLeftButtonDoubleClick:
                _openAction();
                break;
            case WmRightButtonUp:
            case WmContextMenu:
                _menuAction();
                break;
        }

        return true;
    }

    public void ShowBalloonTip(string title, string message)
    {
        if (_disposed)
        {
            return;
        }

        var data = CreateData(NifInfo);
        data.InfoTitle = title;
        data.Info = message;
        data.InfoFlags = NiifInfo;
        _ = ShellNotifyIcon(NimModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var data = CreateData(0);
        _ = ShellNotifyIcon(NimDelete, ref data);
        if (_ownsIconHandle && _iconHandle != IntPtr.Zero)
        {
            _ = DestroyIcon(_iconHandle);
        }

        _disposed = true;
    }

    private void AddIcon(bool throwOnFailure)
    {
        var data = CreateData(NifMessage | NifIcon | NifTip);
        if (!ShellNotifyIcon(NimAdd, ref data))
        {
            if (throwOnFailure)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to add the notification-area icon.");
            }

            return;
        }

        data.TimeoutOrVersion = NotifyIconVersion4;
        _ = ShellNotifyIcon(NimSetVersion, ref data);
    }

    private NotifyIconData CreateData(uint flags)
    {
        return new NotifyIconData
        {
            Size = Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = 1,
            Flags = flags,
            CallbackMessage = CallbackMessage,
            IconHandle = _iconHandle,
            Tip = _toolTip,
            Info = "",
            InfoTitle = ""
        };
    }

    private static (IntPtr Handle, bool OwnsHandle) LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            _ = ExtractIconEx(executablePath, 0, out var largeIcon, out var smallIcon, 1);
            if (smallIcon != IntPtr.Zero)
            {
                if (largeIcon != IntPtr.Zero)
                {
                    _ = DestroyIcon(largeIcon);
                }

                return (smallIcon, true);
            }

            if (largeIcon != IntPtr.Zero)
            {
                return (largeIcon, true);
            }
        }

        return (LoadIcon(IntPtr.Zero, new IntPtr(32512)), false);
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string file,
        int iconIndex,
        out IntPtr largeIcon,
        out IntPtr smallIcon,
        uint iconCount);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIconHandle;
    }
}
