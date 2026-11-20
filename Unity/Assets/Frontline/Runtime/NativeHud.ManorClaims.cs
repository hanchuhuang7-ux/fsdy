using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        void GovernanceEntry(Transform parent,float x,float y,float w,float h)
        {
            var button=ManorButton(parent,"Manor.Governance","领地政务",x,y,w,h,GovernanceDialog);
            var caption=button.GetComponentInChildren<Text>();
            manorValues.Add(()=>{
                bool pending=app.Estate.Todos!=ManorTodo.None;
                SetTodoLook(button,pending);
                caption.text=pending?(portrait?"政务 · 待办":"领地政务 · 待办"):"领地政务";
            });
        }

        void SetTodoLook(Button button,bool pending)
        {
            button.image.color=pending?gold:manorButton;
            button.GetComponentInChildren<Text>().color=pending?ink:white;
        }

        static string NextTodoText(ManorTodo todo)
        {
            if((todo&ManorTodo.Dispatch)!=0)return "待办 · 领地急报";
            if((todo&ManorTodo.Development)!=0)return "待办 · 可解锁专精";
            if((todo&ManorTodo.Milestone)!=0)return "待办 · 可领宏愿";
            return (todo&ManorTodo.Claim)!=0?"待办 · 可宣称疆域":"";
        }

        void TerritoryDialog()
        {
            OpenDialog("边疆疆域",(panel,width)=>{
                Value(manorDialogValues,Label(panel,"",0,0,width,28,16,gold),
                    ()=>"领地威望 "+app.Estate.influence+"  ·  建材 "+app.Estate.timber);
                Label(panel,"清剿一座营地的 5 名守军并结算归营，即可在此筑哨。",0,33,width,portrait?43:25,13,pale,TextAnchor.UpperLeft);
                float y=portrait?77:64;
                for(int index=0;index<Manor.Sectors.Count;index++)
                {
                    TerritoryRow(panel,width,y,index);
                    y+=98;
                }
                panel.sizeDelta=new Vector2(width,y);
            },520,(bar,w)=>ManorButton(bar,"Manor.Claims.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:660);
        }

        void TerritoryRow(RectTransform parent,float width,float y,int index)
        {
            var sector=Manor.Sectors[index];
            var row=Panel(parent,sector.id,0,y,width,92,manorInset);
            float actionWidth=portrait?96:132, textWidth=width-actionWidth-24;
            Label(row,sector.name,10,2,textWidth,28,16,white);
            Label(row,"每日 +"+Manor.FormatHundredths(sector.daily)+" "+sector.resource+
                (sector.id=="north_glade"?" · 猎物":""),
                10,29,textWidth,21,12,thriving);
            Label(row,sector.influence+" 威望 + "+sector.timber+" 建材",10,50,textWidth,19,12,pale);
            var state=Label(row,"",10,70,textWidth,19,12,pale);
            state.name="Manor.Claim.Status."+sector.id;
            var claim=ManorButton(row,"Manor.Claim."+sector.id,"宣称并筑哨",width-actionWidth-8,24,actionWidth,37,
                ()=>app.ClaimManorSector(index));
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                bool owned=estate.ClaimedSector(index);
                var block=estate.SectorClaimBlock(index);
                state.text=block??"可宣称";
                state.color=owned?thriving:block==null?gold:pale;
                claim.interactable=block==null;
                SetTodoLook(claim,block==null);
                claim.GetComponentInChildren<Text>().text=owned?"已筑哨":"宣称并筑哨";
            });
        }
    }
}
