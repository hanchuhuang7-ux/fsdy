using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
namespace Frontline.UnityGame
{
    public sealed class BattleAudio:MonoBehaviour
    {
        FrontlineApp app;readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly List<AudioSource> voices=new List<AudioSource>();int lastId,voice;
        public void Initialize(FrontlineApp a){app=a;foreach(var key in new[]{"melee","block","bow","crossbow","bomber","catapult","explosion","order","heal","cavalry","giant"})clips.Add(key,Make(key));for(int i=0;i<12;i++){var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;voices.Add(source);}}
        static AudioClip Make(string key){int length=key=="order"?14000:key=="explosion"?9000:3500;var samples=new float[length];uint seed=419;float hz=key=="order"?520:key=="heal"?780:key=="bow"?330:key=="block"?900:95;for(int i=0;i<length;i++){seed=unchecked(seed*1664525+1013904223);float noise=(seed>>8)/8388608f-1;float t=i/22050f,fade=1-i/(float)length;float tone=Mathf.Sin(t*hz*Mathf.PI*2)*(key=="order"?.7f:.3f);samples[i]=(tone+noise*(key=="explosion"?.7f:.2f))*fade*fade*.28f;}var clip=AudioClip.Create("Cue "+key,length,1,22050,false);clip.SetData(samples,0);return clip;}
        void Update(){if(!app||app.CurrentFrame==null)return;var f=app.CurrentFrame;if(app.Phase!="battle"&&app.Phase!="replay")return;if(f.cues.Count>0&&f.cues[f.cues.Count-1].id<lastId)lastId=0;int count=0;foreach(var cue in f.cues)if(cue.id>lastId){lastId=cue.id;if(app.Muted||app.Paused||f.tick-cue.tick>12||count++>=4)continue;if(!clips.TryGetValue(cue.type,out var clip))clip=clips["melee"];var source=voices[voice++%voices.Count];source.clip=clip;source.volume=app.Speed>2?.25f:.45f;source.Play();}}
        void OnDestroy(){foreach(var c in clips.Values)Destroy(c);}
    }
}
