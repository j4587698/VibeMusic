using Avalonia.Controls;
using Avalonia.Input;
using KuGouMusicAvalonia.ViewModels;

namespace KuGouMusicAvalonia.Views;

public partial class DesktopLyricsWindow : Window
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(System.IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(System.IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    // macOS P/Invoke
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern System.IntPtr sel_registerName(string name);

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern void objc_msgSend(System.IntPtr receiver, System.IntPtr selector, byte arg);

    // Linux X11 P/Invoke
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct XRectangle
    {
        public short x, y;
        public ushort width, height;
    }

    [System.Runtime.InteropServices.DllImport("libX11.so.6")]
    private static extern System.IntPtr XOpenDisplay(System.IntPtr display);

    [System.Runtime.InteropServices.DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(System.IntPtr display);

    [System.Runtime.InteropServices.DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(System.IntPtr display, System.IntPtr dest, int dest_kind, int x_off, int y_off, ref XRectangle rectangles, int n_rects, int op, int ordering);

    [System.Runtime.InteropServices.DllImport("libXext.so.6", EntryPoint = "XShapeCombineRectangles")]
    private static extern void XShapeCombineRectanglesEmpty(System.IntPtr display, System.IntPtr dest, int dest_kind, int x_off, int y_off, System.IntPtr rectangles, int n_rects, int op, int ordering);

    private const int ShapeInput = 2;
    private const int ShapeSet = 0;

    public DesktopLyricsWindow()
    {
        InitializeComponent();
        DataContext = new DesktopLyricsViewModel();
        KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, System.EventArgs e)
    {
        UpdatePassthroughState();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.StateChanged -= OnServiceStateChanged;
        KuGouMusicAvalonia.Services.LyricsService.Instance.EndWordHighlight();
        (DataContext as DesktopLyricsViewModel)?.Cleanup();
        base.OnClosed(e);
    }

    private void UpdatePassthroughState()
    {
        var isLocked = KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.IsLocked;
        
        // If locked, also force hide the hover state just in case
        if (isLocked && DataContext is DesktopLyricsViewModel vm)
        {
            vm.IsHovered = false;
        }

        var handleInfo = TryGetPlatformHandle();
        if (handleInfo == null) return;

        if (System.OperatingSystem.IsWindows() && handleInfo.HandleDescriptor == "HWND")
        {
            var hwnd = handleInfo.Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (isLocked)
                style |= (WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                style &= ~WS_EX_TRANSPARENT;
                
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
            SetWindowPos(hwnd, System.IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
        else if (System.OperatingSystem.IsMacOS() && handleInfo.HandleDescriptor == "NSWindow")
        {
            try
            {
                var nsWindow = handleInfo.Handle;
                var sel = sel_registerName("setIgnoresMouseEvents:");
                objc_msgSend(nsWindow, sel, isLocked ? (byte)1 : (byte)0);
            }
            catch { /* Ignore if P/Invoke fails */ }
        }
        else if (System.OperatingSystem.IsLinux() && handleInfo.HandleDescriptor == "XID")
        {
            try
            {
                var xid = handleInfo.Handle;
                var display = XOpenDisplay(System.IntPtr.Zero);
                if (display != System.IntPtr.Zero)
                {
                    if (isLocked)
                    {
                        XShapeCombineRectanglesEmpty(display, xid, ShapeInput, 0, 0, System.IntPtr.Zero, 0, ShapeSet, 0);
                    }
                    else
                    {
                        var rect = new XRectangle { x = 0, y = 0, width = (ushort)Bounds.Width, height = (ushort)Bounds.Height };
                        XShapeCombineRectangles(display, xid, ShapeInput, 0, 0, ref rect, 1, ShapeSet, 0);
                    }
                    XCloseDisplay(display);
                }
            }
            catch { /* Ignore if P/Invoke fails or XShape is unsupported */ }
        }
    }

    private void OnDragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm && !vm.IsLocked)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowOpened(object? sender, System.EventArgs e)
    {
        if (Screens.Primary is { } primaryScreen)
        {
            var workingArea = primaryScreen.WorkingArea;
            var x = workingArea.X + (workingArea.Width - Width) / 2;
            var y = workingArea.Bottom - Height - 80;
            Position = new Avalonia.PixelPoint((int)x, (int)y);
        }
        UpdatePassthroughState();
        KuGouMusicAvalonia.Services.LyricsService.Instance.BeginWordHighlight();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm && !KuGouMusicAvalonia.Services.DesktopLyricsWindowService.Instance.IsLocked)
        {
            vm.IsHovered = true;
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DesktopLyricsViewModel vm)
        {
            vm.IsHovered = false;
        }
    }
}
