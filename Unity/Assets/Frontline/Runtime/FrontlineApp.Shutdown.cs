using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Frontline.UnityGame
{
    public sealed partial class FrontlineApp
    {
        bool shuttingDown;
        long shutdownStarted;
        static readonly bool TraceShutdown=Array.IndexOf(Environment.GetCommandLineArgs(),"--trace-shutdown")>=0;

        void OnApplicationQuit()
        {
            Shutdown();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StopWindowsCloseRequest();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool observeWindowsClose,interactiveWindowsPlayer;

        void StartWindowsCloseRequest(bool interactive)
        {
            interactiveWindowsPlayer=interactive;
            observeWindowsClose=true;
            Application.wantsToQuit+=WindowsWantsToQuit;
            bool attached=WindowsCloseRequest.Attach();
            if(TraceShutdown)Debug.Log("FRONTLINE_WINDOW_CLOSE_READY subclass="+attached);
        }

        bool WindowsWantsToQuit() => WindowsCloseRequest.CanQuit(interactiveWindowsPlayer);

        void OnApplicationFocus(bool focused)
        {
            if(focused&&observeWindowsClose)WindowsCloseRequest.Attach();
        }

        bool FinishWindowsCloseRequest()
        {
            uint message=WindowsCloseRequest.TakeOnMainLoop();
            if(message==0)return false;
            if(TraceShutdown)Debug.Log($"FRONTLINE_WINDOW_CLOSE dispatched=0x{message:X} on player loop");
            Shutdown();
            WindowsCloseRequest.Commit();
            Application.Quit(0);
            return true;
        }

        void StopWindowsCloseRequest()
        {
            if(!observeWindowsClose)return;
            observeWindowsClose=false;
            Application.wantsToQuit-=WindowsWantsToQuit;
            WindowsCloseRequest.Detach();
        }
#endif

        void Shutdown()
        {
            if(shuttingDown)return;
            shuttingDown=true;
            enabled=false;
            shutdownStarted=Stopwatch.GetTimestamp();
            MarkShutdown("begin "+Phase);

            // Estate mutations and committed settlements already save synchronously.
            // Flush deferred presentation settings before detaching their consumers.
            if(Presentation!=null)ShutdownStep(Presentation.Flush,"preferences");
            ShutdownStep(StopJob,"background command");
            var battle=Battle;
            Battle=null;
            if(battle!=null)ShutdownStep(battle.Dispose,"battle workers and physics");
            if(View)ShutdownStep(View.StopPresentation,"pose jobs and drawing");
            if(TryGetComponent<BattleAudio>(out var audio))ShutdownStep(audio.StopAudio,"audio sources and clips");
            MarkShutdown("ready for engine shutdown");
        }

        void ShutdownStep(Action step,string label)
        {
            try{step();}
            catch(Exception error){Debug.LogException(error);}
            MarkShutdown(label);
        }

        void MarkShutdown(string label)
        {
            if(TraceShutdown)
            {
                double elapsed=(Stopwatch.GetTimestamp()-shutdownStarted)*1000.0/Stopwatch.Frequency;
                Debug.Log($"FRONTLINE_SHUTDOWN {elapsed:F2}ms {label}");
            }
        }

        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StopWindowsCloseRequest();
#endif
            Shutdown();
            MarkShutdown("destroy");
        }
    }
}
