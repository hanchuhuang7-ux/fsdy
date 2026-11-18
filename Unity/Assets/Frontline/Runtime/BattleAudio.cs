using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
namespace Frontline.UnityGame
{
    public sealed class BattleAudio:MonoBehaviour
    {
        FrontlineApp app;readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly List<AudioSource> voices=new List<AudioSource>();int lastId,voice;
        public void Initialize(FrontlineApp a){app=a;app.ManorSound+=PlayManor;foreach(var key in new[]{"melee","heavy","wall","block","bow","crossbow","bomber","catapult","explosion","order","heal","cavalry","giant","build","harvest","recruit"})clips.Add(key,Make(key));for(int i=0;i<12;i++){var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;voices.Add(source);}}
        static AudioClip Make(string key){bool manor=key=="build"||key=="harvest"||key=="recruit";int length=manor?5800:key=="order"?14000:key=="explosion"?9000:3500;var samples=new float[length];uint seed=419;float hz=key=="order"?520:key=="heal"?780:key=="bow"?330:key=="block"?1350:key=="wall"?48:key=="heavy"?65:95;for(int i=0;i<length;i++){seed=unchecked(seed*1664525+1013904223);float noise=(seed>>8)/8388608f-1;float t=i/22050f,fade=1-i/(float)length;float tone=Mathf.Sin(t*hz*Mathf.PI*2)*(key=="order"?.7f:.3f);if(key=="block")tone=(Mathf.Sin(t*1350*Mathf.PI*2)+Mathf.Sin(t*2183*Mathf.PI*2)*.55f+Mathf.Sin(t*3521*Mathf.PI*2)*.22f)*.38f;
                if(manor){
                    float note=key=="build"?(t<.06f?256:384):key=="harvest"?(t<.07f?523:784):(t<.08f?392:523);
                    tone=Mathf.Sin(2*Mathf.PI*note*t)*.7f;noise*=key=="build"?.6f:.08f;
                }
                samples[i]=(tone+noise*(key=="explosion"?.7f:key=="wall"?.5f:.2f))*fade*fade*.28f;}var clip=AudioClip.Create("Cue "+key,length,1,22050,false);clip.SetData(samples,0);return clip;}
        void Update(){if(!app||app.CurrentFrame==null)return;var f=app.CurrentFrame;if(app.Phase!="battle"&&app.Phase!="replay")return;if(f.cues.Count>0&&f.cues[f.cues.Count-1].id<lastId)lastId=0;int count=0;foreach(var cue in f.cues)if(cue.id>lastId){lastId=cue.id;if(app.Muted||app.Paused||f.tick-cue.tick>12||count++>=4)continue;if(!clips.TryGetValue(cue.type,out var clip))clip=clips["melee"];var source=voices[voice++%voices.Count];source.clip=clip;source.volume=app.Speed>2?.25f:.45f;source.Play();}}
        void PlayManor(string key)
        {
            if(app.Muted||!clips.TryGetValue(key,out var clip))return;
            var source=voices[voice++%voices.Count];source.clip=clip;source.volume=.32f;source.Play();
        }
        void OnDestroy(){if(app!=null)app.ManorSound-=PlayManor;foreach(var c in clips.Values)Destroy(c);}
    }
}
