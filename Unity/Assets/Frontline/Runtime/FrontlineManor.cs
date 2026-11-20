using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class FrontlineApp
    {
        static string ManorKey => VisualValidation.PerformanceRequested ? "frontline.qa.performance.manor" : VisualValidation.FrontierRequested ? VisualValidation.FrontierSaveKey :
            VisualValidation.IsolatedManor ? "frontline.qa.manor.v1" : "frontline.unity.manor.v1";
        public Manor Estate { get; private set; }
        public bool InManor { get; private set; }
        public int ManorSlot { get; private set; } = -1;
        internal int ManorSector { get; private set; } = -1;
        public int ManorSpeed { get; private set; } = 1;
        public int ManorRevision {get;private set;}
        public int SelectedProp { get; private set; } = -1;
        public bool InFrontier => Phase == "frontier";
        public bool ConfirmManorExit => InManor && (Phase == "deploy" || Phase == "battle" || InFrontier && Battle?.Result==null);
        public bool HoldExpedition { get; set; }
        public bool PendingSettlement => returnedEstate != null;
        public event Action<string> ManorSound;
        public event Action<Manor.Settlement> ExpeditionReturned;
        readonly Dictionary<string,int> force = new Dictionary<string,int>();
        readonly Dictionary<string,int> raidForce = new Dictionary<string,int>();
        readonly Dictionary<string,int> patrolForce = new Dictionary<string,int>();
        static readonly IReadOnlyDictionary<string,int> EmptyForce = new Dictionary<string,int>();
        readonly ManorSupport[] supportDrafts = new ManorSupport[3];
        readonly List<string> draftKeys=new List<string>(20);
        Manor.Sortie sortie;
        float manorClock;
        Manor.Settlement returnReceipt;
        Manor returnedEstate;

        sealed class DuelDesk
        {
            public Deployment config;
            public string mode, team, hotseat, style, unit, prop, stance, tool, order;
            public bool bo3, touch, mirror, brief;
            public int limit;
            public float speed;
            public BattleSeries series;
            public ReplayPackage replay;
            public int[] selection;
            public Deployment[] undo;
        }
        DuelDesk duel;
        static int PartyIndex(bool frontier,bool raid) => frontier ? 2 : raid ? 1 : 0;
        Dictionary<string,int> Party(bool frontier,bool raid=false) => frontier ? patrolForce : raid ? raidForce : force;
        internal ManorSupport PartySupport(bool frontier,bool raid=false) => supportDrafts[PartyIndex(frontier,raid)];
        internal ManorSupport PlannedSupport => sortie?.Support ?? default;
        internal string PartySupportBlock(ManorSupport support,bool frontier,bool raid) => Estate.SupportBlock(Data,EmptyForce,support,frontier,raid);
        public int ForceCount(string id,bool frontier=false,bool raid=false) => Party(frontier,raid).TryGetValue(id,out int count) ? count : 0;
        public int ForceCost => PartyCost(false);
        public int ForceUnits => PartyUnits(false);
        public int PartyCost(bool frontier,bool raid=false) => Estate.ForceCost(Data,Party(frontier,raid)) +
            Estate.SupportCost(Data,PartySupport(frontier,raid));
        public int PartyUnits(bool frontier,bool raid=false) => Manor.ForceSize(Data,Party(frontier,raid),PartySupport(frontier,raid));
        internal ManorLogistics PlannedLogistics => sortie?.Logistics??Estate.Logistics;
        internal int PartyStrength(bool frontier,bool raid=false)
        {
            double sum=0;
            foreach(var item in Party(frontier,raid))
            {
                if(Data.Props.ContainsKey(item.Key))continue;
                bool hero=Data.Heroes.ContainsKey(item.Key);
                int level=hero?Estate.HeroLevel(item.Key):Estate.UnitLevel(item.Key);
                sum+=LevelScaling.Scale(Data.Unit(item.Key).cost,Math.Max(1,level))*item.Value;
            }
            var support=PartySupport(frontier,raid);
            if(support.guards>0)
                sum+=LevelScaling.Scale(Estate.SupportCost(Data,new ManorSupport(support.guards,false)),Estate.RetinueBattleLevel);
            if(support.mercenaries)
                sum+=LevelScaling.Scale(Estate.SupportCost(Data,new ManorSupport(0,true)),Manor.MercenaryLevel);
            return (int)Math.Round(sum);
        }
        public string ForceAddBlock(string id,bool frontier=false,bool raid=false) =>
            Estate.ForceAddBlock(Data,Party(frontier,raid),id,frontier,raid,PartySupport(frontier,raid));
        public bool AllowsOrder(string order) => !InManor || Estate.AllowsOrder(order);
        void LoadManor()
        {
            var text = PlayerPrefs.GetString(ManorKey,"");
            if (text.Length != 0)
            {
                try { Estate = Manor.Import(text); return; }
                catch (FormatException e) { Debug.LogWarning("庄园存档读取失败：" + e.Message); Tell("庄园存档无法读取，可在工坊导入备份"); }
            }
            Estate = new Manor();
        }
        void SaveManor()
        {
            PlayerPrefs.SetString(ManorKey,Estate.Export());
            PlayerPrefs.Save();
        }
        void CloseSession()
        {
            StopJob();
            Battle?.Dispose(); Battle = null; archive = null; archiveIndex = 0;
            recorded.Clear(); live = null; ReplayTime = 0; Paused = false;
            accumulator = 0; Modal = HoldExpedition = false; Selected.Clear(); SelectedProp = -1; undo.Clear();
            View.Marker = null;
        }
        public void SetManorMode(bool enter,bool abandon = false)
        {
            if (enter == InManor || !enter && ConfirmManorExit && !abandon) return;
            Manor.Settlement receipt = null;
            if (enter)
            {
                duel = new DuelDesk {
                    config = Config, mode = Mode, team = Team, hotseat = Hotseat, style = Style, unit = UnitType,
                    prop = PropType, stance = Stance, tool = Tool, order = OrderType, bo3 = Bo3, touch = TouchCamera,
                    mirror = Mirror, brief = BriefOpen, limit = PlayerLimit, speed = Speed, series = Series, replay = ReplayFile,
                    selection = Selected.ToArray(), undo = undo.ToArray()
                };
                CloseSession(); InManor = true; Mode = "expedition"; Team = "red"; Hotseat = "revealed"; Bo3 = false;
                Mirror = false; BriefOpen = false; TouchCamera = true; Series = null; ReplayFile = null;
                Config = new Deployment(); OrderType = "inspire"; Speed = 1; Phase = "manor";
                if (Estate == null) LoadManor();
                ManorSlot = ManorSector = -1; manorClock = 0; SelectDefaultForce();
                Tell("领地庄园 · 点选建地，指派领民");
            }
            else
            {
                receipt = CollectReturn();
                CloseSession(); InManor = false; sortie = null;
                var d = duel; duel = null;
                Config = d.config; Mode = d.mode; Team = d.team; Hotseat = d.hotseat; Style = d.style; UnitType = d.unit;
                PropType = d.prop; Stance = d.stance; Tool = d.tool; OrderType = d.order; Bo3 = d.bo3;
                TouchCamera = d.touch; Mirror = d.mirror; BriefOpen = d.brief; PlayerLimit = d.limit; Speed = d.speed;
                Series = d.series; ReplayFile = d.replay;
                for (int i = d.undo.Length - 1; i >= 0; i--) undo.Push(d.undo[i]);
                Selected.AddRange(d.selection);
                Phase = "deploy"; Tell("已返回演武斗蛐蛐");
            }
            Refresh();
            if (receipt != null) ExpeditionReturned?.Invoke(receipt);
        }

        void SelectDefaultForce()
        {
            SelectDefaultForce(false);
            SelectDefaultForce(true);
            SelectDefaultForce(false,true);
        }
        void SelectDefaultForce(bool frontier,bool raid=false)
        {
            var draft = Party(frontier,raid);
            supportDrafts[PartyIndex(frontier,raid)] = default;
            draft.Clear();
            int budget=Estate.ExpeditionLogistics(raid).Budget, limit=frontier?Estate.FrontierLimit:Estate.FieldLimit;
            foreach (var u in Data.Data.units)
                for (int i = 0; i < Estate.Stock(u.id); i++)
                {
                    if (PartyUnits(frontier,raid) >= limit || PartyCost(frontier,raid) + u.cost > budget) break;
                    draft[u.id] = ForceCount(u.id,frontier,raid) + 1;
                }
        }
        void RecheckForce(bool frontier,bool raid=false)
        {
            var draft=Party(frontier,raid);
            draftKeys.Clear();draftKeys.AddRange(draft.Keys);
            foreach(var id in draftKeys)draft[id]=Math.Min(draft[id],Estate.Available(id));
            int index=PartyIndex(frontier,raid);
            var support=supportDrafts[index];
            int guards=Math.Min(support.guards,Estate.RetinueReady);
            bool mercenaries=support.mercenaries&&PartySupportBlock(new ManorSupport(0,true),frontier,raid)==null;
            while(guards>0&&PartySupportBlock(new ManorSupport(guards,mercenaries),frontier,raid)!=null)guards--;
            supportDrafts[index]=new ManorSupport(guards,mercenaries);
            int units=PartyUnits(frontier,raid), cost=PartyCost(frontier,raid), budget=Estate.ExpeditionLogistics(raid).Budget;
            int limit=frontier?Estate.FrontierLimit:Estate.FieldLimit;
            // Preserve the draft as supply or morale changes. Remove the most recently added types first.
            for(int i=draftKeys.Count-1;i>=0&&(units>limit||cost>budget);i--)
            {
                string id=draftKeys[i];
                bool troop=!Data.Props.ContainsKey(id);
                int price=Manor.ItemCost(Data,id);
                while(draft[id]>0&&(cost>budget||troop&&units>limit))
                {
                    draft[id]--;cost-=price;
                    if(troop)units--;
                }
            }
        }
        public void SelectManorSlot(int index,bool focus=false)
        {
            if (Phase != "manor" || index < -1 || index >= Estate.slots.Length) return;
            ManorSector = -1;
            ManorSlot = index;
            Refresh();
            if(focus&&index>=0)View.FocusManorPlot(index);
        }
        internal void SelectManorSector(int index)
        {
            if(Phase!="manor"||(uint)index>=Manor.Sectors.Count||ManorSector==index)return;
            ManorSector=index;
            View.HoveredPlot=-1;
            Changed?.Invoke();
        }

        internal string ManorSectorStatus(int index)
        {
            var sector=Manor.Sectors[index];
            string state=Estate.ClaimedSector(index)?"已筑哨":Estate.ClearedSector(index)?"可宣称":"未清剿";
            return sector.name+"  ·  每日 +"+Manor.FormatHundredths(sector.daily)+" "+sector.resource+"  ·  "+state;
        }
        void ManorAction(Action operation,string message,string sound)
        {
            if (Phase != "manor") return;
            Try(() => {
                int oldDispatch=Estate.eventDay;
                operation();
                Estate.CompleteGuideStep(0);
                ManorRevision++; RecheckForce(false); RecheckForce(true); RecheckForce(false,true); SaveManor();
                if(Estate.eventDay!=0&&Estate.eventDay!=oldDispatch)ManorSound?.Invoke("dispatch");
                else if(sound!=null)ManorSound?.Invoke(sound);
                if (message != null) Tell(message);
                Refresh();
            });
        }
        internal void SetManorBackyard(int plot,string id) => ManorAction(()=>Estate.SetBackyard(plot,id),
            "后院已启用"+Manor.BackyardTitle(id),"build");
        internal void UnlockManorDevelopment(ManorPerk perk) => ManorAction(()=>Estate.UnlockDevelopment(perk),
            "领地专精已解锁","milestone");
        internal void ReclaimManorPlot() => ManorAction(() => Estate.ReclaimPlot(ManorSlot),
            "建地已开垦 · 收回 "+Manor.ReclaimTimber+" 建材","build");
        internal void ClaimManorMilestone(int index) => ManorAction(() => Estate.ClaimMilestone(index),"已领取宏愿奖励","milestone");
        internal void SetFrontierTier(int tier)
        {
            if(Phase=="manor"&&Estate.frontierTier!=tier)
                ManorAction(()=>Estate.SelectFrontierTier(tier),null,null);
        }

        internal void SeeGovernance()
        {
            if(Phase=="manor"&&Estate.CompleteGuideStep(1))SaveGuide();
        }
        internal void DismissManorGuide()
        {
            if(Phase=="manor"&&Estate.DismissGuide())SaveGuide();
        }
        void SaveGuide()
        {
            ManorRevision++;
            SaveManor();
            Refresh();
        }

        static string ConstructionSound(string kind) => kind=="smithy"?"forge":kind=="chapel"?"milestone":"build";
        public void Construct(string kind) => ManorAction(() => Estate.Build(ManorSlot,kind),"已建成" + Manor.Name(kind),ConstructionSound(kind));
        public void UpgradeSlot() => ManorAction(() => Estate.Upgrade(ManorSlot),"建筑已升级",
            ManorSlot>=0?ConstructionSound(Estate.slots[ManorSlot].kind):null);
        public void DemolishSlot() => ManorAction(() => Estate.Demolish(ManorSlot),"已拆除并回收部分建材","build");
        public void AssignWorker(int delta) => ManorAction(() => Estate.Assign(ManorSlot,delta),delta > 0 ? "已指派一户" : "已抽回一户",
            delta>0&&ManorSlot>=0&&Estate.slots[ManorSlot].kind=="smithy"?"forge":null);
        public void Recruit(string id) => ManorAction(() => Estate.Recruit(ManorSlot,id),"已加入庄园库存","recruit");
        public void HireHero(string id) => ManorAction(() => Estate.HireHero(ManorSlot,Data.Heroes[id]),Data.Heroes[id].name+"已加入庄园","recruit");
        public void UpgradeHero(string id) => ManorAction(() => Estate.UpgradeHero(ManorSlot,Data.Heroes[id]),Data.Heroes[id].name+"已升级","recruit");
        public void TrainUnit(string id) => ManorAction(() => Estate.Train(ManorSlot,id),Data.Units[id].name+"训练等级已提升","recruit");
        internal void SetManorTax(string id) => ManorAction(() => Estate.SetTax(id),"税率已调整",null);
        internal void SetManorDecree(string id) => ManorAction(() => Estate.SetDecree(id),"政令已调整",null);
        internal void SetManorRotation(int plot,string id) => ManorAction(() => Estate.SetRotation(plot,id),null,null);
        internal void RecruitManorGuard() => ManorAction(Estate.RecruitGuard,"亲兵已加入领主卫队","recruit");
        internal void UpgradeManorRetinue() => ManorAction(Estate.UpgradeRetinue,"卫队披甲已强化","recruit");
        internal void RefillManorRetinue() => ManorAction(Estate.RefillRetinue,"卫队已完成整补","recruit");
        internal void ToggleManorTithe() => ManorAction(() => Estate.SetTithe(Estate.tithe==0?Manor.TitheRate:0),null,null);
        internal void HoldManorFestival() => ManorAction(Estate.HoldFestival,"领地庆典已召开","milestone");
        internal void CallManorMerchant() => ManorAction(Estate.CallMerchant,"本季集市额度已刷新","trade");
        internal void ClaimManorSector(int index) => ManorAction(()=>Estate.ClaimSector(index),
            "已在"+Manor.Sectors[index].name+"筑哨","milestone");

        internal string ManorHoverStatus(int index)
        {
            if(!Estate.IsPlotCleared(index))return "建地 "+index+"  ·  待开垦林地";
            var plot=Estate.slots[index];
            string title=index==0?"领主宅邸":"建地 "+index+"  "+Manor.Name(plot.kind);
            if(plot.kind==null)return title+"  ·  可建造";
            title+="  Lv."+plot.level;
            if(index==0)
                return title+"  ·  卫队 "+Estate.RetinueReady+" / "+Estate.RetinueLimit+"  ·  披甲 Lv."+Estate.retinue.level;
            if(plot.kind=="cottage")return title+"  ·  后院 "+(plot.backyard==null?"未启用":Manor.BackyardTitle(plot.backyard));
            if(Manor.IsCrop(plot.kind))
            {
                string rotation=plot.rotation=="intensive"?"精耕":plot.rotation=="fallow"?"休耕":
                    Estate.IsRestingField(index)?"自动轮作（休耕）":"自动轮作";
                title+="  ·  地力 "+plot.fertility+"%  ·  "+rotation;
            }
            else if(plot.level>=Manor.BuildingMaxLevel)title+="  ·  已满级";
            int cap=Manor.StaffLimit(plot.kind);
            return title+(cap==0?"":"  ·  派工 "+plot.workers+" / "+cap+(plot.workers==0?"  ·  闲置":""));
        }
        internal void AssignManorPreset(string id) => ManorAction(() => Estate.AssignPreset(id),"派工已调配",null);
        internal void UpgradeSpecialist(string id) => ManorAction(() => Estate.UpgradeSpecialist(id),Manor.SpecialistName(id)+"已升级","recruit");
        internal void ResolveManorEvent(int issuedDay,int choice) => ManorAction(() => Estate.ResolveEvent(issuedDay,choice),"领地急报已结案",null);
        internal void TradeManor(string id,int units) => ManorAction(() => Estate.Trade(id,units),units>0?"已购入物资":"已售出物资","trade");
        public void AdvanceDays(int days) => ManorAction(() => Estate.Advance(days),"已推进" + days + "日","harvest");
        public void SetManorSpeed(int speed)
        {
            if (Phase != "manor" || speed < 0 || speed > 2) return;
            ManorSpeed = speed; Refresh();
        }
        void UpdateManorClock()
        {
            if (ManorSpeed == 0) return;
            manorClock += Time.unscaledDeltaTime * ManorSpeed;
            if (manorClock < 4f) return;
            manorClock %= 4f;
            if (Estate.day >= 1000000) { SetManorSpeed(0); return; }
            ManorAction(() => Estate.Advance(1),null,"harvest");
        }
        public void SelectForce(string id,int delta,bool frontier=false,bool raid=false)
        {
            if (Phase != "manor" || Math.Abs((long)delta) != 1) return;
            Try(() => {
                var draft = new Dictionary<string,int>(Party(frontier,raid));
                draft[id] = ForceCount(id,frontier,raid) + delta;
                Estate.ValidateForce(Data,draft,false,frontier,raid,PartySupport(frontier,raid));
                Party(frontier,raid)[id] = draft[id]; Refresh();
            });
        }

        internal void SelectManorSupport(ManorSupport support,bool frontier,bool raid)
        {
            if(Phase!="manor")return;
            Try(()=>{
                string blocked=PartySupportBlock(support,frontier,raid);
                if(blocked!=null)throw new InvalidOperationException(blocked);
                int beforeCost=Estate.ForceCost(Data,Party(frontier,raid));
                supportDrafts[PartyIndex(frontier,raid)]=support;
                RecheckForce(frontier,raid);
                if(Estate.ForceCost(Data,Party(frontier,raid))<beforeCost)Tell("已从普通名册腾出增援席位或军费");
                Refresh();
            });
        }

        internal void RecommendManorForce(int stage,bool raid,bool frontier)
        {
            if(Phase!="manor")return;
            Try(()=>{
                var support=PartySupport(frontier,raid);
                var next=Estate.RecommendForce(Data,stage,raid,frontier,support);
                if(next.Count==0&&support.Count==0)throw new InvalidOperationException("没有可上阵的兵员或英雄");
                var draft=Party(frontier,raid);
                draft.Clear();
                foreach(var item in next)draft.Add(item.Key,item.Value);
                Tell("推荐编队已更新，可继续调整");
                Refresh();
            });
        }
        public void PrepareFrontier()
        {
            if(Phase!="manor")return;
            var planned=Estate.PrepareFrontier(Data,patrolForce,PartySupport(true));
            var running=new Battle(Data,planned.config,Factory,true,targetScanWorkers:CpuWork.TargetWorkers);
            try
            {
                bool paid=Estate.SignMercenaryContract(planned);
                if(Estate.CompleteGuideStep(2)||paid){ManorRevision++;SaveManor();}
            }
            catch { running.Dispose(); throw; }
            CloseSession(); sortie=planned; returnedEstate=null; returnReceipt=null;
            Battle=running; Config=running.Config; ReplayFile=null;
            live=Battle.Snapshot(Battle.PrepareSnapshot()); Phase="frontier";
            Mode="frontier"; Team="red"; OrderType="inspire"; Tool="march"; TouchCamera=false; Speed=1;
            ManorSound?.Invoke("patrol");
            Tell("队伍已集结。点选外域地面可过桥推进");
            Refresh();
        }
        public void ClickFrontier(Vector3 point)
        {
            if(!InFrontier||Battle?.Result!=null)return;
            if(Tool=="order"){CommandAt(point);return;}
            if(Battle.MarchFrontier(point.x,point.z))
            {
                View.FrontierDestination=Battle.MarchDestination;
                Tell(FrontierMap.Safe(point.x)?"队伍向安全区移动":"队伍推进 · 遇敌自动接战");
            }
        }
        public void RecallFrontier()
        {
            if(!InFrontier||Battle?.Result!=null)return;
            var rally=FrontierMap.Rally;
            Battle.MarchFrontier(rally.x,rally.z);
            Tool="march";
            Tell("队伍撤向半岛集合点");
            Refresh();
        }
        public void PrepareExpedition(int stage,bool raid)
        {
            if (Phase != "manor") return;
            Try(() => {
                var planned = Estate.Prepare(Data,stage,raid,Party(false,raid),PartySupport(false,raid));
                if(Estate.CompleteGuideStep(2)){ManorRevision++;SaveManor();}
                CloseSession(); sortie = planned; returnedEstate = null; returnReceipt = null;
                Config = Json.Clone(planned.config); Phase = "deploy"; Tool = "select"; PropType = null;
                Stance = "advance"; Team = "red"; OrderType = "inspire"; TouchCamera = false;
                Mode = "expedition"; PlayerLimit = planned.Logistics.Budget; ReplayFile = null; Speed = 1;
                ManorSound?.Invoke("patrol");
                Tell((raid ? "迎战掠夺者" : "领地远征") + " · 调整队形后开战");
                Refresh(); View.ResetCamera();
            });
        }
        // Finish the return in the save as soon as PhysX reports a result. Closing the player on the result page is safe.
        void SaveResult()
        {
            if (returnedEstate != null) return;
            returnedEstate = Estate.ReturnedState(sortie,Battle.Result,out returnReceipt);
            PlayerPrefs.SetString(ManorKey,returnedEstate.Export());
            PlayerPrefs.Save();
        }
        Manor.Settlement CollectReturn()
        {
            if(InFrontier){Battle.EndFrontier();SaveResult();}
            if (Phase == "result") SaveResult();
            if (returnedEstate == null) return null;
            Estate = returnedEstate;
            var receipt = returnReceipt;
            returnedEstate = null; returnReceipt = null;
            return receipt;
        }
        public void ReturnManor()
        {
            if (!InManor || Phase == "manor") return;
            var receipt = CollectReturn();
            var message = receipt != null ? receipt.frontier?"巡边已结算":"远征已结算" : sortie == null ? "已返回庄园" : "部队已撤回庄园";
            CloseSession(); sortie = null; Phase = "manor";
            Config = new Deployment(); ReplayFile = null; ManorSlot = ManorSector = -1; TouchCamera = true;
            SelectDefaultForce(); manorClock = 0;
            Tell(message); Refresh();
            if (receipt != null) ExpeditionReturned?.Invoke(receipt);
        }
        void OpenExpeditionReplay(ReplayPackage replay)
        {
            if (InManor && Phase != "manor") throw new InvalidOperationException("请先返回庄园再导入回放");
            if (!InManor) SetManorMode(true);
            CloseSession(); sortie = null;
            Config = replay.config; ReplayFile = replay; Phase = "replay";
            if (replay.kind == "seed") StartJob(ExpandSeed(replay),Watch);
            else Watch();
            View.ResetCamera();
        }
        public void ExportManor()
        {
            if (Phase == "manor") Try(() => FileBridge.Export("frontline-manor.json",Estate.Export()));
        }
        void ImportManor(string text)
        {
            var imported = Manor.Import(text);
            if (InManor && Phase != "manor") throw new InvalidOperationException("请先返回庄园再导入存档");
            if (!InManor) SetManorMode(true);
            Estate = imported; ManorRevision++; ManorSlot = ManorSector = -1; manorClock = 0; SelectDefaultForce();
            SaveManor(); Tell("庄园存档已导入"); Refresh();
        }
    }
}

