using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using WinRT;
using WinColor = Windows.UI.Color;

namespace GalgameManager.Helpers;

public static class TransparentGlassHelper
{
    /// <summary>
    /// 颜色转换：hex RGB → Color，非法输入返回白色
    /// </summary>
    public static WinColor ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length != 6)
            return WinColor.FromArgb(255, 255, 255, 255);
        try
        {
            return WinColor.FromArgb(255,
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        catch
        {
            return WinColor.FromArgb(255, 255, 255, 255);
        }
    }
    public static void Apply(Window window, byte alpha = 5, byte r = 255, byte g = 255, byte b = 255)
    {
        try
        {
            var hwnd = Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
            if (hwnd == IntPtr.Zero) return;
            ExtendFrameIntoClientArea(hwnd);//扩展Frame到窗口
            EnableBlurBehind(hwnd);//启用模糊效果
            SetTransparentBackdrop(window, alpha, r, g, b);//设置透明属性
            RemoveRoundedCorners(hwnd);//移除圆角
        }
        catch { }
    }

    public static void Remove(Window window)
    {
        try
        {
            window.As<ICompositionSupportsSystemBackdrop>().SystemBackdrop = null;
        }
        catch { }
    }

    private static void ExtendFrameIntoClientArea(IntPtr hwnd)
    {
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static void EnableBlurBehind(IntPtr hwnd)
    {
        var rgn = CreateRectRgn(-2, -2, -1, -1);
        var blurBehind = new DWM_BLURBEHIND
        {
            dwFlags = DWM_BB.Enable | DWM_BB.BlurRegion,
            fEnable = true,
            hRgnBlur = rgn
        };
        DwmEnableBlurBehindWindow(hwnd, ref blurBehind);
    }

    private static void SetTransparentBackdrop(Window window, byte alpha, byte r, byte g, byte b)
    {
        var compositor = new Windows.UI.Composition.Compositor();
        var brush = compositor.CreateColorBrush(WinColor.FromArgb(alpha, r, g, b));
        window.As<ICompositionSupportsSystemBackdrop>().SystemBackdrop = brush;
    }

    private static void RemoveRoundedCorners(IntPtr hwnd)
    {
        var preference = 1;
        DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.WindowCornerPreference,
            ref preference, Marshal.SizeOf<int>());
    }

    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private enum DWM_BB : uint { Enable = 1, BlurRegion = 2 }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public DWM_BB dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    private enum DWMWINDOWATTRIBUTE : uint { WindowCornerPreference = 33 }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr,
        ref int pvAttr, int cbAttr);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    #endregion
}
