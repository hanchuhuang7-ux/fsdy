using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        readonly struct BipedGait
        {
            public readonly float phase,weight,run,sway,bob,lean,roll,twist,breath,direction;

            public BipedGait(float metres,float speed,float topSpeed,float forward,float time,int id)
            {
                float ratio=Mathf.Clamp01(speed/Mathf.Max(.1f,topSpeed));
                weight=Ease((speed-.08f)/.67f);
                run=Ease((ratio-.53f)/.47f);
                direction=1-2*Ease((-forward-.12f)/.46f);
                phase=metres*.83f+id*.381966f;
                float wave=Mathf.Sin(phase*(2*Mathf.PI));
                sway=wave*weight;
                breath=Mathf.Sin(time*2.47f+id*1.75488f);
                float lift=Mathf.Abs(wave);
                bob=weight*((.028f+.037f*run)*(lift-.39f)+.009f*lift*(1-lift))
                    +breath*.010f*(1-weight);
                lean=weight*(2.3f+10.8f*run)*Mathf.Clamp(forward,-.6f,1)+breath*.65f*(1-weight);
                roll=sway*(1.75f+1.35f*run)+breath*.64f*(1-weight);
                twist=-sway*(2.6f+2.3f*run);
            }
        }

        readonly struct Footfall
        {
            public readonly float travel,lift,curl;

            public Footfall(float phase,float support)
            {
                phase-=Mathf.Floor(phase);
                if(phase<support)
                {
                    float t=phase/support;
                    travel=.5f-t;
                    lift=0;
                    curl=Ease((t-.77f)/.23f);
                }
                else
                {
                    float t=(phase-support)/(1-support);
                    travel=Ease(t)-.5f;
                    lift=4*t*(1-t)*(1.13f-.30f*t);
                    curl=(1-t)*(1-t)-.32f*lift;
                }
            }
        }

        static float Ease(float t)
        {
            t=Mathf.Clamp01(t);
            return t*t*(3-2*t);
        }

        static Matrix4x4 WalkingLeg(in Matrix4x4 hips,in BipedGait gait,Vector3 joint,bool right,float support=1)
        {
            float drive=gait.weight*support;
            var step=new Footfall(gait.phase+(right?.5f:0),.64f-.14f*gait.run);
            float swing=step.travel*gait.direction*drive;
            var rotation=Quaternion.Euler(-swing*(52+24*gait.run)+step.curl*drive*7,0,right?2:-2);
            var sole=new Vector3(0,-.274f,.054f);
            var rolledSole=rotation*sole;
            // Keep the contact foot on its baseline while the torso takes the weight.
            joint.y+=step.lift*drive*(.077f+.055f*gait.run)+sole.y-rolledSole.y;
            joint.z+=swing*(.48f+.15f*gait.run)+sole.z-rolledSole.z;
            return hips*Pose(joint,rotation,Vector3.one);
        }

        readonly struct HorseStride
        {
            public readonly float phase,weight,trot,gallop,pitch,bob,roll;
            public readonly bool rightLead;

            public HorseStride(float distance,float speed,float charge,float terrain,float breath,int id)
            {
                phase=distance*.58f+id*.27202f;
                weight=Ease((speed-.10f)/.92f);
                trot=Ease((speed-1.35f)/1.75f);
                gallop=Mathf.Max(Ease((speed-3.40f)/1.83f),Ease((charge-.30f)/.73f)*Ease((speed-2.8f)/1.8f));
                rightLead=(id&1)==0;
                float cycle=phase-Mathf.Floor(phase);
                float wave=Mathf.Sin(cycle*(2*Mathf.PI));
                float lift=Ease((cycle-.72f)/.13f)*(1-Ease((cycle-.86f)/.14f));
                bob=weight*Mathf.Lerp(.041f*(Mathf.Abs(wave)-.47f),.157f*lift-.035f,gallop)+breath*.012f*(1-weight);
                pitch=-terrain+weight*Mathf.Lerp(2.05f*wave,6.3f*wave,gallop);
                roll=weight*wave*Mathf.Lerp(1.4f,2.7f,gallop);
            }

            public float Offset(bool front,bool right)
            {
                // Hind/fore on the same side follow in walk. In trot the diagonals
                // pair up; the gallop breaks those pairs and leaves an aerial phase.
                float walk=front?(right?.25f:.75f):(right?.5f:0);
                float diagonal=front?(right?.025f:.525f):(right?.50f:0);
                bool lead=right==rightLead;
                float rush=front?(lead?.55f:.71f):(lead?.89f:0);
                float paired=Mathf.Lerp(walk,diagonal,trot);
                float difference=rush-paired;
                difference-=Mathf.Floor(difference+.5f);
                return paired+difference*gallop;
            }
        }

        static Matrix4x4 Hoof(in Matrix4x4 horseRoot,in HorseStride stride,bool front,bool right)
        {
            var step=new Footfall(stride.phase+stride.Offset(front,right),
                Mathf.Lerp(Mathf.Lerp(.70f,.55f,stride.trot),.31f,stride.gallop));
            float travel=step.travel*stride.weight;
            var turn=Quaternion.Euler(-travel*Mathf.Lerp(45,71,stride.gallop)+step.curl*stride.weight*(front?17:12),0,0);
            var sole=new Vector3(0,-.699f,.082f);
            var tilted=turn*sole;
            float lift=step.lift*stride.weight*Mathf.Lerp(front?.14f:.16f,front?.29f:.23f,stride.gallop);
            var joint=new Vector3(right?.24f:-.24f,-.65f+sole.y-tilted.y+lift-stride.bob,
                (front?.40f:-.40f)+travel*Mathf.Lerp(.57f,.97f,stride.gallop)+sole.z-tilted.z);
            return horseRoot*Pose(joint,turn,Vector3.one);
        }
    }
}
