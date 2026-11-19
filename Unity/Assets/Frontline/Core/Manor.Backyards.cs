using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public readonly struct ManorBackyard
    {
        public readonly string id, title;
        public readonly int coin, timber;

        internal ManorBackyard(string id,string title,int coin,int timber)
        {
            this.id=id; this.title=title; this.coin=coin; this.timber=timber;
        }
    }

    public sealed partial class Manor
    {
        public static IReadOnlyList<ManorBackyard> Backyards {get;} = Array.AsReadOnly(new[] {
            new ManorBackyard("garden","菜园",8,4),
            new ManorBackyard("poultry","鸡舍",12,6),
            new ManorBackyard("workshop","家庭作坊",16,8),
            new ManorBackyard("militia_yard","民兵院落",14,8)
        });

        static int BackyardIndex(string id)
        {
            for(int i=0;i<Backyards.Count;i++)if(Backyards[i].id==id)return i;
            return -1;
        }

        public static string BackyardTitle(string id)
        {
            int index=BackyardIndex(id);
            return index<0?"未启用":Backyards[index].title;
        }

        static int GardenYield(int level) => (Yield(120,level)+50)/100;

        public static string BackyardEffect(string id,int level)
        {
            switch(id)
            {
                case "garden": return "每日粮食 +"+FormatHundredths(GardenYield(level));
                case "poultry": return "每日粮食 +0.8 · 金币 +0.6 · 独立副食";
                case "workshop": return "每日金币 +1.5"+(level>=2?" · 军械 +0.3":" · Lv.2 起军械 +0.3");
                case "militia_yard": return "常备军上限 +2 · 行军上限 +1";
                default: return "可启用一项副业";
            }
        }

        public string BackyardBlock(int plot,string id)
        {
            if((uint)plot>=PlotCount||slots[plot].kind!="cottage")return "请先选择已建成的农舍";
            int index=BackyardIndex(id);
            if(index<0)return "未知后院副业";
            if(slots[plot].backyard==id)return "已启用";
            var item=Backyards[index];
            return coin<item.coin||timber<item.timber?"金币或建材不足":null;
        }

        public void SetBackyard(int plot,string id)
        {
            string blocked=BackyardBlock(plot,id);
            if(blocked!=null)throw new InvalidOperationException(blocked);
            var item=Backyards[BackyardIndex(id)];
            Pay(item.coin,item.timber);
            slots[plot].backyard=id;
        }

        static void AddBackyardYield(ManorPlot plot,ref ManorDay balance,ref bool poultry)
        {
            switch(plot.backyard)
            {
                case "garden": balance.food+=GardenYield(plot.level); break;
                case "poultry": balance.food+=80; balance.coin+=60; poultry=true; break;
                case "workshop": balance.coin+=150; if(plot.level>=2)balance.arms+=30; break;
            }
        }

    }
}
