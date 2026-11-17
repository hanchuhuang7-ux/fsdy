using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public interface IBody
    {
        int Id {get;} V3 Position {get;} V3 Velocity {get;}
        void Impulse(V3 impulse); float[] Transform(float[] reuse=null);
    }
    public struct Contact {public int a,b;public Contact(int a,int b){this.a=a;this.b=b;}}
    public interface IPhysicsWorld:IDisposable
    {
        IBody Unit(int id,UnitPlacement placement,string team,UnitDef def);
        IBody Prop(int id,PropPlacement placement,PropDef def);
        IBody Projectile(int id,string type,V3 p,V3 velocity,float gravityScale,int ownerId);
        void Remove(IBody body);
        void Corpse(int id,string type,string team,UnitDef def,V3 position,V3 velocity,V3 impulse);
        void PushCorpses(V3 origin,float radius);
        void Step(float dt);
        void AgeCorpses(float dt);
        List<CorpseFrame> Corpses(List<CorpseFrame> reuse=null);
        List<Contact> Contacts();
    }
    // Retains v0.6 floating-point evaluation order when rebuilding its native seeds.
    public interface ILegacyCompatibilityWorld {}
    public interface ICombatBody {void Facing(float angle,bool fast);}
    public interface IRecordedBattle:IDisposable
    {
        int Tick {get;} Result Result {get;}
        void Step(); Frame Snapshot(Frame reuse=null); void UseOrderLog(List<OrderEntry> orders);
    }
    public interface IWorldFactory {IPhysicsWorld Create(Catalog catalog,Deployment config);}
}
