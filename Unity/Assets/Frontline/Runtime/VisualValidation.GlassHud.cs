using System.Collections;
using System.Linq;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        static readonly string[] EstateRails={"Manor.Ledger.Quick","Mode switch","Top tools","Frontier camera","Manor calendar"};

        IEnumerator CheckGlassHud()
        {
            var hud=app.GetComponent<NativeHud>();
            var view=app.View;
            sharedHudAtlas=VisibleButton("Mode.Manor").image.sprite.texture;
            app.SetMode("sandbox");
            string arena=Json.Write(app.Config);
            Require(sharedHudAtlas.width>=288&&sharedHudAtlas.height>=144,"The shared 48 px HUD atlas is missing");
            Require(HudElement<Image>("Deployment").sprite.texture==sharedHudAtlas&&
                HudElement<Image>("Map title").sprite.texture==sharedHudAtlas,"Duel did not inherit the scene chrome");
            yield return ManorShot("200-duel-setup");
            yield return Press("开 战");
            yield return WaitForState(()=>app.Phase=="battle"&&(app.Battle?.Tick??0)>=190,"Duel did not reach live combat");
            yield return Press("暂停");
            view.Focus(new Vector3(0,.72f,.8f),31,42,-14);
            yield return new WaitForEndOfFrame();
            Require(HudElement<Image>("Live power").sprite.texture==sharedHudAtlas&&
                HudElement<Image>("Battle controls").sprite.texture==sharedHudAtlas,"Live battle used a different atlas");
            yield return ManorShot("200-duel-live");
            app.ReturnToDeploy();

            yield return Press("Mode.Manor");
            yield return Press("Manor.Speed.0");
            var estate=WideEstateFixture();
            estate.Upgrade(0);estate.Upgrade(0);
            estate.SetRotation(2,"auto");
            estate.guideDismissed=true;
            app.ImportText(estate.Export());
            view.Focus(new Vector3(FrontierMap.ManorX,3.4f,0),57,43,-18);
            yield return new WaitForEndOfFrame();
            yield return WaitForState(()=>HudElement<CanvasGroup>("Manor status").alpha==0,"Normal messages did not time out");
            float desktopExposed=CheckEstateRails();
            Require(desktopExposed>=.90f,"Unselected desktop view has less than 90% clear space");
            yield return ManorShot("200-manor-clear");

            var input=app.GetComponent<BattleInput>();
            yield return PickPlot(0);
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Inspector.Close");
            CheckButtonHit("Manor.Retinue.Open");
            view.Focus(new Vector3(FrontierMap.ManorX-2.7f,5.25f,0),33.5f,33,-18);
            yield return ManorShot("200-castle-inspector");
            yield return Press("Manor.Inspector.Close");
            Require(app.ManorSlot<0&&!app.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name=="Manor plot card"),
                "Closing the inspector left a card on the screen");
            view.FrameFrontier(1);
            yield return new WaitForEndOfFrame();
            yield return PickPlot(2);
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Rotation.auto");
            var farm=new Vector3(FrontierMap.ManorX+Manor.SlotX(2),2,Manor.SlotZ(2));
            view.Focus(farm,20,38,12);
            yield return ManorShot("200-field-inspector");
            Require(input.Shortcut(KeyCode.Escape)&&app.ManorSlot<0,"Escape did not close the inspector");
            view.FrameFrontier(1);

            hud.SetLedgerHover(true);
            Require(HudElement<Text>("Manor.Forecast.Factors").text.Length>0&&
                HudElement<Text>("Manor.Ledger.0.Net").text.StartsWith("▲")&&
                HudElement<Text>("Manor.Ledger.1.Net").text.StartsWith("▼"),"Resource deltas or hover details did not bind");
            hud.SetLedgerHover(false);
            yield return Press("Manor.Ledger.Quick");
            yield return GlassSheet("领地收支账本","200-ledger");
            yield return Press("Manor.Governance");
            yield return new WaitForEndOfFrame();
            CheckGovernanceViewport();
            yield return ManorShot("200-governance");
            yield return Press("Manor.Nobility");
            yield return GlassSheet("贵族头衔与赐土","200-nobility");
            yield return Press("Codex.Open");
            yield return Press("Codex.Tab.1");
            yield return GlassSheet("游戏图鉴","200-codex");
            yield return Press("Manor.Patrol");
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Patrol.Start");
            yield return GlassSheet("边疆巡边 · 集结队伍","200-muster");
            yield return Press("Settings.Quality");
            yield return GlassSheet("显示与声音","200-settings");

            Screen.SetResolution(390,844,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==390&&Screen.height==844,"Portrait viewport did not apply");
            yield return Press("Frontier.View.1");
            yield return WaitForState(()=>!view.CameraMoving,"Portrait manor camera did not settle");
            yield return new WaitForEndOfFrame();
            float portraitExposed=CheckEstateRails();
            CheckFloatingStatus();
            yield return ManorShot("200-portrait-manor");
            yield return Press("Manor.Governance");
            yield return new WaitForEndOfFrame();
            CheckGovernanceViewport();
            yield return GlassSheet("领地政务与集市","200-portrait-governance");
            yield return Press("Codex.Open");
            yield return Press("Codex.Tab.2");
            yield return GlassSheet("游戏图鉴","200-portrait-codex");
            yield return Press("Mode.Duel");
            Require(Json.Write(app.Config)==arena&&!app.InManor&&!view.ViewingFrontier,"Manor UI changed the arena state");
            var proof=new {version=Application.version,unity=Application.unityVersion,desktop="1280x800",portrait="390x844",
                desktopExposed,portraitExposed,governanceBottomMargin,portraitGovernanceBottomMargin,
                atlasWidth=sharedHudAtlas.width,atlasHeight=sharedHudAtlas.height,crossModeStateIntact=true,
                clickableInspector=true,ledgerHoverAndClick=true,nativeScreenshots=14};
            yield return Save("glass-hud-smoke.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_GLASS_HUD_SMOKE_OK "+Json.Write(proof));
        }

        float CheckEstateRails()
        {
            Canvas.ForceUpdateCanvases();
            Require(!app.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name=="Manor plot card"||rect.name=="Top bar"),
                "Idle manor still displays a backdrop or inspector");
            var boxes=new Rect[EstateRails.Length];
            float area=0;
            for(int i=0;i<boxes.Length;i++)
            {
                var rail=HudElement<Image>(EstateRails[i]);
                Require(rail.sprite.texture==sharedHudAtlas,"Chrome atlas differs: "+rail.name);
                var rect=boxes[i]=ScreenRect(rail.rectTransform);
                Require(rect.xMin>=5&&rect.xMax<=Screen.width-5&&rect.yMin>=6&&rect.yMax<=Screen.height-5,
                    "Chrome is clipped on the screen: "+rail.name);
                for(int j=0;j<i;j++)Require(!rect.Overlaps(boxes[j]),"Floating chrome overlapped: "+rail.name);
                area+=rect.width*rect.height;
            }
            foreach(var name in new[]{"Manor.Ledger.Quick","Manor.Governance","Manor.Plots","Manor.Patrol","Settings.Quality",
                "Frontier.View.0","Frontier.View.1","Frontier.View.2","Manor.Speed.0","Manor.Speed.1","Manor.Speed.2","Manor.Day","Manor.Season"})
                CheckButtonHit(name);
            foreach(var name in new[]{"Manor.Speed.1","Manor.Speed.2"})
            {
                var caption=((NativeHudButton)VisibleButton(name)).Caption;
                Require(caption.preferredWidth<=caption.rectTransform.rect.width+1&&caption.preferredHeight<25,
                    "Clock multiplier is clipped or wrapped: "+name);
            }
            foreach(var glyph in app.GetComponentsInChildren<Image>().Where(image=>image.name.StartsWith("Glyph ")))
                Require(glyph.sprite.texture==sharedHudAtlas,"Glyph was drawn from another texture");
            return 1-area/(Screen.width*Screen.height);
        }

        IEnumerator GlassSheet(string title,string image)
        {
            yield return new WaitForEndOfFrame();
            CheckModalSkin(title);
            var rect=ScreenRect(HudElement<Image>(title).rectTransform);
            Require(rect.xMin>=8&&rect.xMax<=Screen.width-8&&rect.yMin>=12&&rect.yMax<=Screen.height-12,
                "Sheet is clipped at "+Screen.width+" px: "+title);
            CheckButtonHit("关闭");
            yield return ManorShot(image);
            yield return Press("关闭");
        }
    }
}
