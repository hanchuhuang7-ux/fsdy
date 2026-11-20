using System;
using System.Collections;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleAudio:MonoBehaviour
    {
        const int VoiceCount=12;
        const int DrizzleBed=4, BedCount=5;
        static readonly string[] CueKeys={
            "melee","heavy","wall","block","bow","crossbow","bomber","catapult","explosion","order","heal",
            "charge","slam","build","harvest","recruit","forge","trade","milestone","dispatch","patrol"
        };

        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>(CueKeys.Length);
        readonly AudioSource[] voices=new AudioSource[VoiceCount], beds=new AudioSource[2];
        readonly AudioClip[] soundscapes=new AudioClip[BedCount];
        readonly float[] voiceLevels=new float[VoiceCount], bedLevels=new float[2];
        FrontlineApp app;
        BattleCameraShake shake;
        Deployment cueDeployment;
        uint variation=128413;
        int lastId,voice,activeBed,desiredBed;
        bool ready;
        internal int PlayingVoices
        {
            get
            {
                int count=0;
                foreach(var source in voices)if(source&&source.isPlaying)count++;
                foreach(var source in beds)if(source&&source.isPlaying)count++;
                return count;
            }
        }

        public void Initialize(FrontlineApp value)
        {
            app=value;
            if(app.Presentation.IsQa)return;
            for(int i=0;i<voices.Length;i++)voices[i]=Source(false);
            for(int i=0;i<beds.Length;i++)beds[i]=Source(true);
            shake=app.View.ViewCamera.gameObject.AddComponent<BattleCameraShake>();
            app.Presentation.Changed+=ApplyGains;
            app.ManorSound+=PlayManor;
            ready=true;
            StartCoroutine(WarmClips());
        }

        AudioSource Source(bool loop)
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.loop=loop;
            source.spatialBlend=0;
            source.priority=loop?192:96;
            return source;
        }

        IEnumerator WarmClips()
        {
            foreach(string key in CueKeys)
            {
                yield return null;
                GetClip(key);
            }
            for(int i=1;i<soundscapes.Length;i++)
            {
                yield return null;
                soundscapes[i]=MakeSoundscape(i);
            }
        }

        AudioClip GetClip(string key)
        {
            key??="melee";
            if(clips.TryGetValue(key,out var clip))return clip;
            if(Array.IndexOf(CueKeys,key)<0)return GetClip("melee");
            clips.Add(key,clip=MakeCue(key));
            return clip;
        }

        void ApplyGains()
        {
            var settings=app.Presentation;
            float effects=settings.EffectsGain, ambience=settings.AmbienceGain;
            for(int i=0;i<voices.Length;i++)
            {
                voices[i].volume=effects*voiceLevels[i];
                if(effects==0&&voices[i].isPlaying)voices[i].Stop();
            }
            for(int i=0;i<beds.Length;i++)beds[i].volume=ambience*bedLevels[i];
            if(!settings.Shake)shake.Clear();
        }

        void Play(string key,float level,float gain)
        {
            int index=voice++%VoiceCount;
            if(voice==int.MaxValue)voice=0;
            var source=voices[index];
            source.pitch=1+.07f*Noise(ref variation);
            voiceLevels[index]=level*(.92f+.08f*Noise(ref variation));
            source.volume=voiceLevels[index]*gain;
            source.clip=GetClip(key);
            source.Play();
        }

        void PlayManor(string key)
        {
            float gain=app.Presentation.EffectsGain;
            if(!app.Muted&&gain>0)Play(key,.75f,gain);
        }

        void UpdateSoundscape(bool battle)
        {
            float gain=app.Muted?0:app.Presentation.AmbienceGain;
            int wanted=gain==0?0:app.Phase=="manor"?ManorBed(app.View.Weather):battle?3:0;
            if(wanted!=desiredBed)
            {
                if(wanted!=0)
                {
                    if(!soundscapes[wanted])return;
                    activeBed^=1;
                    var source=beds[activeBed];
                    source.clip=soundscapes[wanted];
                    bedLevels[activeBed]=source.volume=0;
                    source.Play();
                }
                desiredBed=wanted;
            }
            for(int i=0;i<beds.Length;i++)
            {
                float next=Mathf.MoveTowards(bedLevels[i],i==activeBed&&wanted!=0?1:0,Time.unscaledDeltaTime*1.8f);
                if(next==bedLevels[i])continue;
                bedLevels[i]=next;
                beds[i].volume=next*gain;
                if(next==0)beds[i].Stop();
            }
        }

        static int ManorBed(ManorWeather weather) => weather switch
        {
            ManorWeather.Drizzle => DrizzleBed,
            ManorWeather.Flurry => 2,
            _ => 1
        };

        static bool HammerStrike(Frame frame,Cue cue)
        {
            foreach(var effect in frame.effects)
                if(effect.type=="slam"&&Mathf.Abs(effect.x-cue.x)<.6f&&Mathf.Abs(effect.z-cue.z)<.6f)return true;
            return false;
        }

        void Update()
        {
            if(!ready)return;
            bool battle=app.Phase=="battle"||app.Phase=="replay"||app.InFrontier;
            UpdateSoundscape(battle);
            var deployment=battle?app.Config:null;
            if(deployment!=cueDeployment)
            {
                lastId=0;
                cueDeployment=deployment;
                shake.Clear();
            }
            var frame=app.CurrentFrame;
            if(!battle||frame==null||frame.cues.Count==0)return;
            if(frame.cues[frame.cues.Count-1].id<lastId)lastId=0;
            float gain=app.Muted?0:app.Presentation.EffectsGain;
            bool allowShake=app.Presentation.Shake;
            int played=0;
            foreach(var cue in frame.cues)
            {
                if(cue.id<=lastId)continue;
                lastId=cue.id;
                if(app.Paused||frame.tick-cue.tick>12)continue;
                bool blast=cue.type=="explosion";
                if(allowShake&&(blast||cue.type=="slam"||cue.type=="heavy"&&HammerStrike(frame,cue)))
                {
                    var camera=app.View.ViewCamera.transform.position;
                    float distance=Vector3.Distance(camera,new Vector3(cue.x,cue.y,cue.z));
                    shake.Kick((blast?.105f:.067f)*Mathf.Clamp01(1.28f-distance*.014f));
                }
                if(gain==0||played>=4)continue;
                float level=blast||cue.type=="heavy"?.85f:cue.type=="bow"||cue.type=="heal"?.53f:.69f;
                Play(cue.type,level*(app.Speed>2?.60f:1),gain);
                played++;
            }
        }

        void OnDestroy()
        {
            if(app!=null)
            {
                app.ManorSound-=PlayManor;
                app.Presentation.Changed-=ApplyGains;
            }
            foreach(var clip in clips.Values)Destroy(clip);
            foreach(var clip in soundscapes)if(clip)Destroy(clip);
        }
    }
}
