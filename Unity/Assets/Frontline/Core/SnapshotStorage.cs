using System.Collections.Generic;

namespace Frontline.Core
{
    // Owned by one Frame. Never shared with another live or archived frame.
    internal sealed class SnapshotStorage
    {
        internal readonly List<ProjectileFrame> projectiles=new List<ProjectileFrame>();
        internal readonly List<CorpseFrame> corpses=new List<CorpseFrame>();
        internal readonly List<Cue> cues=new List<Cue>();
        internal readonly List<EffectFrame> effects=new List<EffectFrame>();
        internal readonly List<PropFrame> props=new List<PropFrame>();
        internal readonly List<OrderEntry> log=new List<OrderEntry>(),active=new List<OrderEntry>();
        internal static void Size<T>(List<T> output,List<T> slots,int count) where T:class,new()
        {
            while(slots.Count<count)slots.Add(new T());
            output.Clear();if(output.Capacity<count)output.Capacity=count;
            for(int i=0;i<count;i++)output.Add(slots[i]);
        }
        internal void Prepare(Frame f,int units,int shots,int propCount,int effectCount=41)
        {
            while(f.units.Count<units)f.units.Add(new UnitFrame{t=new float[7]});
            Size(f.projectiles,projectiles,shots);foreach(var p in projectiles)if(p.t==null)p.t=new float[7];
            Size(f.ragdolls,corpses,24);foreach(var c in corpses){c.wreckStorage=new float[4];c.wreck=c.wreckStorage;while(c.parts.Count<6)c.parts.Add(new CorpsePart{t=new float[7]});}
            Size(f.cues,cues,24);Size(f.effects,effects,effectCount);Size(f.props,props,propCount);
            Size(f.commander.log,log,6);Size(f.commander.active,active,6);
        }
    }
}
