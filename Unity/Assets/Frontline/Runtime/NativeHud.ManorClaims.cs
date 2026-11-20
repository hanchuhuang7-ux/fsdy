using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        void GovernanceEntry(Transform parent,float x,float y,float w,float h)
        {
            var button=ManorButton(parent,"Manor.Governance",portrait?"政务":"领地政务",x,y,w,h,GovernanceDialog);
            MarkButton(button,Glyph.Council,portrait?12:15,portrait?4:10,portrait?11:13);
            var corner=Panel(button.transform,"Manor.Governance.Todo",w-(portrait?7:13),3,6,6,
                new Color(.95f,.58f,.29f),Slice.Badge).GetComponent<Image>();
            corner.raycastTarget=false;
            manorValues.Add(()=>{
                bool pending=app.Estate.Todos!=ManorTodo.None;
                SetTodoLook(button,pending);
                corner.gameObject.SetActive(pending);
            });
        }

        void SetTodoLook(Button button,bool pending) => SetButtonTone(button,pending?HudTone.Primary:HudTone.Secondary);

        static string NextTodoText(ManorTodo todo)
        {
            if((todo&ManorTodo.Dispatch)!=0)return "待办 · 领地急报";
            if((todo&ManorTodo.Nobility)!=0)return "待办 · 可受封晋爵";
            if((todo&ManorTodo.Development)!=0)return "待办 · 可解锁专精";
            if((todo&ManorTodo.Milestone)!=0)return "待办 · 可领宏愿";
            return (todo&ManorTodo.Claim)!=0?"待办 · 可宣称疆域":"";
        }

        void TerritoryDialog() => TerritoryDialog(-1);

        void TerritoryDialog(int focused)
        {
            OpenDialog("边疆疆域",(panel,width)=>{
                Value(manorDialogValues,Label(panel,"",0,0,width,28,16,gold),
                    ()=>"领地威望 "+app.Estate.influence+"  ·  建材 "+app.Estate.timber);
                Label(panel,"清剿一座营地的 5 名守军并结算归营，即可在此筑哨。",0,33,width,portrait?43:25,13,pale,TextAnchor.UpperLeft);
                float y=portrait?77:64;
                for(int index=0;index<Manor.Sectors.Count;index++)
                {
                    TerritoryRow(panel,width,y,index,focused==index);
                    y+=98;
                }
                panel.sizeDelta=new Vector2(width,y);
            },550,(bar,w)=>ManorButton(bar,"Manor.Claims.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:660);
        }

        void TerritoryRow(RectTransform parent,float width,float y,int index,bool focused)
        {
            var sector=Manor.Sectors[index];
            var row=Panel(parent,sector.id,0,y,width,92,focused?warmBadge:manorInset,Slice.Inset);
            float actionWidth=portrait?96:132, textWidth=width-actionWidth-24;
            Label(row,sector.name,10,2,textWidth,28,16,white);
            Label(row,"每日 +"+Manor.FormatHundredths(sector.daily)+" "+sector.resource+
                (sector.id=="north_glade"?" · 猎物":""),
                10,29,textWidth,21,12,thriving);
            Label(row,sector.influence+" 威望 + "+sector.timber+" 建材",10,50,textWidth,19,12,pale);
            var state=Badge(row,"",width-actionWidth-8,7,actionWidth,21,12);
            state.name="Manor.Claim.Status."+sector.id;
            var badge=state.transform.parent.GetComponent<Image>();
            var reason=Label(row,"",10,69,textWidth,19,12,danger);
            var claim=ManorButton(row,"Manor.Claim."+sector.id,"宣称并筑哨",width-actionWidth-8,35,actionWidth,36,
                ()=>app.ClaimManorSector(index),true);
            var caption=claim.GetComponentInChildren<Text>();
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                bool owned=estate.ClaimedSector(index);
                bool cleared=estate.ClearedSector(index);
                var block=estate.SectorClaimBlock(index);
                BadgeLook(state,badge,owned?"已筑哨":cleared?"可宣称":"未清剿",good:owned,active:cleared&&!owned);
                reason.text=cleared&&!owned?block??"":"";
                claim.interactable=block==null;
                caption.text=owned?"已筑哨":"宣称并筑哨";
            });
        }
    }
}
