using System.Collections.Generic;

namespace Frontline.Core
{
    // Owned by one Frame. Never shared with another live or archived frame.
    internal sealed class SnapshotStorage
    {
        internal readonly List<ProjectileFrame> projectiles;
        internal readonly List<CorpseFrame> corpses;
        internal readonly List<Cue> cues;
        internal readonly List<EffectFrame> effects;
        internal readonly List<PropFrame> props;
        internal readonly List<OrderEntry> log,active;
        internal SnapshotStorage(Frame fresh=null){projectiles=fresh?.projectiles??new List<ProjectileFrame>();corpses=fresh?.ragdolls??new List<CorpseFrame>();cues=fresh?.cues??new List<Cue>();effects=fresh?.effects??new List<EffectFrame>();props=fresh?.props??new List<PropFrame>();log=fresh?.commander.log??new List<OrderEntry>();active=fresh?.commander.active??new List<OrderEntry>();}
        // Mono's generic new T() takes the Activator path. These are the closed frame slot types.
        static T Create<T>() where T:class,new()
        {
            if(typeof(T)==typeof(ProjectileFrame))return (T)(object)new ProjectileFrame();
            if(typeof(T)==typeof(CorpseFrame))return (T)(object)new CorpseFrame();
            if(typeof(T)==typeof(Cue))return (T)(object)new Cue();
            if(typeof(T)==typeof(EffectFrame))return (T)(object)new EffectFrame();
            if(typeof(T)==typeof(PropFrame))return (T)(object)new PropFrame();
            if(typeof(T)==typeof(OrderEntry))return (T)(object)new OrderEntry();
            return new T();
        }
        internal static void Size<T>(List<T> output,List<T> slots,int count) where T:class,new()
        {
            while(slots.Count<count)slots.Add(Create<T>());
            if(ReferenceEquals(output,slots)){if(output.Count>count)output.RemoveRange(count,output.Count-count);return;}
            output.Clear();if(output.Capacity<count)output.Capacity=count;
            for(int i=0;i<count;i++)output.Add(slots[i]);
        }
        internal void Prepare(Frame f,int units,int shots,int propCount,int effectCount=41)
        {
            while(f.units.Count<units)f.units.Add(new UnitFrame{t=new float[7]});
            Size(f.projectiles,projectiles,shots);foreach(var p in projectiles)if(p.t==null)p.t=new float[7];
            Size(f.ragdolls,corpses,24);foreach(var c in corpses){c.wreckStorage=new float[4];c.wreck=c.wreckStorage;while(c.parts.Count<6)c.parts.Add(new CorpsePart{name=CorpsePart.Name(c.parts.Count),t=new float[7]});}
            Size(f.cues,cues,24);Size(f.effects,effects,effectCount);Size(f.props,props,propCount);
            Size(f.commander.log,log,6);Size(f.commander.active,active,6);
        }
    }
}
