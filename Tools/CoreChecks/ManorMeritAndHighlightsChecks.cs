using System;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunMeritAndHighlights(Action<string,Action> check)
    {
        check("merit counts the six durable objectives and does not change v13",()=>{
            var m=new Manor();
            var first=m.MeritOverview();
            Expect(ManorMerit.Total==29&&first.Completed==1&&first.Percent==3&&first.Title=="边疆新贵","founding merit");
            m=PromotionEstate();
            m.PromoteNobility(1);
            m.PromoteNobility(2);
            var good=m.MeritOverview();
            Expect(good.ranks==3&&good.sectors==3&&good.milestones==0&&good.expeditions==5&&good.Title=="磐石领主","rank or territory score");
            m.ReclaimPlot(18);m.ReclaimPlot(19);m.ClaimMilestone(0);
            m.cleared=Manor.Route.Length;
            m.milestoneClaims=31;
            m.PromoteNobility(3);m.PromoteNobility(4);
            foreach(string id in Manor.SpecialistIds)m.specialists[id]=Manor.SpecialistMaxLevel;
            foreach(string id in Manor.HeroIds)m.heroes[id]=new ManorHero{armament=Manor.MaxHeroArmament};
            var near=m.MeritOverview();
            Expect(near.Completed==28&&near.Title=="铁血统领"&&near.specialists==6&&near.armaments==3&&near.milestones==5,
                "mastery progress or final rating issued early");
            m.ClaimMilestone(5);
            string json=m.Export();
            var full=Manor.Import(json).MeritOverview();
            Expect(full.Completed==ManorMerit.Total&&full.Percent==100&&full.Title=="黄金公爵"&&m.Export()==json&&Manor.SaveVersion==13,
                "terminal rating or schema");
        });

        check("report highlights use actual units and resolve independent red and blue ties by id",()=>{
            var report=new BattleReport();
            var late=new UnitReport{id=22,team="red",damageDealt=80,kills=2,healing=20,alive=false};
            var early=new UnitReport{id=1,team="red",damageDealt=80,kills=2,healing=30};
            var killer=new UnitReport{id=25,team="red",damageDealt=20,kills=4};
            var healer=new UnitReport{id=13,team="blue",damageDealt=10,kills=1,healing=27};
            var siege=new UnitReport{id=14,team="blue",damageDealt=276,kills=1};
            report.units.AddRange(new[]{late,healer,siege,killer,early,new UnitReport{id=34,team="red"},null});
            report.props.Add(new UnitReport{id=150,team="red",damageDealt=3000,kills=9,healing=500});
            var (red,blue)=BattleHighlights.Leaders(report);
            Expect(red.Damage==early&&red.Kills==killer&&red.Healing==early,"red categories");
            Expect(blue.Damage==siege&&blue.Kills==healer&&blue.Healing==healer,"blue categories");
            var noHealing=BattleHighlights.Leaders(new BattleReport());
            Expect(noHealing.red.Healing==null&&noHealing.blue.Damage==null,"empty report produced a leader");
        });

        check("replay moment names resolve orders by team and tick without changing historic records",()=>{
            var h=new Highlight{id=1,type="order",team="blue",tick=60};
            var orders=new[]{
                new OrderEntry{tick=60,team="red",type="focus"},
                new OrderEntry{tick=61,team="blue",type="inspire"},
                new OrderEntry{tick=60,team="blue",type="fortify"}
            };
            string before=Json.Write(h);
            Expect(BattleHighlights.NamedMoment(h,orders).EndsWith("蓝方 盾阵")&&
                BattleHighlights.NamedMoment(h,null)==Replay.HighlightText(h)&&Json.Write(h)==before,"timeline label or compatibility");
        });
    }
}
