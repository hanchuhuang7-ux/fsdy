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
                case "granary": case "chapel": return "建地 "+subject+" 建成"+Name(entry.kind);
                case "development": return "解锁专精 · "+Developments[subject].title;
                case "dispatch":
                    var report=SeasonalEvents[subject];
                    return report.title+" · "+(entry.choice==0?report.first.title:report.second.title)+(entry.choice==2?"（默认）":"");
                case "expedition": return "第 "+(subject+1)+" 场远征凯旋"+MeritText(entry.choice);
                case "frontier": return "击退警戒 "+subject+" 阶全部守军"+MeritText(entry.choice);
                case "raid": return "击退掠夺者"+MeritText(entry.choice);
                case "festival": return "召开领地庆典 · 鼓舞民心";
                default: throw new InvalidOperationException("未知编年史记录");
            }
        }

        static string MeritText(int amount) => amount > 0 ? " · 威望 +"+amount : "";

        bool ValidChronicle(bool current)
        {
            if(chronicle==null||chronicle.Count>ChronicleLimit)return false;
            int previous=1;
            foreach(var entry in chronicle)
            {
                if(entry.day<previous||entry.day>day||!ValidChronicleEntry(entry,current))return false;
                previous=entry.day;
            }
            return true;
        }

        bool ValidChronicleEntry(ManorChronicleEntry entry,bool current)
        {
            int subject=entry.subject;
            if(entry.kind=="dispatch")return (uint)subject<SeasonalEvents.Length&&(uint)entry.choice<=2;
            if(entry.kind=="expedition")return (uint)subject<Route.Length&&(uint)entry.choice<=(current?ExpeditionInfluence:0);
            if(entry.kind=="frontier")return frontierSwept&&subject>=(current?1:2)&&subject<=FrontierMap.MaxTier&&
                (uint)entry.choice<=(current?FrontierInfluence:0);
            if(entry.kind=="raid")return current&&(uint)subject<Route.Length&&(uint)entry.choice<=RaidInfluence;
            if(entry.choice!=0)return false;
            switch(entry.kind)
            {
                case "title": return subject>=1&&subject<=prosperityPeak;
                case "reclaim": return subject>=FirstOuterPlot&&IsPlotCleared(subject);
                case "granary": case "chapel": return subject>0&&subject<PlotCount;
                case "development": return HasDevelopment((ManorPerk)subject);
                case "festival": return current&&subject==0;
                default: return false;
            }
        }

        void ReadChronicle(object value,bool current)
        {
            if(!(value is List<object> entries)||entries.Count>ChronicleLimit)throw new FormatException("领地编年史无效");
            foreach(var item in entries)
            {
                var row=ObjectWith(item,"day","kind","subject","choice");
                chronicle.Add(new ManorChronicleEntry(Number(row["day"],1,day),row["kind"] as string,
                    Number(row["subject"],0,PlotCount-1),Number(row["choice"],0,current?FrontierInfluence:2)));
            }
            if(!ValidChronicle(current))throw new FormatException("领地编年史条目无效或日期乱序");
        }
    }
}
