using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BetterClipboard.Models;

namespace BetterClipboard;

internal static class NativeMethods
{
    public const int WmClipboardUpdate = 0x031D;
    public const int WmHotKey = 0x0312;
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int VkV = 0x56;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    public static SourceAppInfo GetActiveSource()
    {
        var handle = GetForegroundWindow();
        _ = GetWindowThreadProcessId(handle, out var processId);

        var processName = "Unknown";
        try
        {
            if (processId != 0)
            {
                processName = Process.GetProcessById((int)processId).ProcessName;
            }
        }
        catch
        {
            processName = "Unknown";
        }

        return new SourceAppInfo(processName, GetWindowTitle(handle));
    }

    public static System.Windows.Point GetCursorPosition()
    {
        return GetCursorPos(out var point)
            ? new System.Windows.Point(point.X, point.Y)
            : new System.Windows.Point(400, 300);
    }

    public static WindowPlacement PositionWindowAtCursor(IntPtr window, int gap = 2)
    {
        if (window == IntPtr.Zero ||
            !GetCursorPos(out var cursor) ||
            !GetWindowRect(window, out var windowRect))
        {
            return default;
        }

        var monitor = MonitorFromPoint(cursor, 2);
        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return default;
        }

        var windowWidth = windowRect.Right - windowRect.Left;
        var windowHeight = windowRect.Bottom - windowRect.Top;
        var workArea = monitorInfo.WorkArea;

        var x = cursor.X + gap;
        var y = cursor.Y + gap;

        if (x + windowWidth > workArea.Right)
        {
            x = cursor.X - windowWidth - gap;
        }

        if (y + windowHeight > workArea.Bottom)
        {
            y = cursor.Y - windowHeight - gap;
        }

        var maxX = Math.Max(workArea.Left, workArea.Right - windowWidth);
        var maxY = Math.Max(workArea.Top, workArea.Bottom - windowHeight);
        x = Math.Clamp(x, workArea.Left, maxX);
        y = Math.Clamp(y, workArea.Top, maxY);

        var positioned = SetWindowPos(
            window,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            0x0001 | 0x0004);

        return new WindowPlacement(positioned, cursor.X, cursor.Y, x, y, windowWidth, windowHeight);
    }

    public static FocusSnapshot CaptureFocus()
    {
        var foregroundWindow = GetForegroundWindow();
        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var info = new GuiThreadInfo
        {
            Size = Marshal.SizeOf<GuiThreadInfo>()
        };

        var focusedControl = GetGUIThreadInfo(threadId, ref info)
            ? info.FocusedWindow
            : IntPtr.Zero;

        return new FocusSnapshot(foregroundWindow, focusedControl, threadId);
    }

    public static bool RestoreFocus(FocusSnapshot snapshot)
    {
        if (snapshot.ForegroundWindow == IntPtr.Zero || !IsWindow(snapshot.ForegroundWindow))
        {
            return false;
        }

        var currentThreadId = GetCurrentThreadId();
        var attached = snapshot.ThreadId != 0 && snapshot.ThreadId != currentThreadId &&
                       AttachThreadInput(currentThreadId, snapshot.ThreadId, true);

        try
        {
            _ = BringWindowToTop(snapshot.ForegroundWindow);
            _ = SetForegroundWindow(snapshot.ForegroundWindow);
            _ = SetActiveWindow(snapshot.ForegroundWindow);

            if (snapshot.FocusedControl != IntPtr.Zero && IsWindow(snapshot.FocusedControl))
            {
                _ = SetFocus(snapshot.FocusedControl);
            }
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThreadId, snapshot.ThreadId, false);
            }
        }

        return GetForegroundWindow() == snapshot.ForegroundWindow;
    }

    public static SendInputResult SendCtrlV()
    {
        var inputs = new[]
        {
            KeyDown(0x11),
            KeyDown(VkV),
            KeyUp(VkV),
            KeyUp(0x11)
        };

        var count = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        var errorCode = count == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        return new SendInputResult(count, errorCode, Marshal.SizeOf<Input>());
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var builder = new StringBuilder(512);
        return GetWindowText(handle, builder, builder.Capacity) > 0
            ? builder.ToString()
            : "";
    }

    private static Input KeyDown(int virtualKey)
    {
        return new Input
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey
                }
            }
        };
    }

    private static Input KeyUp(int virtualKey)
    {
        return new Input
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    Flags = 0x0002
                }
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    public readonly record struct FocusSnapshot(
        IntPtr ForegroundWindow,
        IntPtr FocusedControl,
        uint ThreadId);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusedWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect MonitorArea;
        public Rect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    public readonly record struct SendInputResult(uint Count, int ErrorCode, int StructureSize);

    public readonly record struct WindowPlacement(
        bool Success,
        int CursorX,
        int CursorY,
        int WindowX,
        int WindowY,
        int Width,
        int Height);
}
