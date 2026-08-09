using System.Drawing;
using System.Runtime.InteropServices;

namespace ShareSwitch;

/// <summary>
/// Thin, safe wrapper around the DWM thumbnail API.
/// One instance manages at most one live thumbnail registration.
/// Destination/source are supplied at <see cref="Register"/> time so the owner
/// can re-register after a WinForms handle recreation.
/// </summary>
public sealed class DwmThumbnail : IDisposable
{
    private IntPtr _thumbnailId = IntPtr.Zero;

    public bool IsRegistered => _thumbnailId != IntPtr.Zero;

    /// <summary>
    /// Registers a live thumbnail of <paramref name="sourceHwnd"/> inside
    /// <paramref name="destinationHwnd"/>. Replaces any previous registration.
    /// </summary>
    /// <exception cref="InvalidOperationException">DWM refused the registration
    /// (e.g. protected/secured window, invalid handle).</exception>
    public void Register(IntPtr destinationHwnd, IntPtr sourceHwnd)
    {
        Unregister();

        int hr = DwmRegisterThumbnail(destinationHwnd, sourceHwnd, out _thumbnailId);
        if (hr != 0)
        {
            _thumbnailId = IntPtr.Zero;
            throw new InvalidOperationException(
                $"DwmRegisterThumbnail failed (HRESULT 0x{hr:X8}). " +
                "The window may be protected or no longer exist.");
        }
    }

    /// <summary>Native pixel size of the source window content, or Size.Empty.</summary>
    public Size GetSourceSize()
    {
        if (!IsRegistered) return Size.Empty;
        int hr = DwmQueryThumbnailSourceSize(_thumbnailId, out SIZE size);
        return hr == 0 ? new Size(size.Width, size.Height) : Size.Empty;
    }

    /// <summary>Sets where (in destination client coordinates) the thumbnail is drawn.</summary>
    public void SetDestinationRect(Rectangle dest)
    {
        if (!IsRegistered) return;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new RECT { Left = dest.Left, Top = dest.Top, Right = dest.Right, Bottom = dest.Bottom },
            fVisible = true,
            fSourceClientAreaOnly = true,
            opacity = 255
        };
        DwmUpdateThumbnailProperties(_thumbnailId, ref props);
    }

    public void Unregister()
    {
        if (!IsRegistered) return;
        try { DwmUnregisterThumbnail(_thumbnailId); }
        catch { /* DWM may already have discarded it (e.g. source destroyed) */ }
        finally { _thumbnailId = IntPtr.Zero; }
    }

    public void Dispose() => Unregister();

    #region P/Invoke

    private const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    private const uint DWM_TNP_VISIBLE = 0x00000008;
    private const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr phThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES props);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnailId, out SIZE size);

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int Width, Height; }

    #endregion
}
