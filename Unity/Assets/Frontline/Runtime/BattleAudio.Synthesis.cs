using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleAudio
    {
        const int SampleRate=22050;
        const float Tau=2*Mathf.PI;

        static float Noise(ref uint state)
        {
            state=unchecked(state*1664525+1013904223);
            return (state>>8)/8388608f-1;
        }
        static float Wave(float hz,float t)=>Mathf.Sin(Tau*hz*t);
        static float Envelope(float t,float attack,float decay) =>
            Mathf.Min(1,t/attack)*Mathf.Exp(-decay*t);

        static float Metal(float t,float frequency,float ringing)
        {
            if(t<0)return 0;
            return (.46f*Wave(frequency,t)+.26f*Wave(frequency*1.48f,t)+.18f*Wave(frequency*2.39f,t)+
                .09f*Wave(frequency*3.07f,t))*Envelope(t,.002f,ringing);
        }

        static float Bell(float t,float frequency)
        {
            if(t<0)return 0;
            var attack=Mathf.Min(1,t/.004f);
            return attack*(.44f*Wave(frequency,t)*Mathf.Exp(-4.4f*t)+
                .24f*Wave(frequency*2.01f,t)*Mathf.Exp(-6.4f*t)+
                .18f*Wave(frequency*2.78f,t)*Mathf.Exp(-9.5f*t));
        }

        static float Horn(float t,float frequency,float length)
        {
            if(t<0||t>=length)return 0;
            float swell=Mathf.SmoothStep(0,1,Mathf.Min(t/.05f,(length-t)/.09f));
            float phase=t+.0009f*Wave(6f,t);
            return swell*(.47f*Wave(frequency,phase)+.21f*Wave(frequency*2,phase)+.11f*Wave(frequency*3,phase));
        }

        static float Drum(float t,float low,float frequency,float decay) =>
            (.68f*Wave(frequency,t+.01f*(1-Mathf.Exp(-32*t)))+.32f*low)*Envelope(t,.006f,decay);

        static float CueSample(string key,float t,float noise,float low) => key switch
        {
            "block" => Metal(t,1177,20)+noise*Envelope(t,.002f,105)*.29f,
            "forge" => .88f*Metal(t,987,15)+.52f*Metal(t-.092f,1332,20)+noise*Envelope(t,.002f,83)*.21f,
            "trade" => .67f*Metal(t,1801,22)+.49f*Metal(t-.045f,2284,30)+.36f*Metal(t-.112f,1998,26),
            "milestone" => Bell(t,392)+.67f*Bell(t-.19f,523.25f),
            "dispatch" => Horn(t,196,.22f)+.91f*Horn(t-.25f,294,.42f),
            "patrol" => Horn(t,220,.24f)+Horn(t-.21f,330,.43f),
            "order" => Horn(t,329.63f,.39f),
            "bow" => .59f*Wave(277,t+.019f*(1-Mathf.Exp(-58*t)))*Envelope(t,.002f,38)+noise*Envelope(t,.005f,30)*.34f,
            "crossbow" => .65f*Wave(166,t)*Envelope(t,.0015f,28)+noise*Envelope(t,.002f,50)*.52f,
            "explosion" => Drum(t,low,43,8)+.41f*noise*Envelope(t,.003f,25)+.45f*low*Envelope(t,.019f,5),
            "catapult" => Drum(t,low,59,13)+noise*Envelope(t,.044f,19)*.36f,
            "bomber" => .5f*Drum(t,low,74,17)+noise*Envelope(t,.014f,25)*.34f,
            "heavy" or "slam" => Drum(t,low,54,12)+noise*Envelope(t,.002f,40)*.24f,
            "wall" => Drum(t,low,56,21)+noise*Envelope(t,.004f,29)*.31f,
            "charge" => Drum(t,low,81,17)+.33f*Drum(Mathf.Max(0,t-.12f),low,63,21),
            "heal" => Bell(t,523.25f)*.66f+Bell(t-.06f,659.25f)*.36f,
            "build" => .6f*Drum(t,low,118,21)+noise*Envelope(t,.002f,49)*.30f,
            "harvest" => .49f*Bell(t,523.25f)+.35f*Bell(t-.08f,783.99f),
            "recruit" => .75f*Horn(t,261.63f,.28f),
            _ => .54f*Drum(t,low,93,28)+noise*Envelope(t,.002f,58)*.31f
        };

        static AudioClip MakeCue(string key)
        {
            float duration=key=="milestone"?1.4f:key=="dispatch"||key=="patrol"||key=="explosion"?.76f:
                key=="order"||key=="harvest"||key=="heal"?.49f:.36f;
            int length=Mathf.RoundToInt(duration*SampleRate);
            var samples=new float[length];
            uint state=182457;
            float low=0;
            for(int i=0;i<length;i++)
            {
                float t=i/(float)SampleRate, noise=Noise(ref state);
                low+=(noise-low)*.055f;
                float tail=Mathf.Clamp01((duration-t)*32);
                samples[i]=Mathf.Clamp(CueSample(key,t,noise,low)*tail*.72f,-1,1);
            }
            return Clip("Cue "+key,samples,SampleRate);
        }

        static AudioClip MakeSoundscape(int kind)
        {
            const int rate=11025, length=rate*8, overlap=rate/14;
            var samples=new float[length];
            int source=kind==DrizzleBed?1:kind;
            uint state=703459u+(uint)source*91u;
            float wind=0,water=0,filtered=0;
            for(int i=0;i<length+overlap;i++)
            {
                float t=i/(float)rate, noise=Noise(ref state);
                wind+=(noise-wind)*(source==2?.052f:.024f);
                water+=(noise-water)*.17f;
                float gust=.54f+.24f*Wave(.25f,t)+.16f*Wave(.375f,t);
                float sample;
                if(kind==3)
                {
                    float pulse=t%2f, echo=(t+.61f)%2f;
                    sample=.15f*Drum(pulse,wind,50,11)+.054f*Drum(echo,wind,64,15)+.23f*wind*gust;
                }
                else
                {
                    float tide=.41f+.25f*Wave(.375f,t)+.12f*Wave(.625f,t);
                    sample=wind*gust*(source==2?1.12f:.66f)+(water-wind)*tide*(source==2?.24f:.39f);
                    // Prefilter the two seasonal loops so the same crossfade works in both players.
                    if(kind==DrizzleBed)
                    {
                        filtered+=(sample+.17f*(noise-water)-filtered)*.29f;
                        sample=.86f*filtered+.14f*sample;
                    }
                    else if(kind==2)
                    {
                        filtered+=(sample-filtered)*.15f;
                        sample=.78f*filtered+.22f*sample;
                    }
                }
                // Generate across the seam and crossfade the continuation into the head of the loop.
                if(i<length)samples[i]=sample*.62f;
                else
                {
                    int head=i-length;
                    samples[head]=Mathf.Lerp(sample*.62f,samples[head],head/(float)overlap);
                }
            }
            return Clip(kind==DrizzleBed?"Shore and drizzle":kind==1?"Shore and wind":kind==2?"Winter shore":"Distant war drums",samples,rate);
        }

        static AudioClip Clip(string name,float[] samples,int rate)
        {
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }

        internal static int VerifyClips()
        {
            for(int i=0;i<CueKeys.Length+BedCount-1;i++)
            {
                var clip=i<CueKeys.Length?MakeCue(CueKeys[i]):MakeSoundscape(i-CueKeys.Length+1);
                var samples=new float[clip.samples];
                bool read=clip.GetData(samples,0);
                float peak=0;
                foreach(float value in samples)peak=Mathf.Max(peak,Mathf.Abs(value));
                Destroy(clip);
                if(!read||peak<=.015f||peak>1||float.IsNaN(peak))
                    throw new System.InvalidOperationException("Generated clip is empty or clipped: "+i);
                if(i>=CueKeys.Length&&Mathf.Abs(samples[0]-samples[samples.Length-1])>.06f)
                    throw new System.InvalidOperationException("Audible discontinuity in ambient loop: "+i);
            }
            return CueKeys.Length+BedCount-1;
        }
    }
}
