using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly string[] CodexTabs={"兵种与道具","建筑与后院","爵位与疆域","英雄与专才"};
        int codexTab;

        readonly struct CodexEntry
        {
            public readonly string id,title,stats,description,state;
            public readonly bool highlighted;

            public CodexEntry(string id,string title,string stats,string description,string state=null,bool highlighted=false)
            {
                this.id=id;this.title=title;this.stats=stats;this.description=description;this.state=state;
                this.highlighted=highlighted;
            }
        }

        void CodexEntryButton(RectTransform parent,float x,float y,float w,float h)
        {
            var button=Button(parent,"图鉴",x,y,w,h,CodexDialog);
            button.name="Codex.Open";
            MarkButton(button,Glyph.Codex,portrait?12:16,portrait?4:9,portrait?11:13);
        }

        internal void CodexDialog() => OpenDialog("游戏图鉴",BuildCodex,maxHeight:740,maxWidth:970,wholeRows:true,header:CodexHeader);

        void CodexHeader(RectTransform parent,float width)
        {
            float cell=(width+5)/CodexTabs.Length;
            for(int i=0;i<CodexTabs.Length;i++)
            {
                int tab=i;
                var item=Button(parent,CodexTabs[i],i*cell,0,cell-5,34,()=>{
                    if(codexTab==tab)return;
                    codexTab=tab;
                    if(dialogContent)dialogContent.anchoredPosition=Vector2.zero;
                    CodexDialog();
                },selected:codexTab==i);
                item.name="Codex.Tab."+i;
                if(portrait)item.GetComponentInChildren<Text>().fontSize=11;
            }
        }

        void BuildCodex(RectTransform p,float w)
        {
            var estate=app.InManor?app.Estate:null;
            float y=0;
            switch(codexTab)
            {
                case 0:
                    y=CodexSection(p,w,y,"兵种",app.Data.Data.units.Select(unit=>CombatEntry(unit,estate)));
                    y=CodexSection(p,w,y,"战场道具",app.Data.Data.props.Select(prop=>PropEntry(prop,estate)));
                    break;
                case 1:
                    y=CodexSection(p,w,y,"庄园建筑",new[]{"manor_hall"}.Concat(Manor.Buildings).Select(BuildingEntry));
                    y=CodexSection(p,w,y,"农舍后院",Manor.Backyards.Select(BackyardEntry));
                    y=CodexSection(p,w,y,"领地协同",EconomyEntries());
                    break;
                case 2:
                    y=CodexSection(p,w,y,"贵族头衔",Enumerable.Range(0,Manor.NobleRanks.Count).Select(rank=>NobleEntry(rank,estate)));
                    y=CodexSection(p,w,y,"外域疆域",Enumerable.Range(0,Manor.Sectors.Count).Select(sector=>SectorEntry(sector,estate)));
                    y=CodexSection(p,w,y,"领地专精",Enumerable.Range(0,Manor.Developments.Count).Select(perk=>DevelopmentEntry(perk,estate)));
                    break;
                default:
                    y=CodexSection(p,w,y,"英雄酒馆",app.Data.Data.heroes.Select(hero=>HeroEntry(hero,estate)));
                    if(estate!=null)y=CodexSection(p,w,y,"专属武装",app.Data.Data.heroes.Select(hero=>GearEntry(hero,estate)));
                    y=CodexSection(p,w,y,"领地专才",Manor.SpecialistIds.Select(id=>SpecialistEntry(id,estate)));
                    break;
            }
            p.sizeDelta=new Vector2(w,y+4);
        }

        float CodexSection(RectTransform parent,float width,float top,string title,IEnumerable<CodexEntry> entries)
        {
            Label(parent,title,0,top,width,26,portrait?14:16,gold);
            top+=30;
            int columns=portrait?1:2, column=0;
            float span=(width+8)/columns, rowHeight=0;
            RectTransform first=null;
            foreach(var entry in entries)
            {
                var card=CodexCard(parent,span*column,top,span-8,entry);
                if(column==0)first=card;
                rowHeight=Mathf.Max(rowHeight,card.sizeDelta.y);
                column++;
                if(column==columns)
                {
                    if(columns==2)
                    {
                        first.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,rowHeight);
                        card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,rowHeight);
                    }
                    top+=rowHeight+7;
                    column=0;
                    rowHeight=0;
                }
            }
            return top+(column==0?0:rowHeight+7)+8;
        }

        RectTransform CodexCard(RectTransform parent,float x,float y,float width,CodexEntry entry)
        {
            var row=Panel(parent,"Codex.Entry."+entry.id,x,y,width,0,manorInset,Slice.Inset);
            row.GetComponent<Image>().raycastTarget=false;
            bool marked=entry.state!=null;
            float badgeWidth=portrait?76:96;
            Label(row,entry.title,10,3,width-(marked?badgeWidth+25:20),28,portrait?14:16,white);
            if(marked)
            {
                var state=Badge(row,entry.state,width-badgeWidth-8,6,badgeWidth,21,11);
                BadgeLook(state,state.transform.parent.GetComponent<Image>(),entry.state,active:entry.highlighted);
            }
            float bottom=33;
            if(!string.IsNullOrEmpty(entry.stats))
                bottom+=ReadingLine(row,entry.stats,10,bottom,width-20,portrait?11:12,gold)+3;
            if(!string.IsNullOrEmpty(entry.description))
                bottom+=ReadingLine(row,entry.description,10,bottom,width-20,portrait?11:13,pale);
            row.sizeDelta=new Vector2(width,bottom+10);
            return row;
        }

        float ReadingLine(Transform parent,string text,float x,float y,float width,int size,Color color)
        {
            var label=Label(parent,text,x,y,width,1,size,color,TextAnchor.UpperLeft);
            KeepClosingMarks(label,width);
            float height=Mathf.Ceil(Mathf.Max(label.preferredHeight,size+3));
            Rect(label.rectTransform,x,y,width,height+1);
            return height+1;
        }

        static bool IsClosingMark(char ch) => "。、，；：！？）》】」』％…".IndexOf(ch)>=0;

        static void KeepClosingMarks(Text label,float width)
        {
            string text=label.text;
            if(!text.Any(IsClosingMark))return;
            var settings=label.GetGenerationSettings(new Vector2(width,0));
            settings.verticalOverflow=VerticalWrapMode.Overflow;
            using var generator=new TextGenerator(text.Length);
            generator.Populate(text,settings);
            var lines=generator.lines;
            StringBuilder adjusted=null;
            for(int row=lines.Count-1;row>0;row--)
            {
                int start=lines[row].startCharIdx;
                if(start<=0||start>=text.Length||text[start-1]=='\n'||!IsClosingMark(text[start]))continue;
                int run=start-1;
                int previous=lines[row-1].startCharIdx;
                while(run>previous&&IsClosingMark(text[run]))run--;
                if(run<=previous)continue;
                if(adjusted==null)adjusted=new StringBuilder(text);
                adjusted.Insert(run,'\n');
            }
            if(adjusted!=null)label.text=adjusted.ToString();
        }

        static string NumberText(float value) => value.ToString("0.##");
        static CodexEntry GearEntry(HeroDef hero,Manor estate)
        {
            int tier=estate.HeroArmament(hero.id);
            string description=string.Join("\n",Enumerable.Range(1,Manor.MaxHeroArmament).Select(stage=>{
                var gear=Manor.Armament(hero.id,stage);
                return stage+" 阶 · "+gear.title+"\n"+gear.Effect;
            }));
            return new CodexEntry("gear_"+hero.id,hero.name+" · 专属武装",
                tier==0?"尚未装备":Manor.Armament(hero.id,tier).title,description,tier+" 阶",tier>0);
        }
        static string UnitAttributes(UnitDef unit) =>
            "军费 "+NumberText(unit.cost)+" · 生命 "+NumberText(unit.hp)+" · 攻击 "+NumberText(unit.damage)+
            (unit.heal>0?" · 治疗 "+NumberText(unit.heal):"")+
            "\n移速 "+NumberText(unit.speed)+" · 射程 "+NumberText(unit.range)+"m · 间隔 "+NumberText(unit.cooldown)+"s";

        static string MusterSource(string id)
        {
            var offer=Manor.RecruitRequirement(id);
            return Manor.Name(offer.building)+" Lv."+offer.level+" 解锁";
        }

        static CodexEntry CombatEntry(UnitDef unit,Manor estate) => new CodexEntry(unit.id,unit.name+" · "+unit.subtitle,
            UnitAttributes(unit),unit.description+(estate==null?"":"\n"+MusterSource(unit.id)+" · 库存 "+estate.Stock(unit.id)),
            estate==null?null:"训练 Lv."+estate.UnitLevel(unit.id),estate!=null&&estate.UnitLevel(unit.id)>1);

        static CodexEntry PropEntry(PropDef prop,Manor estate) => new CodexEntry(prop.id,prop.name+" · "+prop.subtitle,
            "军费 "+NumberText(prop.cost)+" · 耐久 "+NumberText(prop.hp)+" · 半径 "+NumberText(prop.radius)+"m",
            prop.description+(estate==null?"":"\n"+MusterSource(prop.id)),estate==null?null:"库存 "+estate.Stock(prop.id));

        static CodexEntry BuildingEntry(string id)
        {
            string output=Manor.BuildingOutput(id,1);
            string stats=(id=="manor_hall"?"开局持有 · 首次升级 ":"建造 ")+Manor.BuildingPrice(id);
            int staff=Manor.StaffLimit(id);
            return new CodexEntry(id,Manor.Name(id),stats+(output.Length==0?"":"\n"+output),
                Manor.Description(id)+(staff==0?"":"\n派工上限 "+staff+" 户"));
        }

        static CodexEntry BackyardEntry(ManorBackyard entry) => new CodexEntry(entry.id,entry.title,
            entry.coin+" 金币 · "+entry.timber+" 建材",Manor.BackyardEffect(entry.id,1)+
                (entry.id=="garden"?"\nLv.2 · "+Manor.BackyardEffect(entry.id,2)+"\nLv.3 · "+Manor.BackyardEffect(entry.id,3):""));

        static IEnumerable<CodexEntry> EconomyEntries()
        {
            yield return new CodexEntry("crop","耕牧互补","麦田产粮 +"+Manor.CropCompanionPercent+"%",
                "派工农田搭配运转中的牧场或谷仓即可激活。");
            yield return new CodexEntry("reserve","谷仓储备",Manor.BuildingOutput("granary",1),
                "谷仓有派工才启用储备，缺粮与缺柴时减轻民心损失。");
            yield return new CodexEntry("forge","林铁联动","军械产量 +"+Manor.TimberSmithyPercent+"%",
                "伐木营与铁匠铺同时派工。冬季建材先保暖，再供锻造。");
            yield return new CodexEntry("market","市集三需","三项齐全时每日民心 +"+Manor.ProsperousMarketApproval,
                "备足口粮、冬季薪柴，并由酒馆或派工礼拜堂提供精神供给。");
            yield return new CodexEntry("bread","麦香联动","面包产量 +"+Manor.BreadCompanionPercent+"%",
                "面包房派工后，可由派工的麦田或谷仓增产。出炉的面包计作独立副食。");
            yield return new CodexEntry("wool","牧织联动","织坊收入 +"+Manor.PastureWeavePercent+"%",
                "牧场与织坊同时派工。冬衣每日最多节省 "+Manor.WeaverHeatCap+" 份薪柴，最低仍需一份。");
        }

        static CodexEntry NobleEntry(int rank,Manor estate)
        {
            var entry=Manor.NobleRanks[rank];
            return new CodexEntry("rank."+rank,entry.title,NobleGrantText(rank)+" · "+Manor.NoblePrivilege(rank),
                Manor.NobleRequirement(rank)+(rank>0?"\n受封支出 "+NoblePrice(entry):""),
                estate==null?null:NobleState(estate,rank),estate!=null&&(estate.nobility==rank||estate.NoblePromotionBlock(rank)==null));
        }

        static CodexEntry SectorEntry(int index,Manor estate)
        {
            var sector=Manor.Sectors[index];
            return new CodexEntry(sector.id,sector.name,"每日 +"+Manor.FormatHundredths(sector.daily)+" "+sector.resource,
                "击退 "+FrontierMap.GroupSize+" 名守军并结算后，支付 "+sector.influence+" 威望、"+sector.timber+" 建材筑哨。"+
                (sector.resource=="粮食"?"\n猎物计作独立食物来源。":""),
                estate==null?null:estate.ClaimedSector(index)?"已筑哨":estate.ClearedSector(index)?"可宣称":"未清剿",
                estate!=null&&estate.ClaimedSector(index));
        }

        static CodexEntry DevelopmentEntry(int index,Manor estate)
        {
            var perk=(ManorPerk)index;
            var definition=Manor.Developments[index];
            bool learned=estate!=null&&estate.HasDevelopment(perk);
            return new CodexEntry("perk."+index,definition.title,DevelopmentLines[index/2]+" · "+(index%2==0?"一阶":"二阶"),
                definition.effect,estate==null?null:learned?"已解锁":"未解锁",learned);
        }

        static CodexEntry HeroEntry(HeroDef hero,Manor estate)
        {
            int rank=estate?.HeroLevel(hero.id)??0;
            return new CodexEntry(hero.id,hero.name+" · "+hero.subtitle,UnitAttributes(hero),
                hero.description+"\n"+Manor.Name("tavern")+" Lv."+hero.tavernLevel+" 可招募",
                estate==null?null:rank>0?"Lv."+rank:"未招募",rank>0);
        }

        static CodexEntry SpecialistEntry(string id,Manor estate)
        {
            string effects="";
            for(int rank=1;rank<=Manor.SpecialistMaxLevel;rank++)
                effects+=(rank==1?"":"\n")+"Lv."+rank+" · "+Manor.SpecialistEffect(id,rank);
            int level=estate?.SpecialistLevel(id)??0;
            string blocked=estate?.SpecialistUnlockBlock(id);
            if(blocked!=null)effects+="\n"+blocked;
            return new CodexEntry(id,Manor.SpecialistName(id),null,effects,
                estate==null?null:level>0?"当前 Lv."+level:"未解锁",level>0);
        }
    }
}
