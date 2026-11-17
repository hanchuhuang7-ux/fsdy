using System;
using System.Runtime.CompilerServices;

namespace Frontline.Core
{
    // Current rules only. Historical 0.6–0.8 battles and physics use frozen helpers.
    public static class CombatMotion
    {
        public const int EffectCapacity=96;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float AngleDelta(float from,float to)
        {
            float d=(to-from)%(2*(float)Math.PI);
            if(d>(float)Math.PI)d-=2*(float)Math.PI;
            if(d<-(float)Math.PI)d+=2*(float)Math.PI;
            return d;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float TurnRate(string type,float speed)
        {
            switch(type){case "catapult":return 1.05f;case "cavalry":return 2.8f/(1+speed*.16f);case "giant":return 2.2f;case "shield":return 4.5f;case "crossbow":return 4.8f;default:return 6.8f;}
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Turn(float angle,float desired,string type,float speed,float dt)
        {float step=TurnRate(type,speed)*dt;float delta=AngleDelta(angle,desired);return Math.Abs(delta)<=step?desired:angle+Math.Max(-step,Math.Min(step,delta));}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 Forward(float angle){return new V3((float)Math.Sin(angle),0,(float)Math.Cos(angle));}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float BlockImpulse(float pierce){return .22f+.18f*pierce;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int HitTier(float knock,string source)
        {return knock>=15||source=="giant"||source=="catapult"||source=="barrel"?2:knock>=6?1:0;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Stun(int tier,bool blocked){return (tier==2?.42f:tier==1?.13f:.055f)*(blocked?.3f:1);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Windup(string type){return type=="giant"?.24f:type=="catapult"?.28f:type=="cavalry"?.12f:.10f;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 Launch(string type,V3 p,V3 direction,float scale)
        {
            float ahead=type=="catapult"?1.12f:type=="bomber"?.62f:.52f;
            float height=type=="catapult"?1.2f:type=="bomber"?.82f:.94f;
            return p+new V3(direction.x*ahead*scale,height*scale,direction.z*ahead*scale);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool Narrow(string map,float x,float z)
        {return map=="bridge"&&Math.Abs(x)<6.4f&&Math.Abs(z)<2.5f||map=="ruins"&&Math.Abs(x)<5.2f&&Math.Abs(z)<3;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 Separation(V3 delta,float radius,bool narrow,float forwardSign,float distance=-1)
        {
            float dist=distance>=0?distance:delta.Planar;if(dist<=.01f||dist>=radius)return new V3();
            float weight=(radius-dist)*.7f/dist;
            // In a choke, yield longitudinally to the ally ahead; do not counter-steer
            // equally hard into both doorposts. Physical colliders still prevent overlap.
            return narrow?new V3(Math.Min(0,delta.x*forwardSign)*forwardSign*weight*.22f,0,delta.z*weight*.12f):delta*weight;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 SlopeVelocity(V3 planar,float slope){return new V3(planar.x,planar.x*slope,planar.z);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float RockYaw(float x,float z){return (float)Math.Sin(x*12.31f+z*7.17f)*180;}
        // Bounds are in the same local frame as the baked meshes, before UnitDef.scale.
        public static V3 CavalryCenter {[MethodImpl(MethodImplOptions.AggressiveInlining)]get{return new V3(0,-.10f,.12f);}}
        public static V3 CartSize {[MethodImpl(MethodImplOptions.AggressiveInlining)]get{return new V3(1.30f,.76f,1.72f);}}
    }
}
