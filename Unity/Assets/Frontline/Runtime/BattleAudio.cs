using System;
using System.Collections;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleAudio:MonoBehaviour
    {
        const int VoiceCount=12, CrowdVoices=8;
        const float EffectsHeadroom=.79f;
        const int DrizzleBed=4, BedCount=5;
        static readonly string[] CueKeys={
            "melee","heavy","wall","block","bow","crossbow","bomber","catapult","explosion","order","heal",
            "charge","slam","build","harvest","recruit","forge","trade","milestone","dispatch","patrol",
            "coronation","anvil","order_inspire","order_focus","order_fortify"
        };
        static readonly float[] CrowdWindows={.103f,.138f,.105f,.128f};

        struct CrowdTone
        {
            public double last,next;
            public float pressure;
        }

        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>(CueKeys.Length);
        readonly AudioSource[] voices=new AudioSource[VoiceCount], beds=new AudioSource[2];
        readonly AudioClip[] soundscapes=new AudioClip[BedCount];
        readonly float[] voiceLevels=new float[VoiceCount], bedLevels=new float[2];
        readonly CrowdTone[] crowd=new CrowdTone[CrowdWindows.Length];
        FrontlineApp app;
        BattleCameraShake shake;
        Deployment cueDeployment;
        uint variation=128413;
        int lastId,lastTick,orderCursor,voice,accentVoice,activeBed,desiredBed;
        float mixScale=1;
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
            for(int i=0;i<voices.Length;i++)
            {
                voices[i]=Source(false);
                voices[i].priority=i<CrowdVoices?112:72;
            }
            for(int i=0;i<beds.Length;i++)beds[i]=Source(true);
            shake=app.View.ViewCamera.gameObject.AddComponent<BattleCameraShake>();
            app.Presentation.Changed+=ApplyGains;
            app.ManorSound+=PlayManor;
            ready=true;
            GetClip("order_inspire");GetClip("order_focus");GetClip("order_fortify");
            GetClip("coronation");GetClip("anvil");GetClip("melee");
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
            ApplyVoiceMix(app.Muted?0:settings.EffectsGain,0);
            for(int i=0;i<beds.Length;i++)beds[i].volume=(app.Muted?0:settings.AmbienceGain)*bedLevels[i];
            if(!settings.Shake)shake.Clear();
        }

        bool Play(string key,float level,bool accent)
        {
            if(!clips.TryGetValue(key??"melee",out var clip)&&!clips.TryGetValue("melee",out clip))return false;
            int start=accent?CrowdVoices:0,count=accent?VoiceCount-CrowdVoices:CrowdVoices;
            int cursor=accent?accentVoice:voice;
            int index=start+cursor;
            for(int offset=0;offset<count;offset++)
            {
                int candidate=start+(cursor+offset)%count;
                if(voices[candidate].isPlaying)continue;
                index=candidate;
                break;
            }
            if(accent)accentVoice=(index-start+1)%count;
            else voice=(index+1)%count;
            var source=voices[index];
            source.pitch=1+.07f*Noise(ref variation);
            voiceLevels[index]=level*(.92f+.08f*Noise(ref variation));
            source.volume=0;
            source.clip=clip;
            source.Play();
            return true;
        }

        static int CrowdIndex(string key) => key switch
        {
            "melee"=>0,"block"=>1,"bow"=>2,"crossbow"=>3,_=>-1
        };

        static bool CrowdLevel(ref CrowdTone lane,int index,double now,out float level)
        {
            float elapsed=(float)(now-lane.last);
            lane.pressure=Mathf.Min(7,Mathf.Max(0,lane.pressure-elapsed*4.4f)+.58f);
            lane.last=now;
            level=(index>=2?.49f:.58f)/(1+.19f*lane.pressure);
            if(now<lane.next)return false;
            lane.next=now+CrowdWindows[index];
            return true;
        }

        static float VoiceMix(float current,float summed,float elapsed)
        {
            float target=summed>EffectsHeadroom?EffectsHeadroom/summed:1;
            // Fast attenuation preserves headroom on a transient. Recovery is inaudible between adjacent cues.
            return target<current?target:Mathf.MoveTowards(current,target,2.7f*elapsed);
        }

        void ApplyVoiceMix(float gain,float elapsed)
        {
            float total=0;
            for(int i=0;i<voices.Length;i++)
            {
                if(voiceLevels[i]==0)continue;
                if(gain==0||!voices[i].isPlaying)
                {
                    if(gain==0)voices[i].Stop();
                    voiceLevels[i]=0;
                }
                else total+=voiceLevels[i];
            }
            mixScale=VoiceMix(mixScale,total,elapsed);
            float scale=gain*mixScale;
            for(int i=0;i<voices.Length;i++)
            {
                float next=voiceLevels[i]*scale;
                if(voices[i].volume!=next)voices[i].volume=next;
            }
        }

        void PlayManor(string key)
        {
            float gain=app.Presentation.EffectsGain;
            if(!app.Muted&&gain>0)
            {
                GetClip(key);
                Play(key,.75f,true);
                ApplyVoiceMix(gain,0);
            }
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

        static bool HeavyContact(Frame frame,Cue cue)
        {
            foreach(var effect in frame.effects)
                if((effect.type=="slam"||effect.type=="charge")&&Mathf.Abs(effect.x-cue.x)<.6f&&Mathf.Abs(effect.z-cue.z)<.6f)return true;
            return false;
        }

        internal static string OrderCue(string order) => order switch
        {
            "inspire"=>"order_inspire","focus"=>"order_focus","fortify"=>"order_fortify",_=>"order"
        };

        int ReadOrderCues(Frame frame,float gain)
        {
            var orders=frame.commander.log;
            orderCursor=Math.Min(orderCursor,orders.Count);
            int played=0;
            while(orderCursor<orders.Count)
            {
                var entry=orders[orderCursor++];
                if(!app.Paused&&gain>0&&frame.tick-entry.tick<=12&&Play(OrderCue(entry.type),.73f,true))played++;
            }
            return played;
        }

        void ReadBattleCues(Frame frame,float gain,double now)
        {
            if(frame.tick<lastTick)lastId=0;
            lastTick=frame.tick;
            int accents=ReadOrderCues(frame,gain),ordinary=0;
            bool allowShake=app.Presentation.Shake;
            foreach(var cue in frame.cues)
            {
                if(cue.id<=lastId)continue;
                lastId=cue.id;
                if(app.Paused||frame.tick-cue.tick>12||cue.type=="order"&&frame.commander.log.Count>0)continue;
                bool blast=cue.type=="explosion",charge=cue.type=="charge";
                if(allowShake&&(blast||charge||cue.type=="slam"||cue.type=="heavy"&&HeavyContact(frame,cue)))
                {
                    var camera=app.View.ViewCamera.transform.position;
                    float distance=Vector3.Distance(camera,new Vector3(cue.x,cue.y,cue.z));
                    shake.Kick((blast?.105f:charge?.060f:.071f)*Mathf.Clamp01(1.28f-distance*.014f));
                }
                if(gain==0)continue;
                bool accent=blast||charge||cue.type=="heavy"||cue.type=="slam"||cue.type=="catapult"||cue.type=="order";
                if(accent?accents>=3:ordinary>=3)continue;
                int lane=CrowdIndex(cue.type);
                float level=accent?.81f:cue.type=="heal"?.48f:.60f;
                if(lane>=0&&!CrowdLevel(ref crowd[lane],lane,now,out level))continue;
                if(!Play(cue.type,level*(app.Speed>2?.60f:1),accent))continue;
                if(accent)accents++;else ordinary++;
            }
        }

        void Update()
        {
            if(!ready)return;
            bool battle=app.Phase=="battle"||app.Phase=="replay"||app.InFrontier;
            UpdateSoundscape(battle);
            var deployment=battle?app.Config:null;
            if(deployment!=cueDeployment)
            {
                lastId=lastTick=orderCursor=0;
                cueDeployment=deployment;
                shake.Clear();
                Array.Clear(crowd,0,crowd.Length);
            }
            var frame=app.CurrentFrame;
            float gain=app.Muted?0:app.Presentation.EffectsGain;
            if(battle&&frame!=null)ReadBattleCues(frame,gain,Time.unscaledTimeAsDouble);
            ApplyVoiceMix(gain,Time.unscaledDeltaTime);
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
