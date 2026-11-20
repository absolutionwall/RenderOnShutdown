using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace RenderOnShutdown
{
    internal static class ThumbnailHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            void GetImage([In] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [Flags]
        private enum SIIGBF : uint
        {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10,
        }

        public static BitmapSource? GetThumbnail(string path, int width = 160, int height = 90)
        {
            var hbitmap = IntPtr.Zero;
            IShellItemImageFactory? factory = null;
            try
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                if (factory == null) return null;
                factory.GetImage(new SIZE { cx = width, cy = height }, SIIGBF.SIIGBF_RESIZETOFIT, out hbitmap);
                if (hbitmap == IntPtr.Zero) return null;
                var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }
    }
}
