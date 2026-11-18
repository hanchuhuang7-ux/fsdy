using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    [Serializable]
    public sealed class ManorPlot
    {
        public string kind;
        public int level, workers;
    }

    // The only persistent manor state. Draft formations and battle frames belong to the running sortie.
    public sealed partial class Manor
    {
        public const int SeasonDays = 12;
        const int MaxValue = 1000000;
        public string format = "frontline-manor";
        public int version = 1;
        public int coin = 140, food = 18, timber = 80, arms = 12, workers = 2, day = 1, cleared;
        public bool raidReady;
        public ManorPlot[] slots = new ManorPlot[10];
        public Dictionary<string, int> stock = new Dictionary<string, int>(StringComparer.Ordinal);

        public static readonly string[] Buildings = { "cottage", "farm", "timber_camp", "smithy", "barracks", "stables" };
        public static readonly string[] Route = { "meadow", "bridge", "ruins", "marsh", "highland", "island" };
        static readonly float[] xs = { 0, -8, -2.8f, 2.8f, 8.4f, -8.5f, 8.5f, -7.5f, 0, 7.5f };
        static readonly float[] zs = { 0, 5.4f, 5.6f, 5.6f, 5.4f, 0, 0, -5.7f, -5.9f, -5.7f };
        public static float SlotX(int index) => xs[index];
        public static float SlotZ(int index) => zs[index];
        public static int PickSlot(float x, float z)
        {
            for (int i = 0; i < xs.Length; i++)
            {
                float dx = x - xs[i], dz = z - zs[i], radius = i == 0 ? 2.3f : 2.1f;
                if (dx * dx + dz * dz <= radius * radius) return i;
            }
            return -1;
        }

        sealed class Building
        {
            public readonly string name, detail;
            public readonly int coin, timber;
            public Building(string name, string detail, int coin, int timber)
            { this.name = name; this.detail = detail; this.coin = coin; this.timber = timber; }
        }

        static readonly Dictionary<string, Building> buildings = new Dictionary<string, Building>
        {
            { "manor_hall", new Building("领主宅邸", "每日收税。升级可扩大军费与驻军，依次解锁鼓舞、集火和盾阵。", 30, 20) },
            { "cottage", new Building("农舍区", "每级提供一户住房。有余粮时，空房会迎来新领民。", 12, 8) },
            { "farm", new Building("麦田农庄", "派工收粮。升级可提高每户收成，并为更多士兵提供补给。", 14, 8) },
            { "timber_camp", new Building("伐木采石营", "派工采集建材，用于修建、升级和锻造。", 16, 8) },
            { "smithy", new Building("铁匠铺", "每户每天用一份建材锻造军械。", 22, 12) },
            { "barracks", new Building("练兵靶场", "在此招募步兵、远程兵和军医。派驻教头可节省金币。", 22, 14) },
            { "stables", new Building("马厩工坊", "招募骑兵、投石车，置办五种战场道具。派驻工匠可节省金币。", 24, 15) }
        };

        sealed class RecruitDef
        {
            public readonly string kind;
            public readonly int level, coin, arms;
            public readonly bool prop;
            public RecruitDef(string kind, int level, int coin, int arms, bool prop = false)
            { this.kind = kind; this.level = level; this.coin = coin; this.arms = arms; this.prop = prop; }
        }

        static readonly Dictionary<string, RecruitDef> recruits = new Dictionary<string, RecruitDef>
        {
            { "militia", new RecruitDef("barracks", 1, 5, 1) }, { "archer", new RecruitDef("barracks", 1, 9, 2) },
            { "shield", new RecruitDef("barracks", 2, 10, 2) }, { "spear", new RecruitDef("barracks", 2, 8, 2) },
            { "crossbow", new RecruitDef("barracks", 2, 14, 3) }, { "bomber", new RecruitDef("barracks", 3, 16, 4) },
            { "medic", new RecruitDef("barracks", 3, 14, 3) }, { "giant", new RecruitDef("barracks", 3, 23, 6) },
            { "cavalry", new RecruitDef("stables", 1, 15, 3) }, { "barricade", new RecruitDef("stables", 1, 5, 1, true) },
            { "spikes", new RecruitDef("stables", 1, 6, 1, true) }, { "barrel", new RecruitDef("stables", 2, 7, 2, true) },
            { "spring", new RecruitDef("stables", 2, 8, 2, true) }, { "catapult", new RecruitDef("stables", 3, 22, 6) },
            { "banner", new RecruitDef("stables", 3, 9, 2, true) }
        };

        public Manor()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = new ManorPlot();
            slots[0].kind = "manor_hall"; slots[0].level = 1;
            foreach (var id in recruits.Keys) stock.Add(id, 0);
            stock["militia"] = 4; stock["archer"] = 2;
        }

        public static string Name(string kind) => buildings.TryGetValue(kind ?? "", out var b) ? b.name : "空地";
        public static string Description(string kind) => buildings.TryGetValue(kind ?? "", out var b) ? b.detail : "选择一座建筑";
        public int Stock(string id) => stock.TryGetValue(id, out int count) ? count : 0;
        public int FreeWorkers => workers - slots.Sum(p => p.workers);
        public int Housing => 2 + Levels("cottage");
        public int ArmyLimit => 8 + 5 * slots[0].level + 2 * Levels("farm");
        public int FieldLimit => food == 0 ? 3 : Math.Min(ArmyLimit, 6 + 2 * slots[0].level + Levels("farm"));
        public int Budget => slots[0].level == 1 ? 620 : slots[0].level == 2 ? 1400 : 2750;
        public int ArmySize => stock.Where(p => !recruits[p.Key].prop).Sum(p => p.Value);
        int Levels(string kind) => slots.Where(p => p.kind == kind).Sum(p => p.level);
        public static int StaffLimit(string kind, int level) => kind == null || kind == "cottage" ? 0 : level;
        public static string[] Recruits(string building, int level) =>
            recruits.Where(p => p.Value.kind == building && p.Value.level <= level).Select(p => p.Key).ToArray();
        public bool AllowsOrder(string order) => order == "inspire" || order == "focus" && slots[0].level >= 2 ||
                                                   order == "fortify" && slots[0].level >= 3;

        ManorPlot Plot(int i)
        {
            if (i < 0 || i >= slots.Length) throw new InvalidOperationException("请先选择一块建地");
            return slots[i];
        }
        static Building Def(string kind)
        {
            if (kind == null || !buildings.TryGetValue(kind, out var b)) throw new InvalidOperationException("未知建筑");
            return b;
        }
        static int HallRequirement(int level) => level == 1 ? 1 : 3;
        static string Price(int coins, int wood) => coins + " 金币 · " + wood + " 建材";
        public string BuildPrice(string kind) { var b = Def(kind); return Price(b.coin, b.timber); }
        public string UpgradePrice(int i)
        {
            var p = Plot(i);
            if (p.kind == null || p.level >= 3) return p.level == 3 ? "已达最高等级" : "";
            var b = Def(p.kind);
            return Price(b.coin * (p.level + 1), b.timber * (p.level + 1)) +
                (i == 0 ? " · 通关 " + HallRequirement(p.level) + " 关" : "");
        }
        void Pay(int coins, int wood)
        {
            if (coin < coins || timber < wood) throw new InvalidOperationException("金币或建材不足");
            coin -= coins; timber -= wood;
        }
        public void Build(int i, string kind)
        {
            var b = Def(kind); var p = Plot(i);
            if (i == 0 || p.kind != null || kind == "manor_hall") throw new InvalidOperationException("请选择空建地");
            Pay(b.coin, b.timber); p.kind = kind; p.level = 1;
        }
        public void Upgrade(int i)
        {
            var p = Plot(i); var b = Def(p.kind);
            if (p.level >= 3) throw new InvalidOperationException("已达最高等级");
            if (i == 0 && cleared < HallRequirement(p.level)) throw new InvalidOperationException("宅邸升级需通关 " + HallRequirement(p.level) + " 关");
            Pay(b.coin * (p.level + 1), b.timber * (p.level + 1)); p.level++;
        }
        public void Demolish(int i)
        {
            var p = Plot(i);
            if (i == 0 || p.kind == null) throw new InvalidOperationException("此处无法拆除");
            if (p.kind == "cottage" && workers > Housing - p.level) throw new InvalidOperationException("请先在别处备足住房");
            timber = Math.Min(MaxValue, timber + Def(p.kind).timber * p.level / 2);
            slots[i] = new ManorPlot();
        }
        public void Assign(int i, int delta)
        {
            var p = Plot(i);
            if (Math.Abs((long)delta) != 1 || p.workers + delta < 0 || p.workers + delta > StaffLimit(p.kind, p.level) || delta > FreeWorkers)
                throw new InvalidOperationException("没有空闲户或可用工位");
            p.workers += delta;
        }
        static int Cap(int n) => Math.Min(MaxValue, Math.Max(0, n));
        public void Advance(int days)
        {
            if (days < 1 || days > SeasonDays || day > MaxValue - days) throw new InvalidOperationException("推进天数超出范围");
            for (int n = 0; n < days; n++)
            {
                int harvest = 1, wood = 1, tax = 5 + 3 * slots[0].level + 2 * slots[0].workers;
                foreach (var p in slots)
                {
                    int output = p.workers * (3 + p.level);
                    if (p.kind == "farm") harvest += output + p.workers;
                    if (p.kind == "timber_camp") wood += output;
                }
                day++; coin = Cap(coin + tax); timber = Cap(timber + wood);
                food = Cap(food + harvest - workers);
                // Each staffed forge uses the same day's incoming material in plot order.
                foreach (var p in slots) if (p.kind == "smithy" && p.workers > 0)
                {
                    int working = Math.Min(timber, p.workers);
                    timber -= working; arms = Cap(arms + working * (1 + p.level));
                }
                if (workers < Housing && food >= 2 * (workers + 1)) { food -= 2; workers++; }
                if ((day - 1) % 6 == 0) raidReady = true;
            }
        }

        RecruitDef Offer(string id)
        {
            if (id == null || !recruits.TryGetValue(id, out var d)) throw new InvalidOperationException("未知征召项目");
            return d;
        }
        int RecruitCoins(RecruitDef d) => Math.Max(1, d.coin - slots.Where(p => p.kind == d.kind && p.level >= d.level).Select(p => p.workers).DefaultIfEmpty(0).Max());
        public string RecruitPrice(string id) { var d = Offer(id); return RecruitCoins(d) + " 金币 · " + d.arms + " 军械"; }
        public void Recruit(int i, string id)
        {
            var p = Plot(i); var d = Offer(id);
            if (d.kind != p.kind || p.level < d.level) throw new InvalidOperationException("建筑或等级尚未解锁此征召");
            if (!d.prop && ArmySize >= ArmyLimit || Stock(id) >= 60) throw new InvalidOperationException("库存或常备军已满");
            int coins = RecruitCoins(d);
            if (coin < coins || arms < d.arms) throw new InvalidOperationException("金币或军械不足");
            coin -= coins; arms -= d.arms; stock[id]++;
        }

        public string Export() => Json.Write(this);
        const int FileLimit = 32768;
        static Dictionary<string,object> ObjectWith(object value, params string[] keys)
        {
            var obj = value as Dictionary<string,object>;
            if (obj == null || obj.Count != keys.Length || !keys.All(obj.ContainsKey)) throw new FormatException("庄园存档字段不完整或含未知字段");
            return obj;
        }
        static int Number(object value, int min, int max)
        {
            if (!(value is double n) || Math.Truncate(n) != n || n < min || n > max) throw new FormatException("庄园数值超出范围");
            return (int)n;
        }
        public static Manor Import(string text)
        {
            var root = ObjectWith(Json.Parse(text, FileLimit), "format","version","coin","food","timber","arms","workers","day","cleared","raidReady","slots","stock");
            if (!(root["format"] is string f) || f != "frontline-manor" || Number(root["version"],1,1) != 1 || !(root["raidReady"] is bool raid))
                throw new FormatException("无效的庄园存档标识");
            var m = new Manor {
                coin = Number(root["coin"],0,MaxValue), food = Number(root["food"],0,MaxValue),
                timber = Number(root["timber"],0,MaxValue), arms = Number(root["arms"],0,MaxValue),
                workers = Number(root["workers"],2,29), day = Number(root["day"],1,MaxValue),
                cleared = Number(root["cleared"],0,Route.Length), raidReady = raid
            };
            if (!(root["slots"] is List<object> plots) || plots.Count != m.slots.Length) throw new FormatException("庄园须含宅邸与九个槽位");
            for (int i = 0; i < plots.Count; i++)
            {
                var p = ObjectWith(plots[i],"kind","level","workers");
                string kind = p["kind"] as string;
                if (p["kind"] != null && (kind == null || !buildings.ContainsKey(kind)) || (i == 0) != (kind == "manor_hall"))
                    throw new FormatException("建筑种类或位置无效");
                int level = Number(p["level"],kind == null ? 0 : 1, kind == null ? 0 : 3);
                m.slots[i] = new ManorPlot { kind = kind, level = level, workers = Number(p["workers"],0,StaffLimit(kind,level)) };
            }
            if (m.workers > m.Housing || m.FreeWorkers < 0 || m.slots[0].level > 1 && m.cleared < HallRequirement(m.slots[0].level - 1))
                throw new FormatException("庄园人口、工位或宅邸进度不一致");
            var inventory = ObjectWith(root["stock"], recruits.Keys.ToArray());
            foreach (var id in recruits.Keys) m.stock[id] = Number(inventory[id],0,60);
            // Closing a farm can leave veterans above today's supply. The historical maximum still bounds a legal reserve.
            if (m.ArmySize > 8 + 5 * m.slots[0].level + 2 * 27) throw new FormatException("常备军超出可达上限");
            return m;
        }
    }
}

