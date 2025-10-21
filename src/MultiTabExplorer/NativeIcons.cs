using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MultiTabExplorer;

internal static class NativeIcons
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string? pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000; // 32x32
    private const uint SHGFI_SMALLICON = 0x000000001; // 16x16
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    public static Icon? GetSmallIconForFile(string path)
    {
        SHFILEINFO shinfo = new();
        IntPtr hImg = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);
        if (shinfo.hIcon != IntPtr.Zero)
        {
            try
            {
                var icon = (Icon?)Icon.FromHandle(shinfo.hIcon).Clone();
                return icon;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        return null;
    }

    public static Icon? GetSmallIconForFolder()
    {
        SHFILEINFO shinfo = new();
        IntPtr hImg = SHGetFileInfo(null, FILE_ATTRIBUTE_DIRECTORY, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
        if (shinfo.hIcon != IntPtr.Zero)
        {
            try
            {
                var icon = (Icon?)Icon.FromHandle(shinfo.hIcon).Clone();
                return icon;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        return null;
    }
}
