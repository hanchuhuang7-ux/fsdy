using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly Color ReportRed=new Color(1,.66f,.58f),ReportBlue=new Color(.64f,.83f,1);
        static readonly string[] ReportTeams={"red","blue"};

        public void ReportDialog() => OpenDialog("战果 · 贡献与关键战况",BuildBattleReport,748,maxWidth:824,wholeRows:true);

        void BuildBattleReport(RectTransform p,float w)
        {
            var result=app.ReplayFile?.result??app.Battle?.Result;
            var report=result?.report??app.Battle?.Report();
            if(report==null)
            {
                Label(p,"开战后查看贡献。",0,0,w,44,14,muted);
                return;
            }
            string outcome=result==null?"实时战报":result.winner=="draw"?"平局":(result.winner=="red"?"红方":"蓝方")+"获胜";
            Label(p,outcome+" · "+(result?.time??app.Battle.Time).ToString("0.0")+"s",0,0,w,33,portrait?17:20,gold);
            float y=36;
            y+=BuildLeaderCard(p,w,y,report);
            y+=BuildReportMoments(p,w,y);
            if(report.mvp!=null)
            {
                Label(p,"MVP · "+ItemName(report.mvp.type)+"  ·  贡献 "+report.mvp.score.ToString("0"),0,y,w,30,14,white);
                y+=34;
            }
            foreach(string team in ReportTeams)
            {
                if(report.teams==null||!report.teams.TryGetValue(team,out var resultTeam)||resultTeam==null)continue;
                Label(p,(team=="red"?"红方":"蓝方")+" · 存活 "+resultTeam.survived+" / "+resultTeam.initial+
                    " · 残余战力 "+resultTeam.remainingValue.ToString("0"),0,y,w,32,portrait?13:16,team=="red"?ReportRed:ReportBlue);
                y+=35;
                if(resultTeam.byType!=null)foreach(var row in resultTeam.byType)
                {
                    var stats=row.Value;
                    if(stats==null)continue;
                    string separator=portrait?"\n":"   ";
                    var band=Panel(p,"Unit contribution",0,y,w,portrait?43:34,manorInset,Slice.Inset);
                    Label(band,ItemName(row.Key)+"  "+stats.survived+" / "+stats.initial+separator+
                        "伤害 "+stats.damageDealt.ToString("0")+" · 治疗 "+stats.healing.ToString("0")+
                        " · 误伤 "+stats.friendlyFire.ToString("0")+" · 击杀 "+stats.kills,9,1,w-18,band.sizeDelta.y-2,portrait?11:13,pale);
                    y+=band.sizeDelta.y+4;
                }
                y+=10;
            }
            p.sizeDelta=new Vector2(w,y);
        }

        float BuildLeaderCard(RectTransform p,float w,float y,BattleReport report)
        {
            var (red,blue)=BattleHighlights.Leaders(report);
            float line=portrait?40:33,left=portrait?82:145,side=(w-left-13)/2;
            var card=Panel(p,"Report leaders",0,y,w,52+line*3,manorInset,Slice.Inset);
            Label(card,"全场亮点",11,3,w-22,26,portrait?16:18,gold);
            Label(card,"红方",left,27,side,21,13,ReportRed);
            Label(card,"蓝方",left+side,27,side,21,13,ReportBlue);
            Rule(card,11,49,w-22);
            LeaderRow(card,"Damage","最高输出",52,red.Damage,blue.Damage,red.Damage?.damageDealt,blue.Damage?.damageDealt);
            LeaderRow(card,"Kills","最多击杀",52+line,red.Kills,blue.Kills,red.Kills?.kills,blue.Kills?.kills);
            LeaderRow(card,"Healing","最高治疗",52+line*2,red.Healing,blue.Healing,red.Healing?.healing,blue.Healing?.healing);
            return card.sizeDelta.y+10;

            void LeaderRow(Transform parent,string kind,string title,float top,UnitReport r,UnitReport b,float? first,float? second)
            {
                Label(parent,title,11,top,left-16,line,portrait?12:14,pale);
                var redText=Label(parent,LeaderText(r,first),left,top,side-6,line,portrait?11:14,r==null?muted:white);
                var blueText=Label(parent,LeaderText(b,second),left+side,top,side-6,line,portrait?11:14,b==null?muted:white);
                redText.name="Report.Leaders.Red."+kind;
                blueText.name="Report.Leaders.Blue."+kind;
            }
        }

        string LeaderText(UnitReport unit,float? value) => unit==null?"—":ItemName(unit.type)+" #"+unit.id+
            (portrait?"\n":" · ")+value.GetValueOrDefault().ToString("0.#");

        float BuildReportMoments(RectTransform p,float w,float y)
        {
            var highlights=app.Highlights;
            IReadOnlyList<OrderEntry> orders=app.ReplayFile?.orderLog??app.Battle?.Commander.log;
            bool playable=app.ReplayFile?.kind=="trajectory";
            Label(p,playable?"高光时刻 · 点击跳转":"高光时刻",0,y,w,30,portrait?14:16,gold);
            float top=y+33;
            foreach(var moment in highlights)
            {
                string name=BattleHighlights.NamedMoment(moment,orders);
                if(playable)
                {
                    var jump=Button(p,name+"  ›",0,top,w,33,()=>{CloseDialog();app.SeekHighlight(moment);});
                    jump.name="Report.Moment."+moment.id;
                    jump.GetComponentInChildren<Text>().alignment=TextAnchor.MiddleLeft;
                }
                else Label(p,name,0,top,w,33,13,pale);
                top+=37;
            }
            if(highlights.Count==0)
            {
                Label(p,"暂无记录",0,top,w,26,12,muted);
                top+=30;
            }
            return top-y+8;
        }
    }
}
