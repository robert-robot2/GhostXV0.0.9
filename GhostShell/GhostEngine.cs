
using System.Runtime.InteropServices;

namespace GhostShell
{
    internal static class GhostEngine
    {
        private const string DllName = "GhostX.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GhostEngine_Init(IntPtr hwnd, uint width, uint height);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GhostEngine_Tick();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GhostEngine_Resize(uint width, uint height);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GhostEngine_Shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void GhostEngine_SetClearColor(float r, float g, float b);

    }
}