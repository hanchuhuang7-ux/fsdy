using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class FrontlineApp
    {
        static string ManorKey => VisualValidation.IsolatedManor ? "frontline.qa.manor.v1" : "frontline.unity.manor.v1";
        public Manor Estate { get; private set; }
        public bool InManor { get; private set; }
        public int ManorSlot { get; private set; } = -1;
        public int ManorSpeed { get; private set; } = 1;
        public event Action<string> ManorSound;
        readonly Dictionary<string,int> force = new Dictionary<string,int>();
        Manor.Sortie sortie;
        float manorClock;
        string returnMessage;
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
        public int ForceCount(string id) => force.TryGetValue(id,out int count) ? count : 0;
        public int ForceCost => Estate.ForceCost(Data,force);
        public int ForceUnits => force.Where(p => Data.Units.ContainsKey(p.Key)).Sum(p => p.Value);
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
            accumulator = 0; Modal = false; Selected.Clear(); undo.Clear();
            View.Marker = null;
        }
        public void SetManorMode(bool enter)
        {
            if (enter == InManor) return;
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
                ManorSlot = -1; manorClock = 0; SelectDefaultForce();
                Tell("领地庄园 · 点选建地，指派领民");
            }
            else
            {
                if (Phase == "result") { SaveResult(); Estate = returnedEstate; }
                CloseSession(); InManor = false; sortie = null; returnedEstate = null;
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
        }

        void SelectDefaultForce()
        {
            force.Clear();
            foreach (var u in Data.Data.units)
                for (int i = 0; i < Estate.Stock(u.id); i++)
                {
                    if (ForceUnits >= Estate.FieldLimit || ForceCost + u.cost > Estate.Budget) break;
                    force[u.id] = ForceCount(u.id) + 1;
                }
        }
        void RecheckForce()
        {
            // Preserve a hand-picked party as supply shrinks. Only remove the surplus.
            foreach (var id in force.Keys.ToArray())
                while (force[id] > 0 && (force[id] > Estate.Stock(id) ||
                    Data.Units.ContainsKey(id) && ForceUnits > Estate.FieldLimit)) force[id]--;
        }
        public void SelectManorSlot(int index)
        {
            if (Phase != "manor" || index < -1 || index >= Estate.slots.Length) return;
            ManorSlot = index; Refresh();
        }
        void ManorAction(Action operation,string message,string sound)
        {
            if (Phase != "manor") return;
            Try(() => {
                operation(); RecheckForce(); SaveManor();
                if (sound != null) ManorSound?.Invoke(sound);
                if (message != null) Tell(message);
                Refresh();
            });
        }
        public void Construct(string kind) => ManorAction(() => Estate.Build(ManorSlot,kind),"已建成" + Manor.Name(kind),"build");
        public void UpgradeSlot() => ManorAction(() => Estate.Upgrade(ManorSlot),"建筑已升级","build");
        public void DemolishSlot() => ManorAction(() => Estate.Demolish(ManorSlot),"已拆除并回收部分建材","build");
        public void AssignWorker(int delta) => ManorAction(() => Estate.Assign(ManorSlot,delta),delta > 0 ? "已指派一户" : "已抽回一户",null);
        public void Recruit(string id) => ManorAction(() => Estate.Recruit(ManorSlot,id),"已加入庄园库存","recruit");
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
        public void SelectForce(string id,int delta)
        {
            if (Phase != "manor" || Math.Abs((long)delta) != 1) return;
            Try(() => {
                var draft = new Dictionary<string,int>(force);
                draft[id] = ForceCount(id) + delta;
                Estate.ValidateForce(Data,draft,false);
                force[id] = draft[id]; Refresh();
            });
        }
        public void PrepareExpedition(int stage,bool raid)
        {
            if (Phase != "manor") return;
            Try(() => {
                var planned = Estate.Prepare(Data,stage,raid,force);
                CloseSession(); sortie = planned; returnedEstate = null; returnMessage = null;
                Config = Json.Clone(planned.config); Phase = "deploy"; Tool = "select"; PropType = null;
                Stance = "advance"; Team = "red"; OrderType = "inspire"; TouchCamera = false;
                Mode = "expedition"; PlayerLimit = Estate.Budget; ReplayFile = null; Speed = 1;
                Tell((raid ? "迎战掠夺者" : "领地远征") + " · 调整队形后开战");
                Refresh(); View.ResetCamera();
            });
        }
        // Finish the return in the save as soon as PhysX reports a result. Closing the player on the result page is safe.
        void SaveResult()
        {
            if (returnedEstate != null) return;
            returnedEstate = Estate.ReturnedState(sortie,Battle.Result,out returnMessage);
            PlayerPrefs.SetString(ManorKey,returnedEstate.Export());
            PlayerPrefs.Save();
        }
        public void ReturnManor()
        {
            if (!InManor || Phase == "manor") return;
            if (Phase == "result") { SaveResult(); Estate = returnedEstate; }
            var message = returnedEstate != null ? returnMessage : "部队已撤回庄园";
            CloseSession(); sortie = null; returnedEstate = null; Phase = "manor";
            Config = new Deployment(); ReplayFile = null; ManorSlot = -1; TouchCamera = true;
            SelectDefaultForce(); manorClock = 0;
            Tell(message); Refresh();
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
            Estate = imported; ManorSlot = -1; manorClock = 0; SelectDefaultForce();
            SaveManor(); Tell("庄园存档已导入"); Refresh();
        }
    }
}

