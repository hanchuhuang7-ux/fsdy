using System.Collections.Generic;

namespace Frontline.Core
{
    public struct BattleLeaders
    {
        public UnitReport Damage { get; private set; }
        public UnitReport Kills { get; private set; }
        public UnitReport Healing { get; private set; }

        internal void Consider(UnitReport unit)
        {
            if(unit.damageDealt>0&&(Damage==null||unit.damageDealt>Damage.damageDealt||
                unit.damageDealt==Damage.damageDealt&&unit.id<Damage.id))
                Damage=unit;
            if(unit.kills>0&&(Kills==null||unit.kills>Kills.kills||unit.kills==Kills.kills&&unit.id<Kills.id))
                Kills=unit;
            if(unit.healing>0&&(Healing==null||unit.healing>Healing.healing||
                unit.healing==Healing.healing&&unit.id<Healing.id))
                Healing=unit;
        }
    }

    public static class BattleHighlights
    {
        public static (BattleLeaders red,BattleLeaders blue) Leaders(BattleReport report)
        {
            BattleLeaders red=default,blue=default;
            if(report?.units==null)return (red,blue);
            foreach(var unit in report.units)
            {
                if(unit==null)continue;
                if(unit.team=="red")red.Consider(unit);
                else if(unit.team=="blue")blue.Consider(unit);
            }
            return (red,blue);
        }

        public static string NamedMoment(Highlight moment,IReadOnlyList<OrderEntry> orders)
        {
            if(moment.type=="order"&&orders!=null)
                for(int i=0;i<orders.Count;i++)
                {
                    var order=orders[i];
                    if(order.tick!=moment.tick||order.team!=moment.team)continue;
                    string name=order.type=="inspire"?"鼓舞":order.type=="focus"?"集火":"盾阵";
                    return (moment.tick/60f).ToString("0.0")+"s · "+(moment.team=="red"?"红方":"蓝方")+" "+name;
                }
            return Replay.HighlightText(moment);
        }
    }
}
