using System;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly string[] FrameRateLabels={"60 FPS","120 FPS","垂直同步","无限制"};
        static readonly string[] WindowLabels={"1280×800 窗口","1600×900 窗口","1920×1080 窗口","无边框全屏"};
        static readonly string[] VolumeLabels={"主音量","音效音量","环境声量"};

        void QualityDialog() => OpenDialog("显示与声音",BuildPresentation,maxHeight:680,maxWidth:710);

        void BuildPresentation(RectTransform p,float w)
        {
            var settings=app.Presentation;
            QualityChoices(p,w,0,QualityDialog);
            string detail=app.View.QualityLevel==2?"4× 抗锯齿 · 极高精度阴影 · 四级级联":
                app.View.QualityLevel==1?"2× 抗锯齿 · 高精度阴影 · 两级级联":"关闭多重采样 · 中精度硬阴影";
            Label(p,detail,0,43,w,29,portrait?12:14,pale);
            float y=81;
            if(PlayerPresentation.Desktop)
            {
                y=PresentationChoices(p,w,y,"帧率限制","Settings.Fps.",FrameRateLabels,settings.FrameRateMode,settings.SetFrameRate);
                y=PresentationChoices(p,w,y,"窗口模式","Settings.Window.",WindowLabels,settings.WindowMode,settings.SetWindow);
            }

            float toggleWidth=w/3;
            PresentationToggle(p,"Settings.Shake","镜头微震",settings.Shake,0,y,toggleWidth,settings.ToggleShake);
            PresentationToggle(p,"Settings.Mute","静音",settings.AudioMuted,toggleWidth,y,toggleWidth,settings.ToggleMute);
            PresentationToggle(p,"Settings.Ambience","环境声",settings.AmbienceEnabled,toggleWidth*2,y,toggleWidth,settings.ToggleAmbience);
            y+=43;
            for(int i=0;i<VolumeLabels.Length;i++)
            {
                int bus=i;
                Label(p,VolumeLabels[bus],0,y,83,34,13,pale);
                var readout=Label(p,settings.Volume(bus)+"%",w-55,y,55,34,13,white,TextAnchor.MiddleRight);
                var slider=Slider(p,89,y+11,w-151,13,100,value=>{
                    settings.SetVolume(bus,Mathf.RoundToInt(value));
                    readout.text=settings.Volume(bus)+"%";
                });
                slider.name="Settings.Volume."+bus;
                slider.wholeNumbers=true;
                slider.SetValueWithoutNotify(settings.Volume(bus));
                y+=37;
            }
            p.sizeDelta=new Vector2(w,y);
        }

        void PresentationToggle(RectTransform p,string id,string name,bool enabled,float x,float y,float w,Action toggle)
        {
            var button=Button(p,name+(enabled?" 开":" 关"),x,y,w-5,34,()=>{
                toggle();QualityDialog();
            },accent:enabled);
            button.name=id;
        }

        float PresentationChoices(RectTransform p,float w,float y,string title,string prefix,string[] choices,int selected,Action<int> apply)
        {
            Label(p,title,0,y,w,23,14,gold);
            y+=26;
            int columns=portrait?2:4;
            float span=w/columns;
            for(int i=0;i<choices.Length;i++)
            {
                int index=i;
                var option=Button(p,choices[i],i%columns*span,y+i/columns*38,span-5,34,()=>{
                    apply(index);QualityDialog();
                },accent:i==selected);
                option.name=prefix+i;
            }
            return y+(choices.Length+columns-1)/columns*38+7;
        }
    }
}
