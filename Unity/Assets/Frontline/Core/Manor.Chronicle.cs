using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public readonly struct ManorChronicleEntry
    {
        public readonly int day, subject, choice;
        public readonly string kind;

        internal ManorChronicleEntry(int day,string kind,int subject,int choice)
        {
            this.day=day; this.kind=kind; this.subject=subject; this.choice=choice;
        }
    }

    public sealed partial class Manor
    {
        public const int ChronicleLimit=12;
        public List<ManorChronicleEntry> chronicle=new List<ManorChronicleEntry>(ChronicleLimit);

        void TrackProsperity()
        {
            int tier=ProsperityTier;
            while(prosperityPeak<tier)
            {
                prosperityPeak++;
                Remember("title",prosperityPeak);
            }
        }

        void Remember(string kind,int subject,int choice=0)
        {
            if(chronicle.Count==ChronicleLimit)chronicle.RemoveAt(0);
            chronicle.Add(new ManorChronicleEntry(day,kind,subject,choice));
        }

        public static string ChronicleText(ManorChronicleEntry entry)
        {
            int subject=entry.subject;
            switch(entry.kind)
            {
                case "title": return "晋升 · "+ProsperityTitles[subject];
                case "reclaim": return "开垦外环建地 "+subject;
                case "granary": case "chapel": case Bakery: case Weaver: return "建地 "+subject+" 建成"+Name(entry.kind);
                case "specialist": return SpecialistNames[subject]+" · 晋升 Lv."+entry.choice;
                case "development": return "解锁专精 · "+Developments[subject].title;
                case "dispatch":
                    var report=SeasonalEvents[subject];
                    return report.title+" · "+(entry.choice==0?report.first.title:report.second.title)+(entry.choice==2?"（默认）":"");
                case "expedition": return "第 "+(subject+1)+" 场远征凯旋"+MeritText(entry.choice);
                case "frontier": return "击退警戒 "+subject+" 阶全部守军"+MeritText(entry.choice);
                case "raid": return "击退掠夺者"+MeritText(entry.choice);
                case "festival": return "召开领地庆典 · 鼓舞民心";
                case "claim": return "宣称并筑哨 · "+Sectors[subject].name;
                case "nobility": return "受封"+NobleRanks[subject].title+" · 赐地 "+NobleGrantPlots(subject,entry.choice==1);
                case "armament": return "锻成 "+Armament(HeroIds[subject],entry.choice).title+" · 专属武装 "+entry.choice+" 阶";
                default: throw new InvalidOperationException("未知编年史记录");
            }
        }

        static string MeritText(int amount) => amount > 0 ? " · 威望 +"+amount : "";

        bool ValidChronicle(int schema)
        {
            if(chronicle==null||chronicle.Count>ChronicleLimit)return false;
            int previous=1,lastRank=0;
            Span<int> lastArmament=stackalloc int[HeroIds.Length];
            lastArmament.Clear();
            foreach(var entry in chronicle)
            {
                if(entry.day<previous||entry.day>day||!ValidChronicleEntry(entry,schema))return false;
                if(entry.kind=="nobility")
                {
                    if(entry.subject<=lastRank)return false;
                    lastRank=entry.subject;
                }
                if(entry.kind=="armament")
                {
                    if(entry.choice<=lastArmament[entry.subject])return false;
                    lastArmament[entry.subject]=entry.choice;
                }
                previous=entry.day;
            }
            return true;
        }

        bool ValidChronicleEntry(ManorChronicleEntry entry,int schema)
        {
            int subject=entry.subject;
            bool merit=schema>=RetinueSaveVersion;
            if(entry.kind=="dispatch")return (uint)subject<SeasonalEvents.Length&&(uint)entry.choice<=2;
            if(entry.kind=="expedition")return (uint)subject<Route.Length&&(uint)entry.choice<=(merit?ExpeditionInfluence:0);
            if(entry.kind=="frontier")return frontierSwept&&subject>=(merit?1:2)&&subject<=FrontierMap.MaxTier&&
                (uint)entry.choice<=(merit?FrontierInfluence:0);
            if(entry.kind=="raid")return merit&&(uint)subject<Route.Length&&(uint)entry.choice<=RaidInfluence;
            if(entry.kind=="specialist")return schema>=CraftSaveVersion&&(uint)subject<SpecialistIds.Length&&
                entry.choice>=2&&entry.choice<=SpecialistRank(SpecialistIds[subject]);
            if(entry.kind=="nobility")return schema>=NobilitySaveVersion&&subject>=1&&subject<=nobility&&
                (entry.choice==0||entry.choice==1&&schema>=CraftSaveVersion);
            if(entry.kind=="armament")return schema>=ArmamentSaveVersion&&(uint)subject<HeroIds.Length&&
                entry.choice>=1&&entry.choice<=HeroArmament(HeroIds[subject]);
            if(entry.choice!=0)return false;
            switch(entry.kind)
            {
                case "title": return subject>=1&&subject<=prosperityPeak;
                case "reclaim": return subject>=FirstOuterPlot&&subject<FirstNoblePlot&&IsPlotCleared(subject);
                case "granary": case "chapel": return subject>0&&IsPlotGranted(subject)&&
                    (subject<LegacyNoblePlotCount||schema>=CraftSaveVersion);
                case Bakery: case Weaver: return schema>=CraftSaveVersion&&subject>0&&IsPlotGranted(subject);
                case "development": return HasDevelopment((ManorPerk)subject);
                case "festival": return merit&&subject==0;
                case "claim": return schema>=ClaimsSaveVersion&&ClaimedSector(subject);
                default: return false;
            }
        }

        void ReadChronicle(object value,int schema)
        {
            if(!(value is List<object> entries)||entries.Count>ChronicleLimit)throw new FormatException("领地编年史无效");
            foreach(var item in entries)
            {
                var row=ObjectWith(item,"day","kind","subject","choice");
                chronicle.Add(new ManorChronicleEntry(Number(row["day"],1,day),row["kind"] as string,
                    Number(row["subject"],0,PlotCount-1),Number(row["choice"],0,schema>=RetinueSaveVersion?FrontierInfluence:2)));
            }
            if(!ValidChronicle(schema))throw new FormatException("领地编年史条目无效或日期乱序");
        }
    }
}
