using System;
using UnityEngine;

namespace Frontline.UnityGame
{
    internal sealed class PlayerPresentation
    {
        const string LivePrefix="frontline.presentation.", QaPrefix="frontline.qa.presentation.";
        readonly string prefix;
        readonly int[] volumes=new int[3];
        bool dirty;

        public PlayerPresentation(bool qa)
        {
            IsQa=qa;
            prefix=qa?QaPrefix:LivePrefix;
            FrameRateMode=Read("fps",0,3);
            WindowMode=Read("window",0,3);
            Shake=Read("shake",1,1)!=0;
            AudioMuted=Read("mute",0,1)!=0;
            AmbienceEnabled=Read("ambience",1,1)!=0;
            volumes[0]=Read("volume0",80,100);
            volumes[1]=Read("volume1",75,100);
            volumes[2]=Read("volume2",45,100);
        }

        public static bool Desktop => Application.platform==RuntimePlatform.WindowsPlayer ||
            Application.platform==RuntimePlatform.OSXPlayer || Application.platform==RuntimePlatform.LinuxPlayer;
        public bool IsQa {get;}
        public int FrameRateMode {get;private set;}
        public int WindowMode {get;private set;}
        public bool Shake {get;private set;}
        public bool AudioMuted {get;private set;}
        public bool AmbienceEnabled {get;private set;}
        public float EffectsGain => AudioMuted?0:volumes[0]*volumes[1]*.0001f;
        public float AmbienceGain => AudioMuted||!AmbienceEnabled?0:volumes[0]*volumes[2]*.0001f;
        public event Action Changed;

        int Read(string key,int fallback,int max) => Mathf.Clamp(PlayerPrefs.GetInt(prefix+key,fallback),0,max);

        void Write(string key,int value)
        {
            PlayerPrefs.SetInt(prefix+key,value);
            dirty=true;
            Changed?.Invoke();
        }

        public void Flush()
        {
            if(!dirty)return;
            PlayerPrefs.Save();
            dirty=false;
        }

        public void ApplyStartup()
        {
            ApplyFrameRate();
            if(Desktop&&!IsQa&&PlayerPrefs.HasKey(prefix+"window"))ApplyResolution();
        }

        void ApplyFrameRate()
        {
            QualitySettings.vSyncCount=Desktop&&FrameRateMode==2?1:0;
            Application.targetFrameRate=Application.platform==RuntimePlatform.WebGLPlayer?-1:FrameRateMode switch {
                0=>60,1=>120,_=>-1
            };
        }

        public void SetFrameRate(int mode)
        {
            if((uint)mode>3)throw new ArgumentOutOfRangeException(nameof(mode));
            FrameRateMode=mode;
            ApplyFrameRate();
            Write("fps",mode);
        }

        void ApplyResolution()
        {
            if(WindowMode==3)
            {
                var desktop=Screen.currentResolution;
                Screen.SetResolution(desktop.width,desktop.height,FullScreenMode.FullScreenWindow);
                return;
            }
            Screen.SetResolution(WindowMode==0?1280:WindowMode==1?1600:1920,
                WindowMode==0?800:WindowMode==1?900:1080,FullScreenMode.Windowed);
        }

        public void SetWindow(int mode)
        {
            if(!Desktop||(uint)mode>3)throw new ArgumentOutOfRangeException(nameof(mode));
            WindowMode=mode;
            ApplyResolution();
            Write("window",mode);
        }

        public void ToggleShake() { Shake=!Shake;Write("shake",Shake?1:0); }
        public void ToggleMute() { AudioMuted=!AudioMuted;Write("mute",AudioMuted?1:0); }
        public void ToggleAmbience() { AmbienceEnabled=!AmbienceEnabled;Write("ambience",AmbienceEnabled?1:0); }
        public int Volume(int bus) => volumes[bus];

        public void SetVolume(int bus,int volume)
        {
            volume=Mathf.Clamp(volume,0,100);
            if(volumes[bus]==volume)return;
            volumes[bus]=volume;
            Write("volume"+bus,volume);
        }

        internal static void ResetQa()
        {
            foreach(var key in new[]{"fps","window","shake","mute","ambience","volume0","volume1","volume2"})
                PlayerPrefs.DeleteKey(QaPrefix+key);
        }
    }
}
