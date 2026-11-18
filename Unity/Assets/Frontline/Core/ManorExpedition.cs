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
        public int ForceCost(Catalog data, IReadOnlyDictionary<string,int> force)
        {
            int cost = 0;
            foreach (var p in force)
            {
                var d = Offer(p.Key);
                if (p.Value < 0 || p.Value > Stock(p.Key)) throw new InvalidOperationException("上阵数超出库存");
                cost += p.Value * (int)(d.prop ? data.Props[p.Key].cost : data.Units[p.Key].cost);
            }
            return cost;
        }
        public void ValidateForce(Catalog data, IReadOnlyDictionary<string,int> force, bool requireUnit)
        {
            if (force == null) throw new InvalidOperationException("请选择出征队伍");
            int cost = ForceCost(data, force), units = 0, props = 0;
            foreach (var p in force) { if (recruits[p.Key].prop) props += p.Value; else units += p.Value; }
            if (requireUnit && units == 0) throw new InvalidOperationException("至少选择一名常备军");
            if (cost > Budget) throw new InvalidOperationException("出征军费超过宅邸上限");
            if (units > FieldLimit) throw new InvalidOperationException("上阵兵员超过当前补给上限");
            if (props > Catalog.MaxProps) throw new InvalidOperationException("每方最多携带十二件道具");
        }
        public sealed class Sortie
        {
            internal Manor owner;
            internal Dictionary<string,int> force;
            internal bool resolved;
            public int stage { get; internal set; }
            public bool raid { get; internal set; }
            public Deployment config { get; internal set; }

            public bool Matches(Deployment c)
            {
                if (c.map != config.map || c.seed != config.seed || c.budget != config.budget) return false;
                var roster = c.red.Select(u => u.type).Concat(c.props.Where(p => p.team == "red").Select(p => p.type));
                var counts = roster.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
                return counts.Count == force.Count && force.All(p => counts.TryGetValue(p.Key, out int n) && n == p.Value);
            }
        }
        // Search both halves in real world coordinates: shorelines, obstacles and neutral props vary by map.
        static void PlaceOne(Catalog data, Deployment c, string team, string id, bool prop)
        {
            int side = team == "red" ? -1 : 1;
            var def = prop ? null : data.Units[id];
            bool rear = def != null && (def.ranged || id == "medic");
            for (int col = 0; col < 9; col++)
            {
                float x = side * (3.3f + (rear ? (col + 3) % 9 : col) * 1.55f);
                for (int n = 0; n < 13; n++)
                {
                    float z = ((n + 1) / 2) * (n % 2 == 0 ? -1 : 1) * 1.65f;
                    if (!data.CanPlace(c, team, id, x, z, prop)) continue;
                    if (prop) c.props.Add(new PropPlacement { team = team, type = id, x = x, z = z });
                    else c.Army(team).Add(new UnitPlacement(id,x,z));
                    return;
                }
            }
            throw new InvalidOperationException("本图可用阵位不足，请减少上阵数");
        }
        public Sortie Prepare(Catalog data, int stage, bool raid, IReadOnlyDictionary<string,int> force)
        {
            if (raid) stage = Math.Min(cleared, Route.Length - 1);
            if (stage < 0 || stage >= Route.Length || stage > cleared) throw new InvalidOperationException("请先完成前一关");
            ValidateForce(data,force,true);
            var c = new Deployment { map = Route[stage], budget = Budget, seed = (uint)(day * 967 + stage * 193 + (raid ? 79 : 11)) };
            c.commander.enabled = true; c.commander.aiTeams.Add("blue");
            var selected = force.Where(p => p.Value > 0).ToDictionary(p => p.Key,p => p.Value);
            // Catalog order keeps an equivalent selection independent of the order of UI clicks.
            foreach (var id in recruits.Keys)
                if (selected.TryGetValue(id,out int count))
                    for (int i = 0; i < count; i++) PlaceOne(data,c,"red",id,recruits[id].prop);
            var enemy = opponents[stage];
            int enemies = raid ? Math.Min(enemy.Length,2 + (day / 6 + stage) % 4) : enemy.Length;
            for (int i = 0; i < enemies; i++) PlaceOne(data,c,"blue",enemy[i],false);
            c.budget = Math.Max(c.budget,(int)data.Cost(c,"blue"));
            return new Sortie { owner = this, force = selected, stage = stage, raid = raid, config = data.Validate(c) };
        }

        public Manor ReturnedState(Sortie sortie, Result result, out string receipt)
        {
            if (sortie == null || sortie.owner != this || sortie.resolved)
                throw new InvalidOperationException("远征已结算或已撤回");
            var next = Import(Export());
            var copy = new Sortie { owner = next, force = sortie.force, stage = sortie.stage, raid = sortie.raid, config = sortie.config };
            receipt = next.Settle(copy,result);
            sortie.resolved = true;
            return next;
        }

        // Calculate the entire return manifest before mutating stocks. A sortie can be settled once.
        public string Settle(Sortie sortie, Result result)
        {
            if (sortie == null || sortie.owner != this || sortie.resolved || result?.report?.units == null ||
                result.winner != "red" && result.winner != "draw" && result.winner != "blue")
                throw new InvalidOperationException("没有可结算的远征战报");
            var report = result.report.units.Where(u => u.team == "red").ToList();
            var outgoing = sortie.force.Where(p => !recruits[p.Key].prop).ToDictionary(p => p.Key,p => p.Value);
            if (report.Count != outgoing.Values.Sum() || report.Select(u => u.id).Distinct().Count() != report.Count ||
                outgoing.Any(p => report.Count(u => u.type == p.Key) != p.Value) || sortie.force.Any(p => Stock(p.Key) < p.Value))
                throw new InvalidOperationException("战报与出征名册不一致");
            int survivors = 0, wounded = 0, losses = 0;
            var returned = new Dictionary<string,int>();
            foreach (var u in report)
            {
                if (u.alive) survivors++;
                else
                {
                    // Recover one in every three fallen soldiers, rounding the total up.
                    if (losses++ % 3 != 0) continue;
                    wounded++;
                }
                returned.TryGetValue(u.type,out int count); returned[u.type] = count + 1;
            }
            bool victory = result.winner == "red";
            int coins = victory ? 22 + 7 * sortie.stage : 5, wood = victory ? 10 + 3 * sortie.stage : 2, weapons = victory ? 5 + sortie.stage : 1;
            foreach (var p in sortie.force) stock[p.Key] -= p.Value;
            foreach (var p in returned) stock[p.Key] += p.Value;
            coin = Cap(coin + coins); timber = Cap(timber + wood); arms = Cap(arms + weapons);
            if (sortie.raid) raidReady = false;
            else if (victory && sortie.stage == cleared) cleared++;
            sortie.resolved = true;
            return "归营 " + survivors + " 人 · 伤员 " + wounded + " 人 · 金币 +" + coins + " / 建材 +" + wood + " / 军械 +" + weapons;
        }
    }
}

