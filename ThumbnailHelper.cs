using System;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows;
using System.IO;

namespace RenderOnShutdown
{
    internal static class ThumbnailHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem { }

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

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource? GetThumbnail(string path, int width = 160, int height = 90)
        {
            try
            {
                var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem).GUID, out var shellItem);
                var factory = shellItem as IShellItemImageFactory;
                if (factory == null) return null;
                var size = new SIZE { cx = width, cy = height };
                factory.GetImage(size, SIIGBF.SIIGBF_RESIZETOFIT, out var hbitmap);
                if (hbitmap == IntPtr.Zero) return null;
                var bitmap = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hbitmap);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
