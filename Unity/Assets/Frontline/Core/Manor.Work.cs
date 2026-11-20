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
        static readonly string[] ColdWork = { "granary","chapel",Weaver,"timber_camp" };
        static readonly string[] WarmWork = { Bakery,"timber_camp","smithy","manor_hall" };
        public bool seasonalWork;

        public void AssignPreset(string preset)
        {
            if(preset=="seasonal")
            {
                seasonalWork=true;
                AssignSeasonalWork();
                return;
            }
            if (preset!="food" && preset!="smithing" && preset!="balanced") throw new InvalidOperationException("未知派工预设");
            seasonalWork=false;
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

        void AssignSeasonalWork()
        {
            Span<int> plan=stackalloc int[PlotCount];
            plan.Clear();
            int remaining=workers, season=Season;
            bool winter=season==3;
            // Reserve a food job when provisions are low. Warm-season jobs always start at the best active field.
            int foodJobs=0;
            if((!winter||food<workers*3)&&AssignFood(plan,season)){remaining--;foodJobs++;}
            foreach(string kind in winter?ColdWork:WarmWork)
            {
                if(remaining==0)break;
                int index=BestWorkPlot(plan,kind);
                if(index<0)continue;
                plan[index]++;
                remaining--;
            }
            // Cold-weather timber and cloth have starter jobs. Use the other jobs to feed the estate,
            // then fill workshops in rounds so that one oversized building cannot monopolize the roster.
            while(remaining>0&&AssignFood(plan,season))
            {
                remaining--;
                if(++foodJobs>=workers/2&&remaining>0&&AssignNext(plan,winter?ColdWork:WarmWork))remaining--;
            }
            while(remaining>0)
            {
                int before=remaining;
                if(AssignNext(plan,winter?ColdWork:WarmWork))remaining--;
                if(remaining>0&&AssignNext(plan,CivicWork))remaining--;
                if(remaining>0&&AssignNext(plan,winter?WarmWork:ColdWork))remaining--;
                if(before==remaining)break;
            }
            for(int i=0;i<slots.Length;i++)slots[i].workers=plan[i];
        }

        bool AssignFood(Span<int> plan,int season)
        {
            int best=-1, yield=-1;
            for(int i=1;i<slots.Length;i++)
            {
                var plot=slots[i];
                bool pasture=plot.kind=="pasture";
                if((!pasture&&(!IsCrop(plot.kind)||IsResting(plot,season)))||plan[i]>=StaffLimit(plot.kind))continue;
                int value=Yield(Def(plot.kind).harvest,plot.level)*(pasture?PastureSeasonPercent*100:CropSeasonPercent*plot.fertility);
                if(value<=yield)continue;
                yield=value;
                best=i;
            }
            if(best<0)return false;
            plan[best]++;
            return true;
        }

        int BestWorkPlot(ReadOnlySpan<int> plan,string kind)
        {
            int best=-1;
            for(int i=0;i<slots.Length;i++)
                if(slots[i].kind==kind&&plan[i]<StaffLimit(kind)&&(best<0||slots[i].level>slots[best].level))
                    best=i;
            return best;
        }

        bool AssignNext(Span<int> plan,string[] line)
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
