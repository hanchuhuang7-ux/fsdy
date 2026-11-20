using System;
using UnityEngine;
using UnityEngine.UI;

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
                case KeyCode.L: LedgerDialog();return true;
                case KeyCode.N: NobilityDialog();return true;
                case KeyCode.Escape when app.ManorSlot>=0: app.SelectManorSlot(-1);return true;
                default:return false;
            }
        }

        Text guideToast;
        Button guideDismiss,territoryToast;

        void BindManorToast(RectTransform notice)
        {
            float w=notice.sizeDelta.x;
            status=Label(notice,app.Message,14,0,w-28,34,portrait?11:13,white,TextAnchor.MiddleCenter);
            status.name="Manor.HoverStatus";
            guideToast=Label(notice,"",14,0,w-110,34,portrait?11:12,gold);
            guideDismiss=ManorButton(notice,"Manor.Guide.Dismiss","不再提示",w-81,4,74,26,app.DismissManorGuide);
            guideDismiss.GetComponentInChildren<Text>().fontSize=11;
            territoryToast=ManorButton(notice,"Manor.Claim.Focus","边疆疆域",w-91,4,84,26,()=>TerritoryDialog(app.ManorSector));
            territoryToast.GetComponentInChildren<Text>().fontSize=12;
            guideToast.gameObject.SetActive(false);guideDismiss.gameObject.SetActive(false);territoryToast.gameObject.SetActive(false);
            manorValues.Add(()=>{
                var estate=app.Estate;
                if(estate.GuideActive)
                {
                    guideToast.text=GuideText[estate.guideStep];
                    if(lastGuidedEstate!=estate||lastGuideStep!=estate.guideStep)
                    {
                        lastGuidedEstate=estate;
                        lastGuideStep=estate.guideStep;
                        guideUntil=Time.unscaledTime+9;
                    }
                }
                int sector=app.ManorSector;
                sectorStatus=sector<0?null:app.ManorSectorStatus(sector);
                bool claim=sector>=0&&estate.ClearedSector(sector);
                Rect(status.rectTransform,14,0,w-(claim?106:28),34);
            });
        }

        void ApplyManorToastMode(bool guide,bool visible)
        {
            bool claim=visible&&!guide&&app.ManorSector>=0&&app.Estate.ClearedSector(app.ManorSector);
            SetToastChild(status.gameObject,!guide);
            SetToastChild(guideToast.gameObject,guide);
            SetToastChild(guideDismiss.gameObject,guide);
            SetToastChild(territoryToast.gameObject,claim);
        }

        static void SetToastChild(GameObject child,bool active)
        {
            if(child.activeSelf!=active)child.SetActive(active);
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
                },accent:!save,destructive:save);
            },maxWidth:540);
        }
    }
}
