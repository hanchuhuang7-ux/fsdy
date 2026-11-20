using System.Collections;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        Texture2D sharedHudAtlas;
        bool modalSkinVerified,rosterScrollVerified,portraitStatusVerified,outpostPickingVerified;
        float portraitGovernanceBottomMargin;
        long flurryAllocationBytes;
        int verifiedGarrisons;

        T HudElement<T>(string name) where T:Component =>
            app.GetComponentsInChildren<T>().Single(element=>element.name==name);

        void CheckModalSkin(string title)
        {
            var shade=HudElement<Image>("Dialog overlay");
            var shadow=HudElement<Image>("Dialog shadow");
            var shell=HudElement<Image>(title);
            var rule=shell.GetComponentsInChildren<Image>().Single(image=>image.name=="Gold rule"&&image.transform.parent==shell.transform);
            Require(shade.raycastTarget&&shade.color.a>=.45f&&!shadow.raycastTarget&&
                shadow.sprite.texture==sharedHudAtlas&&shell.sprite.texture==sharedHudAtlas&&shell.type==Image.Type.Sliced&&
                rule.sprite.texture==sharedHudAtlas&&rule.rectTransform.rect.height==1,"Modal chrome lost the shared HUD texture");
            modalSkinVerified=true;
        }

        static Rect ScreenRect(RectTransform rect)
        {
            var corners=new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        }

        void CheckFloatingStatus()
        {
            Canvas.ForceUpdateCanvases();
            var notice=HudElement<RectTransform>("Manor status");
            var statusRect=ScreenRect(notice);
            var calendar=ScreenRect(HudElement<RectTransform>("Manor calendar"));
            var plot=app.GetComponentsInChildren<RectTransform>().SingleOrDefault(rect=>rect.name=="Manor plot card");
            Require((!plot||!statusRect.Overlaps(ScreenRect(plot)))&&!statusRect.Overlaps(calendar)&&statusRect.xMin>=8&&
                statusRect.xMax<=Screen.width-8&&statusRect.yMin>=6,
                "Floating status or guide overlapped a manor card or the screen edge");
            foreach(var label in notice.GetComponentsInChildren<Text>())
                Require(label.preferredHeight<=label.rectTransform.rect.height+.6f,"Floating status clipped text: "+label.text);
            var dismiss=notice.GetComponentsInChildren<Button>().SingleOrDefault(button=>button.name=="Manor.Guide.Dismiss");
            if(dismiss)
            {
                var skip=ScreenRect((RectTransform)dismiss.transform);
                Require(statusRect.Contains(skip.min)&&statusRect.Contains(skip.max),"Guide action is clipped");
                CheckButtonHit("Manor.Guide.Dismiss");
            }
            if(Viewport.Compact)portraitStatusVerified=true;
        }

        IEnumerator CheckRosterScroll()
        {
            Canvas.ForceUpdateCanvases();
            var scroll=HudElement<ScrollRect>("Scroll viewport");
            var bar=scroll.verticalScrollbar;
            var last=scroll.content.GetComponentsInChildren<Button>().Last();
            Require(scroll.content.rect.height>scroll.viewport.rect.height&&bar.gameObject.activeInHierarchy&&
                bar.handleRect.rect.height>6,"Overflowing muster has no visible thumb");
            bar.value=0;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var bounds=ScreenRect((RectTransform)last.transform);
            var viewport=ScreenRect(scroll.viewport);
            Require(bounds.yMin>=viewport.yMin+9&&bounds.yMax<=viewport.yMax,"Muster's last item has no bottom breathing room");
            CheckButtonHit(last.name);
            bar.value=1;
            yield return null;
            rosterScrollVerified=true;
        }

        void ClickSector(Vector3 world,int index,string status,bool shortcut)
        {
            var point=(Vector2)app.View.ViewCamera.WorldToScreenPoint(world);
            Require(UiHits(point).Count==0,"An outpost is behind the HUD at "+point);
            var input=app.GetComponent<BattleInput>();
            input.BeginMouse(0,point,false,false);
            input.EndMouse(point,false);
            Require(app.ManorSector==index&&ManorReadout("Manor.HoverStatus").text.Contains(status)&&
                ManorReadout("Manor.HoverStatus").text.Contains(Manor.Sectors[index].name)&&
                ManorReadout("Manor.HoverStatus").text.Contains(Manor.Sectors[index].resource)&&
                app.GetComponentsInChildren<Button>().Any(button=>button.name=="Manor.Claim.Focus")==shortcut,
                "Camp picking did not expose the sector's supply, state and shortcut: "+index);
        }

        IEnumerator CheckHudArena(bool captures)
        {
            var atlas=HudElement<Image>("Deployment").sprite.texture;
            var clear=VisibleButton("清空");
            var start=VisibleButton("开 战");
            Require(atlas==sharedHudAtlas&&HudElement<Image>("Map title").sprite.texture==atlas&&
                clear.image.sprite.texture==atlas&&clear.image.sprite!=start.image.sprite&&
                ((NativeHudButton)clear).Tone==HudTone.Danger&&((NativeHudButton)start).Tone==HudTone.Primary&&
                start.colors.normalColor!=start.colors.highlightedColor&&start.colors.pressedColor!=start.colors.highlightedColor,
                "Arena did not share the themed buttons and map cards");
            if(captures)yield return ManorShot("145-arena-deploy");
            yield return Press("开 战");
            yield return WaitForState(()=>app.Phase=="battle"&&(app.Battle?.Tick??0)>=80,"Arena did not start for the battle capture");
            yield return Press("暂停");
            Require(HudElement<Image>("Live power").sprite.texture==atlas&&HudElement<Image>("Battle controls").sprite.texture==atlas,
                "Battle HUD did not retain the layout skin");
            if(captures)yield return ManorShot("145-arena-battle");
            app.View.Focus(new Vector3(-.6f,1.0f,.22f),12.8f,24,-18);
            yield return new WaitForEndOfFrame();
            Require(!app.View.LowActorLod&&!app.InManor,"Arena close-up did not use the detailed battle meshes");
            arenaCloseup=true;
            if(captures)yield return ManorShot("160-arena-close");
            app.ReturnToDeploy();
        }
    }
}
