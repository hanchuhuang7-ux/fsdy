using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    internal enum ManorWeather { Clear, Drizzle, Leaves, Flurry }

    public sealed partial class BattleView
    {
        readonly struct SeasonalLight
        {
            public readonly Quaternion rotation;
            public readonly Color color;
            public readonly float intensity;

            public SeasonalLight(float pitch,float yaw,Color color,float intensity)
                : this(Quaternion.Euler(pitch,yaw,0),color,intensity) {}

            public SeasonalLight(Quaternion rotation,Color color,float intensity)
            {
                this.rotation=rotation;
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
        SeasonalLight sunTarget=ArenaLight;
        Batch weatherPage;
        static readonly Quaternion RainSlant=Quaternion.Euler(0,-18,18);
        static readonly Color RainTint=new Color(.72f,.85f,.94f,.49f);
        static readonly Color LeafTint=new Color(.97f,.65f,.17f,.82f);
        static readonly Color SnowTint=new Color(.90f,.96f,1f,.68f);
        internal ManorWeather Weather {get;private set;}
        internal int WeatherBudget => QualityLevel==0?12:QualityLevel==1?28:52;
        internal int WeatherParticles => manor==null?0:weatherPage.count;
        internal bool WeatherCastsShadows => weatherPage.mesh.castShadow;
        internal bool SeasonLightBlending => seasonLightBlending;
        internal Color SeasonLightColor => sun.color;
        internal Quaternion SeasonLightRotation => sun.transform.rotation;

        internal static ManorWeather WeatherForDay(int day)
        {
            if(day<=0)return ManorWeather.Clear;
            int season=(day-1)/Manor.SeasonDays%4;
            if(season==3)return ManorWeather.Flurry;
            if(season==2)return ManorWeather.Leaves;
            int cycle=(day+season*3)%7;
            return cycle==4||cycle==5?ManorWeather.Drizzle:ManorWeather.Clear;
        }

        void SetSeasonLight(int season,int day,bool smooth)
        {
            var weather=WeatherForDay(day);
            if(season==litSeason&&weather==Weather)return;
            Weather=weather;
            litSeason=season;
            sunTarget=season<0?ArenaLight:Seasons[season];
            if(weather==ManorWeather.Drizzle)
                sunTarget=new SeasonalLight(sunTarget.rotation,Color.Lerp(sunTarget.color,new Color(.83f,.90f,1f),.36f),
                    sunTarget.intensity*.92f);
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
                sun.transform.rotation=sunTarget.rotation;
                sun.color=sunTarget.color;
                sun.intensity=sunTarget.intensity;
            }
        }

        void AdvanceSeasonLight()
        {
            float t=Mathf.Clamp01((Time.unscaledTime-lightBlendStart)/.78f);
            float curve=t*t*(3-2*t);
            sun.transform.rotation=Quaternion.Slerp(sunFromRotation,sunTarget.rotation,curve);
            sun.color=Color.Lerp(sunFromColor,sunTarget.color,curve);
            sun.intensity=Mathf.Lerp(sunFromIntensity,sunTarget.intensity,curve);
            seasonLightBlending=t<1;
        }

        // One reusable page for every quality tier. This uses the existing translucent cross-quad mesh.
        void PrepareWeatherPage(float time)
        {
            weatherPage.Clear();
            if(Weather==ManorWeather.Clear)return;
            bool rain=Weather==ManorWeather.Drizzle, leaves=Weather==ManorWeather.Leaves;
            var tint=rain?RainTint:leaves?LeafTint:SnowTint;
            float width=Mathf.Clamp(distance*.61f,5.1f,72f), depth=Mathf.Clamp(distance*.36f,4f,25f);
            float xCenter=Mathf.Clamp(target.x,FrontierMap.West,FrontierMap.East);
            float zCenter=Mathf.Clamp(target.z,-FrontierMap.HalfDepth,FrontierMap.HalfDepth);
            float height=Mathf.Clamp(distance*.27f,3.7f,10f);
            float phase=time*(rain?1.22f:leaves?.32f:.28f)+manor.day*.267f;
            float visibilityScale=1+Mathf.Clamp01((distance-16)/65)*(rain||leaves?.68f:.90f);
            int count=WeatherBudget;
            for(int i=0;i<count;i++)
            {
                float speed=.78f+.42f*Mathf.Repeat(i*.733f,1);
                float flight=Mathf.Repeat(phase*speed+i*.419f,1);
                float sway=rain?flight*height*.30f:Mathf.Sin(time*.94f+i*2.12f)*(leaves?.68f:.26f)+flight*(leaves?1.23f:.55f);
                var position=new Vector3(xCenter+width*(2*Mathf.Repeat(.19f+i*.618f,1)-1)+sway,
                    .22f+height*(1-flight),zCenter+depth*(2*Mathf.Repeat(.35f+i*.377f,1)-1));
                float size=(.70f+.53f*Mathf.Repeat(i*.481f,1))*visibilityScale*
                    Mathf.Min(1,Mathf.Min(flight*10,(1-flight)*10));
                var rotation=rain?RainSlant:leaves?Quaternion.Euler(42+time*57+i*21,i*37+time*33,23+i*5):
                    Quaternion.Euler(11+i*47, i*137.5f+time*4, i*81+time*(i%2==0?7+3*speed:-8-2*speed));
                var scale=rain?new Vector3(.39f,5.4f,.39f):leaves?new Vector3(1.87f,.90f,.30f):Vector3.one*.96f;
                weatherPage.Add(Pose(position,rotation,scale*size),tint,0);
            }
        }

        internal long MeasureWeatherAllocations()
        {
            if(!AllocationMeter.Available)return -1;
            PrepareWeatherPage(manorTime);
            long start=AllocationMeter.Read();
            for(int i=0;i<128;i++)PrepareWeatherPage(manorTime+i*.043f);
            return AllocationMeter.Read()-start;
        }
    }
}
