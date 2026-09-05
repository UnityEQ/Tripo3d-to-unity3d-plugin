using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEditor;

namespace Tripo3D.Editor
{
    /// <summary>
    /// Windows parks the Unity editor when it is not the foreground window, so script
    /// compile never starts. A thread-pool timer posts WM_NULL to the editor hwnd so the
    /// Win32 pump keeps running without stealing focus.
    /// </summary>
    [InitializeOnLoad]
    static class TripoEditorKeepAlive
    {
        const int WmNull = 0x0000;

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        static readonly Timer Timer;
        static readonly MethodInfo SignalTick;
        static IntPtr _hwnd;

        static TripoEditorKeepAlive()
        {
            SignalTick = typeof(EditorApplication).GetMethod(
                "SignalTick",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _hwnd = Process.GetCurrentProcess().MainWindowHandle;
            Timer = new Timer(_ => Pulse(), null, 0, 50);
        }

        static void Pulse()
        {
            try
            {
                if (_hwnd == IntPtr.Zero)
                    _hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (_hwnd != IntPtr.Zero)
                    PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);

                if (SignalTick != null)
                    EditorApplication.delayCall += InvokeSignalTick;
            }
            catch
            {
            }
        }

        static void InvokeSignalTick()
        {
            try
            {
                SignalTick.Invoke(null, null);
            }
            catch
            {
            }
        }
    }
}
