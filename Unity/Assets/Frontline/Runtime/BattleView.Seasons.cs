using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        readonly struct SeasonalLight
        {
            public readonly Quaternion rotation;
            public readonly Color color;
            public readonly float intensity;

            public SeasonalLight(float pitch,float yaw,Color color,float intensity)
            {
                rotation=Quaternion.Euler(pitch,yaw,0);
                this.color=color;
                this.intensity=intensity;
            }
        }

        static readonly SeasonalLight ArenaLight=new SeasonalLight(48,-32,new Color(1,.92f,.8f),.95f);
        static readonly SeasonalLight[] Seasons={
            new SeasonalLight(51,-35,new Color(1,.95f,.85f),.96f),
            new SeasonalLight(64,-25,new Color(1,.91f,.72f),.98f),
            new SeasonalLight(39,-44,new Color(1,.80f,.60f),.95f),
            new SeasonalLight(31,-18,new Color(.83f,.90f,1),.90f)
        };

        Quaternion sunFromRotation;
        Color sunFromColor;
        float sunFromIntensity,lightBlendStart;
        int litSeason=-1;
        bool seasonLightBlending;
        internal bool SeasonLightBlending => seasonLightBlending;
        internal Color SeasonLightColor => sun.color;
        internal Quaternion SeasonLightRotation => sun.transform.rotation;

        void SetSeasonLight(int season,bool smooth)
        {
            if(season==litSeason)return;
            litSeason=season;
            seasonLightBlending=smooth;
            if(smooth)
            {
                sunFromRotation=sun.transform.rotation;
                sunFromColor=sun.color;
                sunFromIntensity=sun.intensity;
                lightBlendStart=Time.unscaledTime;
            }
            else
            {
                var light=season<0?ArenaLight:Seasons[season];
                sun.transform.rotation=light.rotation;
                sun.color=light.color;
                sun.intensity=light.intensity;
            }
        }

        void AdvanceSeasonLight()
        {
            float t=Mathf.Clamp01((Time.unscaledTime-lightBlendStart)/.78f);
            float curve=t*t*(3-2*t);
            var light=Seasons[litSeason];
            sun.transform.rotation=Quaternion.Slerp(sunFromRotation,light.rotation,curve);
            sun.color=Color.Lerp(sunFromColor,light.color,curve);
            sun.intensity=Mathf.Lerp(sunFromIntensity,light.intensity,curve);
            seasonLightBlending=t<1;
        }
    }
}
