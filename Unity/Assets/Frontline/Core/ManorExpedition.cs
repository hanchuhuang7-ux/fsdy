using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        static readonly string[][] opponents = {
            new[] { "militia", "militia", "archer" },
            new[] { "shield", "spear", "archer", "militia" },
            new[] { "shield", "shield", "crossbow", "spear", "archer" },
            new[] { "militia", "spear", "spear", "archer", "medic", "cavalry" },
            new[] { "shield", "shield", "giant", "crossbow", "spear", "bomber", "archer" },
            new[] { "shield", "giant", "cavalry", "spear", "archer", "crossbow", "medic", "catapult" }
        };
        public int RaidStage => Math.Min(cleared,Route.Length - 1);
        public IReadOnlyList<string> EnemyRoster(int stage,bool raid)
        {
            var enemy = opponents[raid ? RaidStage : stage];
            return raid ? enemy.Take(Math.Min(enemy.Length,2 + (day / 6 + RaidStage) % 4)).ToArray() : enemy;
        }
        public int EnemyCost(Catalog data,int stage,bool raid) => EnemyRoster(stage,raid).Sum(id => (int)data.Units[id].cost);
        public static int ItemCost(Catalog data,string id) => (int)(data.Props.TryGetValue(id,out var prop) ? prop.cost : data.Unit(id).cost);
        public static int ForceSize(Catalog data,IReadOnlyDictionary<string,int> force,ManorSupport support = default) =>
            force.Where(p => !data.Props.ContainsKey(p.Key)).Sum(p => p.Value) + support.Count;
        static int GearSize(Catalog data,IReadOnlyDictionary<string,int> force) =>
            force.Where(p => data.Props.ContainsKey(p.Key)).Sum(p => p.Value);
        public int ForceCost(Catalog data, IReadOnlyDictionary<string,int> force)
        {
            int cost = 0;
            foreach (var p in force)
            {
                if (!recruits.ContainsKey(p.Key) && !data.Heroes.ContainsKey(p.Key)) throw new InvalidOperationException("未知出征项目");
                if (p.Value < 0 || p.Value > Available(p.Key)) throw new InvalidOperationException("上阵数超出库存或英雄正在休整");
                cost += p.Value * ItemCost(data,p.Key);
            }
            return cost;
        }
        string ForceLimit(int cost,int units,int gear,bool frontier,bool raid)
        {
            if (frontier && gear != 0) return "巡边仅派兵员与英雄";
            if (cost > ExpeditionLogistics(raid&&!frontier).Budget) return "超出宅邸军费";
            if (units > (frontier ? FrontierLimit : FieldLimit))
                return food == 0 ? "缺粮 · 最多 3 人" : frontier ? "已达巡边人数上限" : "已达行军上限";
            if (gear > Catalog.MaxProps) return "道具最多 12 件";
            return null;
        }
        public string ForceAddBlock(Catalog data,IReadOnlyDictionary<string,int> force,string id,bool frontier=false,bool raid=false,ManorSupport support=default)
        {
            if (data.Heroes.ContainsKey(id) && HeroLevel(id) == 0) return "英雄尚未招募";
            if (HeroRest(id) > 0) return "休整 " + HeroRest(id) + " 日";
            if (force.TryGetValue(id,out int count) && count >= Available(id) || Available(id) == 0)
                return data.Heroes.ContainsKey(id) ? "英雄已上阵" : "库存不足";
            bool prop = data.Props.ContainsKey(id);
            return ForceLimit(ForceCost(data,force) + SupportCost(data,support) + ItemCost(data,id),ForceSize(data,force,support) + (prop ? 0 : 1),
                GearSize(data,force) + (prop ? 1 : 0),frontier,raid);
        }
        public string SupportBlock(Catalog data,IReadOnlyDictionary<string,int> force,ManorSupport support,bool frontier,bool raid)
        {
            if (support.guards < 0 || support.guards > RetinueReady) return "可用卫队不足";
            if (support.mercenaries && !CanHireMercenaries) return "威望或金币不足";
            return ForceLimit(ForceCost(data,force) + SupportCost(data,support),ForceSize(data,force,support),GearSize(data,force),frontier,raid);
        }
        public void ValidateForce(Catalog data, IReadOnlyDictionary<string,int> force, bool requireUnit,bool frontier=false,bool raid=false,ManorSupport support=default)
        {
            if (force == null) throw new InvalidOperationException("请选择出征队伍");
            string reason = SupportBlock(data,force,support,frontier,raid);
            if (reason != null) throw new InvalidOperationException(reason);
            if (requireUnit && ForceSize(data,force,support) == 0) throw new InvalidOperationException("至少选择一名上阵兵员");
        }
        public sealed class Sortie
        {
            internal Manor owner;
            internal Dictionary<string,int> force;
            internal Dictionary<string,int> gear;
            internal (string type,int level)[] roster;
            internal bool resolved, contractPaid;
            public int stage { get; internal set; }
            public bool raid { get; internal set; }
            public int FrontierTier { get; internal set; }
            public Deployment config { get; internal set; }
            public ManorLogistics Logistics { get; internal set; }
            public ManorSupport Support { get; internal set; }
            public bool IsFrontier => config.map == FrontierMap.Id;

            public bool Matches(Deployment c)
            {
                if (c.map != config.map || c.seed != config.seed || c.budget != config.budget || c.manorHall != config.manorHall ||
                    c.red.Count != roster.Length) return false;
                for (int i = 0; i < roster.Length; i++)
                    if (c.red[i].type != roster[i].type || c.red[i].level != roster[i].level) return false;
                var counts = c.props.Where(p => p.team == "red").GroupBy(p => p.type).ToDictionary(g => g.Key,g => g.Count());
                return counts.Count == gear.Count && gear.All(p => counts.TryGetValue(p.Key,out int n) && n == p.Value);
            }

            public Deployment MoveProp(Catalog data,Deployment draft,int index,float x,float z)
            {
                if (index < 0 || index >= draft.props.Count || draft.props[index].team != "red")
                    throw new InvalidOperationException("请选择一件己方携带道具");
                var next = Json.Clone(draft);
                var prop = next.props[index];
                next.props.RemoveAt(index);
                if (!data.CanPlace(next,"red",prop.type,x,z,true)) throw new InvalidOperationException("道具位置无效或重叠");
                prop.x = x; prop.z = z;
                next.props.Insert(index,prop);
                return next;
            }

            public Deployment MoveTroops(Catalog data,Deployment draft,IReadOnlyList<int> indices,float dx,float dz)
            {
                if (indices.Any(i => i < 0 || i >= draft.red.Count)) throw new InvalidOperationException("请选择己方部队");
                var positions = indices.Select(i => new UnitPlacement(null,draft.red[i].x + dx,draft.red[i].z + dz)).ToList();
                return PlaceTroops(data,draft,indices,positions);
            }

            public Deployment PlaceTroops(Catalog data,Deployment draft,IReadOnlyList<int> indices,IReadOnlyList<UnitPlacement> positions)
            {
                var next = Json.Clone(draft);
                var ordered = next.red.ToArray();
                var chosen = new HashSet<int>(indices);
                if (chosen.Count == 0 || chosen.Count != indices.Count || positions.Count != indices.Count || chosen.Any(i => i < 0 || i >= ordered.Length))
                    throw new InvalidOperationException("请选择己方部队");
                next.red = ordered.Where((_,i) => !chosen.Contains(i)).ToList();
                for (int i = 0; i < indices.Count; i++)
                {
                    var u = ordered[indices[i]];
                    u.x = positions[i].x; u.z = positions[i].z;
                    if (!data.CanPlace(next,"red",u.type,u.x,u.z)) throw new InvalidOperationException("部队位置无效或重叠");
                    next.red.Add(u);
                }
                next.red = ordered.ToList();
                return next;
            }
        }
        // Search each half around the map's shorelines, obstacles and neutral props.
        static void PlaceOne(Catalog data, Deployment c, string team, string id, bool prop,int level = 0)
        {
            int side = team == "red" ? -1 : 1;
            var def = prop ? null : data.Unit(id);
            bool rear = def != null && (def.ranged || def.heal > 0);
            for (int col = 0; col < 9; col++)
            {
                float x = side * (3.3f + (rear ? (col + 3) % 9 : col) * 1.55f);
                for (int n = 0; n < 13; n++)
                {
                    float z = ((n + 1) / 2) * (n % 2 == 0 ? -1 : 1) * 1.65f;
                    if (!data.CanPlace(c, team, id, x, z, prop)) continue;
                    if (prop) c.props.Add(new PropPlacement { team = team, type = id, x = x, z = z });
                    else c.Army(team).Add(new UnitPlacement(id,x,z) { level = level });
                    return;
                }
            }
            throw new InvalidOperationException("本图可用阵位不足，请减少上阵数");
        }
        public Sortie Prepare(Catalog data, int stage, bool raid, IReadOnlyDictionary<string,int> force,ManorSupport support=default)
        {
            if (raid) stage = RaidStage;
            if (stage < 0 || stage >= Route.Length || stage > cleared) throw new InvalidOperationException("请先完成前一关");
            ValidateForce(data,force,true,raid:raid,support:support);
            var c = new Deployment { map = Route[stage], budget = ExpeditionLogistics(raid).Budget, manorHall = slots[0].level,
                seed = (uint)(day * 967 + stage * 193 + (raid ? 79 : 11)) };
            c.commander.enabled = true; c.commander.aiTeams.Add("blue");
            return Muster(data,c,stage,raid,force,support);
        }

        public Sortie PrepareFrontier(Catalog data,IReadOnlyDictionary<string,int> force,ManorSupport support=default)
        {
            ValidateForce(data,force,true,true,support:support);
            if(frontierTier<1||frontierTier>MaxFrontierTier)throw new InvalidOperationException("外域警戒阶位尚未解锁");
            var c = new Deployment { map=FrontierMap.Id, budget=Budget, manorHall=slots[0].level,
                seed=(uint)(day*967+451+(frontierTier-1)*193) };
            c.commander.enabled=true;
            var roster=FrontierMap.Roster(frontierTier);
            for(int i=0;i<roster.Count;i++)
            {
                var spawn=FrontierMap.Guards[i].spawn;
                c.blue.Add(new UnitPlacement(roster[i],spawn.x,spawn.z){level=frontierTier==1?0:frontierTier});
            }
            var planned=Muster(data,c,-1,false,force,support);
            planned.FrontierTier=frontierTier;
            return planned;
        }

        static void PlaceRally(Catalog data,Deployment c,string id,int level,int total)
        {
            int index=c.red.Count,rank=index/FrontierMap.Columns(total);
            float z=FrontierMap.Lane(index,total);
            for(int back=0;back<12;back++)
            {
                float x=FrontierMap.BridgeStart-2.2f-rank*1.75f-back*1.15f;
                if(!data.CanPlace(c,"red",id,x,z))continue;
                c.red.Add(new UnitPlacement(id,x,z){level=level});return;
            }
            throw new InvalidOperationException("集合区阵位不足");
        }

        Sortie Muster(Catalog data,Deployment c,int stage,bool raid,IReadOnlyDictionary<string,int> force,ManorSupport support)
        {
            var selected = force.Where(p => p.Value > 0).ToDictionary(p => p.Key,p => p.Value);
            var gear = new Dictionary<string,int>(StringComparer.Ordinal);
            int total=c.map==FrontierMap.Id?ForceSize(data,force,support):0;
            void Add(string id,int level,bool prop)
            {
                if(c.map==FrontierMap.Id) PlaceRally(data,c,id,level,total);
                else PlaceOne(data,c,"red",id,prop,level);
            }
            // Catalog order keeps an equivalent selection independent of the order of UI clicks.
            foreach (var id in recruits.Keys)
                if (selected.TryGetValue(id,out int count))
                {
                    bool prop = recruits[id].prop;
                    int level = prop ? 0 : UnitLevel(id);
                    if (prop) gear.Add(id,count);
                    for (int i = 0; i < count; i++) Add(id,level,prop);
                }
            foreach (var id in HeroIds)
                if (selected.ContainsKey(id))
                    Add(id,HeroLevel(id),false);
            for (int i = 0; i < support.guards; i++) Add(RetinueRole(i),RetinueBattleLevel,false);
            if (support.mercenaries)
                for (int i = 0; i < MercenarySize; i++) Add(MercenaryRole(i),MercenaryLevel,false);
            if(c.map!=FrontierMap.Id)
                foreach(var id in EnemyRoster(stage,raid)) PlaceOne(data,c,"blue",id,false);
            c.budget = Math.Max(c.budget,(int)data.Cost(c,"blue"));
            return new Sortie { owner = this, force = selected, gear = gear, stage = stage, raid = raid, Support = support,
                roster = c.red.Select(u => (u.type,u.level)).ToArray(), Logistics = ExpeditionLogistics(raid), config = data.Validate(c,true) };
        }

        public sealed class TroopReturn
        {
            public int survivors, wounded, fallen;
        }

        public sealed class Settlement
        {
            public string winner;
            public int stage, clearedBefore, clearedAfter, coin, timber, arms, defeated, approvalBefore, approvalAfter, frontierTier;
            public int influence, guards, guardLosses, mercenaries, mercenaryLosses;
            public ManorLogistics logistics;
            public bool raid, frontier, withdrew;
            public readonly Dictionary<string,TroopReturn> troops = new Dictionary<string,TroopReturn>();
            public readonly Dictionary<string,int> spent = new Dictionary<string,int>();
            public readonly Dictionary<string,int> heroes = new Dictionary<string,int>();
        }

        public Manor ReturnedState(Sortie sortie, Result result, out Settlement receipt)
        {
            if (sortie == null || sortie.owner != this || sortie.resolved)
                throw new InvalidOperationException("远征已结算或已撤回");
            var next = Import(Json.Write(this));
            var copy = new Sortie { owner = next, force = sortie.force, gear = sortie.gear, roster = sortie.roster, stage = sortie.stage,
                raid = sortie.raid, FrontierTier=sortie.FrontierTier, Logistics = sortie.Logistics, config = sortie.config,
                Support = sortie.Support, contractPaid = sortie.contractPaid };
            receipt = next.Settle(copy,result);
            sortie.resolved = true;
            return next;
        }

        // Calculate the entire return manifest before mutating stocks. A sortie can be settled once.
        public Settlement Settle(Sortie sortie, Result result)
        {
            if (sortie == null || sortie.owner != this || sortie.resolved || result?.report?.units == null ||
                result.winner != "red" && result.winner != "draw" && result.winner != "blue" ||
                sortie.Support.mercenaries && !sortie.contractPaid)
                throw new InvalidOperationException("没有可结算的远征战报");
            var report = result.report.units.Where(u => u.team == "red").ToList();
            if (report.Count != sortie.roster.Length || report.Select(u => u.id).Distinct().Count() != report.Count ||
                report.Any(u => u.id < 1 || u.id > sortie.roster.Length || u.type != sortie.roster[u.id-1].type) ||
                sortie.force.Any(p => Available(p.Key) < p.Value) || sortie.Support.guards > RetinueReady)
                throw new InvalidOperationException("战报与出征名册不一致");
            var receipt = new Settlement { winner = result.winner, stage = sortie.stage, raid = sortie.raid, clearedBefore = cleared,
                frontier = sortie.IsFrontier, frontierTier=sortie.FrontierTier, withdrew = result.reason == "withdraw",
                logistics = sortie.Logistics, approvalBefore = approval, guards = sortie.Support.guards,
                mercenaries = sortie.Support.mercenaries ? MercenarySize : 0 };
            if(receipt.frontier)
            {
                var enemies=result.report.units.Where(u=>u.team=="blue").ToArray();
                if(enemies.Length!=sortie.config.blue.Count || enemies.Select(u=>u.id).Distinct().Count()!=enemies.Length)
                    throw new InvalidOperationException("战报与边疆敌情不一致");
                receipt.defeated=enemies.Count(u=>!u.alive);
            }
            int casualties = 0, regulars = report.Count - sortie.Support.Count;
            int guardEnd = regulars + receipt.guards;
            foreach (var u in report)
            {
                // Battle assigns red IDs in roster order, including identical units from different sources.
                if (u.id > guardEnd) { if (!u.alive) receipt.mercenaryLosses++; continue; }
                if (u.id > regulars) { if (!u.alive) receipt.guardLosses++; continue; }
                if (heroes.ContainsKey(u.type))
                {
                    receipt.heroes.Add(u.type,u.alive ? 0 : Math.Min(receipt.logistics.heroRestDays,MaxValue - day));
                    continue;
                }
                if (!receipt.troops.TryGetValue(u.type,out var row)) receipt.troops.Add(u.type,row = new TroopReturn());
                if (u.alive) row.survivors++;
                // Recover a share of all downed troops in report order, rounding the total up.
                else if (receipt.logistics.RecoversWound(casualties++)) row.wounded++;
                else row.fallen++;
            }
            bool victory = result.winner == "red";
            int coins,wood,weapons;
            if(receipt.frontier)
            {
                int reward=FrontierMap.RewardPercent(receipt.frontierTier);
                coins=receipt.defeated*3*reward/100;
                wood=receipt.defeated*reward/100;
                weapons=(receipt.defeated+1)/2*reward/100;
            }
            else
            {
                coins=victory ? 22 + 7 * sortie.stage : 5;
                wood=victory ? 10 + 3 * sortie.stage : 2;
                weapons=victory ? 5 + sortie.stage : 1;
            }
            foreach (var p in sortie.force)
                if (stock.ContainsKey(p.Key))
                {
                    stock[p.Key] -= p.Value;
                    if (recruits[p.Key].prop) receipt.spent.Add(p.Key,p.Value);
                }
            foreach (var p in receipt.troops) stock[p.Key] += p.Value.survivors + p.Value.wounded;
            foreach (var p in receipt.heroes) heroes[p.Key].restUntil = p.Value > 0 ? day + p.Value : 0;
            retinue.wounded += receipt.guardLosses;
            int oldCoin = coin, oldTimber = timber, oldArms = arms;
            Credit(ref coin,ref remainder.coin,coins * 100);
            Credit(ref timber,ref remainder.timber,wood * 100);
            Credit(ref arms,ref remainder.arms,weapons * 100);
            receipt.coin = coin - oldCoin; receipt.timber = timber - oldTimber; receipt.arms = arms - oldArms;
            if (sortie.raid) raidReady = false;
            else if (!receipt.frontier && victory && sortie.stage == cleared) cleared++;
            receipt.clearedAfter = cleared;
            int approvalChange;
            if (receipt.frontier)
            {
                approvalChange = receipt.defeated / FrontierMap.GroupSize * FrontierMap.ApprovalPerGroup(receipt.frontierTier);
                frontierSwept |= receipt.defeated == FrontierMap.Guards.Count;
            }
            else if (victory) approvalChange = receipt.clearedAfter > receipt.clearedBefore ? 8 : 4;
            else approvalChange = !receipt.raid && result.winner == "blue" ? -4 : 0;
            receipt.approvalAfter = approval = ClampApproval(approval + approvalChange);
            if (receipt.frontier && receipt.defeated == FrontierMap.Guards.Count)
                Remember("frontier",receipt.frontierTier,receipt.influence = GainInfluence(FrontierInfluence));
            else if (!receipt.frontier && victory)
                Remember(receipt.raid ? "raid" : "expedition",receipt.stage,
                    receipt.influence = GainInfluence(receipt.raid ? RaidInfluence : ExpeditionInfluence));
            TrackProsperity();
            sortie.resolved = true;
            return receipt;
        }
    }
}

