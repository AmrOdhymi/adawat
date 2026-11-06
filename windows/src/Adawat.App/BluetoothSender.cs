using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;

namespace Adawat.App;

/// <summary>
/// Sends files to a paired Bluetooth device by dropping them onto the
/// shell "Send To › Bluetooth" shortcut's IDropTarget – the exact same
/// mechanism Windows Explorer uses.
/// </summary>
internal static class BluetoothSender
{
    // ── COM interfaces (only the vtable slots we actually call) ──────────

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void _ParseDisplayName();   // slot 0
        void _EnumObjects();        // slot 1
        void _BindToObject();       // slot 2
        void _BindToStorage();      // slot 3
        void _CompareIDs();         // slot 4
        void _CreateViewObject();   // slot 5
        void _GetAttributesOf();    // slot 6
        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
            [In] ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);
    }

    [ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleDropTarget
    {
        [PreserveSig] int DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject pDataObj, uint grfKeyState, long pt, ref uint pdwEffect);
        [PreserveSig] int DragOver(uint grfKeyState, long pt, ref uint pdwEffect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop(System.Runtime.InteropServices.ComTypes.IDataObject pDataObj, uint grfKeyState, long pt, ref uint pdwEffect);
    }

    // ── P/Invoke ────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, [In] ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    private static readonly Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IID_IDropTarget  = new("00000122-0000-0000-C000-000000000046");

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Opens the native Windows Bluetooth file transfer wizard (fsquirt) for sending and receiving.</summary>
    public static void OpenBluetoothTransfer()
    {
        try
        {
            var fsquirt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "fsquirt.exe");
            if (File.Exists(fsquirt))
            {
                Process.Start(new ProcessStartInfo(fsquirt) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Bluetooth transfer open error: {ex}");
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
            }
            catch { }
        }
    }

    public static bool Send(IntPtr hwndOwner, IReadOnlyList<string> files)
    {
        if (files.Count == 0) return false;

        // 1. Locate the Bluetooth shortcut in the user's SendTo folder.
        var sendTo = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
        if (string.IsNullOrEmpty(sendTo) || !Directory.Exists(sendTo)) return false;

        var btLink = Directory.GetFiles(sendTo)
            .FirstOrDefault(f =>
            {
                var n = Path.GetFileName(f);
                return n.IndexOf("Bluetooth", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("بلوتوث",    StringComparison.OrdinalIgnoreCase) >= 0;
            });
        if (btLink == null) return false;

        // 2. Parse the shortcut path into a PIDL.
        if (SHParseDisplayName(btLink, IntPtr.Zero, out var pidlLink, 0, out _) != 0)
            return false;

        IntPtr pFolder = IntPtr.Zero;
        try
        {
            // 3. Get the parent IShellFolder + relative child PIDL.
            var iidSF = IID_IShellFolder;
            if (SHBindToParent(pidlLink, ref iidSF, out pFolder, out var pidlChild) != 0)
                return false;

            var folder  = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pFolder, typeof(IShellFolder));
            var iidDrop = IID_IDropTarget;

            // 4. Ask the shortcut for its IDropTarget.
            if (folder.GetUIObjectOf(hwndOwner, 1, [pidlChild], ref iidDrop, IntPtr.Zero, out var pDrop) != 0
                || pDrop == IntPtr.Zero)
                return false;

            try
            {
                var drop = (IOleDropTarget)Marshal.GetObjectForIUnknown(pDrop);

                // 5. Build a COM data-object carrying the file paths.
                var data    = new DataObject(DataFormats.FileDrop, files.ToArray());
                var comData = (System.Runtime.InteropServices.ComTypes.IDataObject)data;

                // 6. Simulate a drop – exactly what Explorer does.
                uint effect = 1; // DROPEFFECT_COPY
                if (drop.DragEnter(comData, 0, 0, ref effect) != 0) return false;
                effect = 1;
                return drop.Drop(comData, 0, 0, ref effect) == 0;
            }
            finally { Marshal.Release(pDrop); }
        }
        finally
        {
            CoTaskMemFree(pidlLink);  // also invalidates pidlChild – OK, we're done with it
            if (pFolder != IntPtr.Zero) Marshal.Release(pFolder);
        }
    }
}
