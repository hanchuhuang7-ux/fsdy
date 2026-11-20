using System.Collections;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        bool claimsVerified,todosVerified,hoverDetailsVerified,weatherVerified;
        long weatherAllocationBytes;
        int[] weatherBudgets;

        IEnumerator CheckClaimsAndWeather(bool captures)
        {
            var estate=app.Estate;
            int timber=estate.timber,influence=estate.influence,forecastTimber=estate.Forecast().timber;
            string forecastText=ManorReadout("Manor.Forecast").text;
            var gold=VisibleButton("Mode.Manor").image.color;
            var governance=VisibleButton("Manor.Governance");
            Require((estate.Todos&ManorTodo.Claim)!=0&&governance.image.color==gold&&
                governance.GetComponentInChildren<Text>().text=="领地政务 · 待办","The territorial opportunity did not reach the top bar");
            yield return Press("Manor.Governance");
            CheckGovernanceViewport();
            CheckButtonHit("Manor.Claims");
            Require(VisibleButton("Manor.Claims").image.color==gold,"Claim tab did not highlight");
            yield return Press("Manor.Claims");
            Require(ManorReadout("Manor.Claim.Status.west_outpost").text=="可宣称"&&
                !VisibleButton("Manor.Claim.north_glade").IsInteractable()&&!VisibleButton("Manor.Claim.east_ridge").IsInteractable(),
                "Claim dialog did not distinguish the cleared sector");
            if(captures)yield return ManorShot("144-territory");
            int rebuilds=app.View.StaticRebuilds;
            yield return Press("Manor.Claim.west_outpost");
            Require(estate.claimedSectors==1&&estate.timber==timber-16&&estate.influence==influence-35&&
                estate.Forecast().timber==forecastTimber+150&&ManorReadout("Manor.Forecast").text!=forecastText&&
                ManorReadout("Manor.Claim.Status.west_outpost").text=="已宣称"&&
                !VisibleButton("Manor.Claim.west_outpost").IsInteractable()&&
                estate.chronicle.Any(row=>row.kind=="claim"&&row.subject==0)&&
                PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),"Claim costs, production, chronicle or save did not commit together");
            yield return new WaitForEndOfFrame();
            Require(app.View.ClaimedFlagMask==1&&app.View.AlertCampMask==6&&app.View.StaticRebuilds==rebuilds,
                "Claim did not replace just the first camp marker");
            yield return Press("Manor.Claims.Back");
            Require((estate.Todos&ManorTodo.Claim)==0&&VisibleButton("Manor.Claims").image.color!=gold,
                "Claim tab remained highlighted after all affordable entitlements were handled");
            todosVerified=true;
            yield return Press("关闭");
            yield return CheckFrontierPreset(2);
            if(captures)yield return ManorShot("144-claimed-frontier");

            yield return Press("Manor.Patrol");
            yield return Press("Manor.Patrol.Start");
            yield return new WaitForEndOfFrame();
            Require(app.Config.blue.Count==15&&app.View.ClaimedFlagMask==0&&app.View.AlertCampMask==7,
                "Ownership suppressed live patrol guards");
            yield return Press("Frontier.Settle");
            yield return Press("Manor.Settlement.Done");
            Require(app.Estate.claimedSectors==1&&app.View.ClaimedFlagMask==1,"Patrol did not restore its ownership marker");
            claimsVerified=true;
            yield return CheckWeatherAndHover(captures);
        }

        void CheckHoverDetails(int plot,params string[] expected)
        {
            var point=PlotPoint(plot);
            app.GetComponent<BattleInput>().HoverAt(point,UiHits(point).Count>0);
            app.GetComponent<NativeHud>().UpdateManorHover();
            string text=ManorReadout("Manor.HoverStatus").text;
            Require(app.View.HoveredPlot==plot&&expected.All(piece=>text.Contains(piece)),"Hover detail missing at "+plot+": "+text);
        }

        IEnumerator CheckWeatherAndHover(bool captures)
        {
            string saved=app.Estate.Export();
            var estate=Manor.Import(saved);
            int year=((estate.day-1)/(Manor.SeasonDays*4)+1)*Manor.SeasonDays*4+1;
            int springRain=Enumerable.Range(year+1,Manor.SeasonDays-1).First(d=>
                BattleView.WeatherForDay(d)==ManorWeather.Drizzle&&BattleView.WeatherForDay(d-1)==ManorWeather.Clear);
            estate.day=springRain-1;
            estate.eventDay=0;
            estate.SetRotation(2,"auto");
            estate.RecruitGuard();
            estate.UpgradeRetinue();
            estate.slots[2].fertility=88;
            app.ImportText(estate.Export());
            var view=app.View;
            view.FrameFrontier(1);
            yield return new WaitForEndOfFrame();
            Require(view.Weather==ManorWeather.Clear&&view.WeatherParticles==0,"Clear spring rendered weather particles");
            var sun=view.SeasonLightColor;
            app.AdvanceDays(1);
            Require(view.Weather==ManorWeather.Drizzle&&view.SeasonLightBlending,"Daily advance did not start a wet-light transition");
            yield return WaitForState(()=>!view.SeasonLightBlending,"The drizzle light transition never finished");
            yield return new WaitForEndOfFrame();
            Require(view.SeasonLightColor!=sun&&view.WeatherParticles==52&&!view.WeatherCastsShadows,"Drizzle did not render as a shadowless page");
            CheckHoverDetails(2,"麦田农庄","地力 88%","自动轮作","派工");
            CheckHoverDetails(1,"农舍区","后院 鸡舍");
            CheckHoverDetails(0,"领主宅邸","卫队 1 / 6","披甲 Lv.2");
            hoverDetailsVerified=true;
            weatherAllocationBytes=view.MeasureWeatherAllocations();
            Require(weatherAllocationBytes<=0,"Weather page allocated managed memory in steady state");

            estate.day=Enumerable.Range(year+Manor.SeasonDays,Manor.SeasonDays).First(d=>BattleView.WeatherForDay(d)==ManorWeather.Drizzle);
            app.ImportText(estate.Export());
            yield return new WaitForEndOfFrame();
            Require(view.Weather==ManorWeather.Drizzle&&view.WeatherParticles>0,"Seasonal cycle skipped summer drizzle");
            estate.day=year+Manor.SeasonDays*2;
            app.ImportText(estate.Export());
            yield return new WaitForEndOfFrame();
            Require(view.Weather==ManorWeather.Leaves&&view.WeatherParticles==52&&!view.WeatherCastsShadows,"Autumn leaves did not render");
            estate.day=year+Manor.SeasonDays*3;
            app.ImportText(estate.Export());
            view.FrameFrontier(1);
            yield return new WaitForEndOfFrame();
            CheckHoverDetails(2,"自动轮作（休耕）");
            weatherBudgets=new int[3];
            for(int tier=0;tier<3;tier++)
            {
                view.SetQuality(tier,false);
                yield return new WaitForEndOfFrame();
                Require(view.Weather==ManorWeather.Flurry&&!view.WeatherCastsShadows,"Winter weather or shadow settings failed");
                weatherBudgets[tier]=view.WeatherParticles;
            }
            Require(weatherBudgets.SequenceEqual(new[]{12,28,52}),"Weather exceeded its per-tier instance page");
            view.Focus(new Vector3(FrontierMap.BridgeStart+1.3f,.7f,0),14,32,71);
            if(captures)yield return ManorShot("144-winter-flurry");
            weatherVerified=true;
            app.ImportText(saved);
            Debug.Log("FRONTLINE_CLAIMS_WEATHER_OK cleared=1 claimed=1 upkeep=150 weather=12/28/52 weatherAllocationBytes="+
                weatherAllocationBytes+" marketInset="+governanceBottomMargin);
        }
    }
}
