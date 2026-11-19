using System;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        static readonly string[] FoodWork = { "irrigated_field","farm","pasture","granary","tavern","timber_camp","smithy","manor_hall","chapel" };
        static readonly string[] SmithingWork = { "timber_camp","smithy","tavern","irrigated_field","farm","pasture","granary","manor_hall","chapel" };
        static readonly string[] CivicWork = { "tavern","chapel" };
        static readonly string[][] ProductionLines = {
            new[] { "irrigated_field","farm","pasture","granary" }, new[] { "timber_camp" }, new[] { "smithy" }, new[] { "manor_hall","chapel" }
        };

        public void AssignPreset(string preset)
        {
            if (preset!="food" && preset!="smithing" && preset!="balanced") throw new InvalidOperationException("未知派工预设");
            int[] plan=new int[slots.Length];
            int remaining=workers;
            if (preset=="balanced")
            {
                if (remaining>0 && AssignNext(plan,CivicWork)) remaining--;
                while (remaining>0)
                {
                    int before=remaining;
                    foreach (var line in ProductionLines)
                        if (remaining>0 && AssignNext(plan,line)) remaining--;
                    if (remaining==before) break;
                }
            }
            else
            {
                var order=preset=="food"?FoodWork:SmithingWork;
                foreach (string kind in order)
                    for (int i=0; i<slots.Length && remaining>0; i++)
                    {
                        if (slots[i].kind!=kind) continue;
                        plan[i]=Math.Min(remaining,StaffLimit(kind));
                        remaining-=plan[i];
                    }
            }
            for (int i=0; i<slots.Length; i++) slots[i].workers=plan[i];
        }

        bool AssignNext(int[] plan,string[] line)
        {
            int best=-1, fewest=int.MaxValue;
            foreach (string kind in line)
                for (int i=0; i<slots.Length; i++)
                {
                    if (slots[i].kind!=kind || plan[i]>=StaffLimit(kind) || plan[i]>=fewest) continue;
                    best=i; fewest=plan[i];
                }
            if (best<0) return false;
            plan[best]++;
            return true;
        }
    }
}
