#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Frontline.UnityGame
{
    // The close button is tracked by uxtheme, which can synchronously send SC_CLOSE
    // to this window before the original non-client message has returned.
    internal static class WindowsCloseRequest
    {
        const uint SystemCommand=0x0112,Close=0x0010,DestroyNonClient=0x0082,CloseCommand=0xf060;
        static readonly UIntPtr subclassId=(UIntPtr)0x464c;
        static readonly WindowProcedure procedure=OnMessage;
        static readonly EnumerateWindow findPlayer=FindPlayer;
        static IntPtr player;
        static int nativeDispatchDepth;
        static uint pendingMessage;
        static bool committing;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr WindowProcedure(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr reference);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate bool EnumerateWindow(IntPtr window,IntPtr context);

        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,EnumerateWindow callback,IntPtr context);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int capacity);
        [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(IntPtr window,WindowProcedure callback,UIntPtr id,UIntPtr reference);
        [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(IntPtr window,WindowProcedure callback,UIntPtr id);
        [DllImport("comctl32.dll")] static extern IntPtr DefSubclassProc(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam);

        internal static bool Attach()
        {
            if(player==IntPtr.Zero)EnumThreadWindows(GetCurrentThreadId(),findPlayer,IntPtr.Zero);
            return player!=IntPtr.Zero;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumerateWindow))]
        static bool FindPlayer(IntPtr window,IntPtr context)
        {
            var name=new StringBuilder(48);
            if(GetClassName(window,name,name.Capacity)==0||name.ToString()!="UnityWndClass")return true;
            if(!SetWindowSubclass(window,procedure,subclassId,UIntPtr.Zero))return true;
            player=window;
            return false;
        }

        [AOT.MonoPInvokeCallback(typeof(WindowProcedure))]
        static IntPtr OnMessage(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,UIntPtr id,UIntPtr reference)
        {
            if(!committing&&(message==Close||(message==SystemCommand&&(wParam.ToUInt64()&0xfff0)==CloseCommand)))
            {
                if(pendingMessage==0)pendingMessage=message;
                return IntPtr.Zero;
            }
            if(message==DestroyNonClient)Detach();
            nativeDispatchDepth++;
            try {return DefSubclassProc(window,message,wParam,lParam);}
            finally {nativeDispatchDepth--;}
        }

        internal static bool CanQuit(bool interactive)
        {
            if(committing||(!interactive&&pendingMessage==0&&nativeDispatchDepth==0))return true;
            if(pendingMessage==0)pendingMessage=Close;
            return false;
        }

        internal static uint TakeOnMainLoop()
        {
            if(nativeDispatchDepth!=0||committing)return 0;
            uint message=pendingMessage;
            pendingMessage=0;
            return message;
        }

        internal static void Commit() => committing=true;

        internal static void Detach()
        {
            if(player==IntPtr.Zero)return;
            RemoveWindowSubclass(player,procedure,subclassId);
            player=IntPtr.Zero;
        }
    }
}
#endif
