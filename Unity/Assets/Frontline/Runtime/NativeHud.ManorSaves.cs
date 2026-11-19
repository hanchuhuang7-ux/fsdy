using System;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly string[] GuideText={
            "① 建一块麦田并派工","② 打开领地政务","③ 集结巡边或远征"
        };

        internal bool TryManorShortcut(KeyCode key)
        {
            switch(key)
            {
                case KeyCode.G: GovernanceDialog();return true;
                case KeyCode.P: PlotList();return true;
                case KeyCode.M: MilestonesDialog();return true;
                default:return false;
            }
        }

        void ManorStatus(RectTransform notice)
        {
            float w=notice.sizeDelta.x,h=notice.sizeDelta.y;
            status=Label(notice,app.Message,9,0,w-18,h,12,pale);
            float guideWidth=portrait?194:269, actionWidth=portrait?60:80;
            var guide=Label(notice,"",w-guideWidth,0,guideWidth-actionWidth-10,h,portrait?10:12,gold);
            var skip=ManorButton(notice,"Manor.Guide.Dismiss","不再提示",w-actionWidth-4,1,actionWidth,h-2,app.DismissManorGuide);
            if(portrait)skip.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=10;
            manorValues.Add(()=>{
                var estate=app.Estate;
                bool active=estate.GuideActive;
                var rect=status.rectTransform;
                var size=rect.sizeDelta;
                float textWidth=w-(active?guideWidth+15:18);
                if(size.x!=textWidth)rect.sizeDelta=new Vector2(textWidth,size.y);
                guide.gameObject.SetActive(active);
                skip.gameObject.SetActive(active);
                if(active)guide.text=GuideText[estate.guideStep];
            });
        }

        void ManorSaves()
        {
            OpenDialog("本机庄园存档",(p,w)=>{
                for(int i=0;i<FrontlineApp.ManorSaveSlotCount;i++)
                {
                    int slot=i;
                    bool occupied=app.HasManorSave(slot),readable=false;
                    string detail="空存档槽";
                    if(occupied)
                    {
                        try
                        {
                            var saved=app.ReadManorSave(slot);
                            detail="第 "+saved.day+" 日 · "+saved.workers+" 户 · "+saved.ProsperityTitle;
                            readable=true;
                        }
                        catch(FormatException){detail="存档损坏";}
                        catch(InvalidOperationException){detail="存档无法读取";}
                    }
                    var row=Panel(p,"存档 "+(slot+1),0,i*91,w,84,manorInset);
                    Label(row,"存档 "+(slot+1),10,2,w-20,26,15,gold);
                    Label(row,detail,10,27,w-20,23,12,pale);
                    float half=(w-25)/2;
                    ManorButton(row,"Manor.SaveSlot."+slot,occupied?"覆盖":"保存",10,52,half,28,()=>{
                        if(app.HasManorSave(slot))ConfirmManorSlot(slot,true);
                        else{app.SaveManorSlot(slot,false);ManorSaves();}
                    },!occupied);
                    var load=ManorButton(row,"Manor.LoadSlot."+slot,"载入",15+half,52,half,28,()=>ConfirmManorSlot(slot,false));
                    load.interactable=readable;
                }
                p.sizeDelta=new Vector2(w,268);
            },480,(bar,w)=>ManorButton(bar,"Manor.Saves.Back","返回工坊",0,3,w,40,OpenManorWorkshop),maxWidth:580);
        }

        void ConfirmManorSlot(int slot,bool save)
        {
            string slotName="存档 "+(slot+1);
            OpenDialog(save?"覆盖"+slotName+"？":"载入"+slotName+"？",(p,w)=>{
                Label(p,save?"将当前庄园写入此槽，替换其中原有进度。":"将此槽的进度载入，并更新当前自动存档。",0,0,w,57,14,pale);
            },275,(bar,w)=>{
                ManorButton(bar,"Manor.Slot.Cancel","返回",0,3,w/2-4,40,ManorSaves);
                ManorButton(bar,save?"Manor.Slot.Overwrite":"Manor.Slot.Load",save?"确认覆盖":"确认载入",w/2+4,3,w/2-4,40,()=>{
                    if(save){app.SaveManorSlot(slot,true);ManorSaves();}
                    else{app.LoadManorSlot(slot);CloseDialog();}
                },true);
            },maxWidth:540);
        }
    }
}
