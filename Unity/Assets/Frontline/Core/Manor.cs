using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    [Serializable]
    public sealed class ManorPlot
    {
        public string kind, backyard, rotation;
        public int level, workers, fertility;
    }

    public sealed class ManorRemainders
    {
        public int coin, food, timber, arms;
    }

    // Amounts are in hundredths of a resource so building yields accrue exactly through Lv.3.
    public struct ManorDay
    {
        public int coin, food, timber, arms, forgeTimber, winterTimber, winterTimberRequired, reserveFood, titheFood, influence, bakeryGrain;
        public ManorApprovalFactors approvalFactors;
        public ManorSynergy synergy;
        public ManorMarketNeeds marketNeeds;
        public int approvalAfter;
        public bool newHousehold;
    }

    // The only persistent manor state. Draft formations and battle frames belong to the running sortie.
    public sealed partial class Manor
    {
        public const int SeasonDays = 12, BuildingMaxLevel = 3, OriginalPlotCount = 14, FirstOuterPlot = 18, PlotCount = 36;
        public const float PlateauHalfWidth = 42, PlateauHalfDepth = 28;
        public const float TownScaleX = 1.27f, TownScaleZ = 1.40f;
        const int MaxValue = 1000000;
        public string format = "frontline-manor";
        public int version = SaveVersion;
        public int coin = 140, food = 18, timber = 80, arms = 12, workers = 2, day = 1, cleared;
        public bool raidReady;
        public ManorPlot[] slots = new ManorPlot[PlotCount];
        public Dictionary<string, int> stock = new Dictionary<string, int>(StringComparer.Ordinal);
        public ManorRemainders remainder = new ManorRemainders();

        public static readonly string[] Buildings = { "cottage", "farm", "irrigated_field", "pasture", "timber_camp", "smithy", "barracks", "stables", "tavern", "drill_yard", "granary", "chapel", Bakery, Weaver };
        public static readonly string[] Route = { "meadow", "bridge", "ruins", "marsh", "highland", "island" };
        static readonly float[] xs = { 0, -8, -2.8f, 2.8f, 8.4f, -8.5f, 8.5f, -7.5f, 0, 7.5f, -13, 13, -13, 13,
            -18.3f, -18.3f, -18.3f, -18.3f, -12.6f, -1.2f, -12.6f, 6.2f, -24.55f, -24.55f,
            19.0f, 19.0f, 11.1f, 11.1f, 17.9f, 17.9f, 24.65f, 24.65f, -27.1f, -27.1f, 5.1f, -19.8f };
        static readonly float[] zs = { 0, 5.4f, 5.6f, 5.6f, 5.4f, 0, 0, -5.7f, -5.9f, -5.7f, 5, 5, -5, -5,
            9.8f, 5.35f, -5.35f, -9.8f, 14.85f, 14.85f, -14.85f, -14.85f, 6.1f, -6.1f,
            5.9f, -5.9f, 14.85f, -14.85f, 14.85f, -14.85f, 14.85f, -14.85f, 12.1f, -12.1f, 17.2f, -17.2f };
        public static float SlotX(int index) => xs[index]*TownScaleX;
        public static float SlotZ(int index) => index==8?-9.65f:zs[index]*TownScaleZ;
        public static int PickSlot(float x, float z)
        {
            for (int i = 0; i < xs.Length; i++)
            {
                float dx = x - SlotX(i), dz = z - SlotZ(i);
                if(i==0&&Math.Abs(dx)<=4.65f&&Math.Abs(dz)<=4.1f)return i;
                const float radius=2.35f;
                if (dx * dx + dz * dz <= radius * radius) return i;
            }
            return -1;
        }

        sealed class Building
        {
            public readonly string name, detail;
            public readonly int coin, timber, harvest, housing, army, field;
            public Building(string name, string detail, int coin, int timber, int harvest = 0, int housing = 0, int army = 0, int field = 0)
            {
                this.name = name; this.detail = detail; this.coin = coin; this.timber = timber;
                this.harvest = harvest; this.housing = housing; this.army = army; this.field = field;
            }
        }

        static readonly Dictionary<string, Building> buildings = new Dictionary<string, Building>
        {
            { "manor_hall", new Building("领主宅邸", "每日收税。升级扩大军费、驻军和行军规模，并解锁鼓舞、集火和盾阵。", 30, 20, army:13, field:8) },
            { "cottage", new Building("农舍区", "提供住房。有余粮时，空房会迎来新领民。", 12, 8, housing:3) },
            { "farm", new Building("麦田农庄", "每户收粮，提供基础驻军与行军补给。", 14, 8, harvest:5, army:2, field:1) },
            { "irrigated_field", new Building("水浇麦田", "每户收成最高，扩增驻军与行军补给。", 24, 15, harvest:8, army:4, field:2) },
            { "pasture", new Building("牧场", "派户放牧产粮，并为领民提供住房、为军队提供补给。", 20, 11, harvest:4, housing:2, army:2, field:1) },
            { "timber_camp", new Building("伐木采石营", "派工采集建材，用于修建、升级和锻造。", 16, 8) },
            { "smithy", new Building("铁匠铺", "每户每天用一份建材锻造军械。", 22, 12) },
            { "barracks", new Building("练兵靶场", "在此招募步兵、远程兵和军医。派驻教头可节省金币。", 22, 14) },
            { "stables", new Building("马厩工坊", "招募骑兵、投石车，置办五种战场道具。派驻工匠可节省金币。", 24, 15) },
            { "tavern", new Building("英雄酒馆", "派驻一户后招募和培养英雄。每级开放一位英雄。", 28, 16) },
            { "drill_yard", new Building("兵营", "永久提升兵种等级。派驻教头可节省金币，升级兵营会提高折扣。", 26, 15) },
            { "granary", new Building("谷仓", "派工提振产粮。粮食告急时启用储备，缓和缺粮与缺柴的民心损失。", 25, 16) },
            { "chapel", new Building("石礼拜堂", "稳定民心，协助救护伤员。繁荣评定额外计一座建筑。", 34, 24) },
            { Bakery, new Building("磨坊面包房", "有余粮时烘烤面包，增加粮食供应并提供独立副食。", 26, 16) },
            { Weaver, new Building("领地织坊", "派工织造布匹供市集出售，冬衣能节省过冬薪柴。", 28, 18) }
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
        public int FreeWorkers
        {
            get {int free=workers;foreach(var plot in slots)free-=plot.workers;return free;}
        }
        public int Season => (day - 1) / SeasonDays % 4;
        public int Housing => 2 + Capacity(b => b.housing);
        public int ArmyLimit => Capacity(b => b.army,2) + DevelopmentArmyBonus + NobleRanks[nobility].armyBonus;
        public int FieldLimit => food == 0 ? 3 : Math.Min(Catalog.MaxUnits, Math.Min(ArmyLimit, Capacity(b => b.field,1)+NobleRanks[nobility].fieldBonus));
        public int FrontierLimit => Math.Min(FrontierMap.MaxParty,FieldLimit);
        public int Budget => Logistics.Budget;
        public int ArmySize
        {
            get {int size=0;foreach(var item in stock)if(!recruits[item.Key].prop)size+=item.Value;return size;}
        }
        int Capacity(Func<Building,int> read,int militiaYardCapacity=0)
        {
            int total=0;
            foreach(var plot in slots)
            {
                if(plot.kind==null)continue;
                total+=WholeLevelValue(read(Def(plot.kind)),plot.level);
                if(plot.backyard=="militia_yard")total+=militiaYardCapacity;
            }
            return total;
        }
        static int Whole(double value) => (int)Math.Round(value,MidpointRounding.AwayFromZero);
        // Discrete seats and berths gain at least one per upgrade, even at very small bases.
        static int WholeLevelValue(int value,int level) => value == 0 ? 0 : Math.Max(value + level - 1,Whole(LevelScaling.Scale(value,level)));
        public static int StaffLimit(string kind) => kind == null || kind == "cottage" ? 0 :
            kind == "tavern" || kind == "drill_yard" || kind == "granary" || kind == "chapel" ? 1 : 3;
        public static string BuildingOutput(string kind,int level)
        {
            var b = Def(kind);
            var values = new List<string>();
            string Amount(int n) => LevelScaling.Scale(n,level).ToString("0.##");
            if (b.harvest > 0) values.Add(Amount(b.harvest) + " 粮 / 户·日");
            if (b.housing > 0) values.Add(WholeLevelValue(b.housing,level) + " 住房");
            if (b.army > 0) values.Add(WholeLevelValue(b.army,level) + " 驻军");
            if (b.field > 0) values.Add(WholeLevelValue(b.field,level) + " 行军");
            if (kind == "manor_hall") values.Add(Amount(8) + " 基础税 / 日 · " + Whole(LevelScaling.Scale(620,level)) + " 基础军费");
            if (kind == "timber_camp") values.Add(Amount(4) + " 建材 / 户·日");
            if (kind == "smithy") values.Add(Amount(2) + " 军械 / 户·日");
            if (kind == "drill_yard") values.Add("派工后训练省 " + WholeLevelValue(6,level) + " 金币");
            if (kind == "granary") values.Add("派工后产粮 +" + GranaryPercent(level) + "% · 储备 " + level + " 粮 / 日");
            if (kind == "chapel") values.Add("民心 +" + ChapelApproval(level,false) + " / 日 · 派工再 +1 · 伤员救护");
            if (kind == Bakery) values.Add("每户每日以 "+BreadInput+" 粮烤制 "+Amount(BreadOutput)+" 面包");
            if (kind == Weaver) values.Add(Amount(WeaverCoin)+" 金币 / 户·日 · 冬季单坊省 1 柴");
            return string.Join(" · ",values);
        }
        public static string[] Recruits(string building, int level) =>
            recruits.Where(p => p.Value.kind == building && p.Value.level <= level).Select(p => p.Key).ToArray();
        public static int OrderLevel(string order) => order == "inspire" ? 1 : order == "focus" ? 2 : order == "fortify" ? 3 : int.MaxValue;
        public bool AllowsOrder(string order) => slots[0].level >= OrderLevel(order);

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
        static int UpgradeAmount(int baseCost,int currentLevel) => Whole(LevelScaling.Scale(baseCost,currentLevel));
        public static string BuildingPrice(string kind) { var b = Def(kind); return Price(b.coin, b.timber); }
        public string BuildPrice(string kind)
        {
            var b=Def(kind);int rank=SpecialistLevel(Mason);
            return Price(MasonryCost(b.coin,rank),MasonryCost(b.timber,rank));
        }
        public string UpgradePrice(int i)
        {
            var p = Plot(i);
            if (p.kind == null || p.level >= BuildingMaxLevel) return p.level == BuildingMaxLevel ? "已达最高等级" : "";
            var b = Def(p.kind);
            int rank=SpecialistLevel(Mason);
            return Price(MasonryCost(UpgradeAmount(b.coin,p.level),rank),MasonryCost(UpgradeAmount(b.timber,p.level),rank)) +
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
            if (!IsPlotGranted(i)) throw new InvalidOperationException("晋升"+NobleRanks[PlotRank(i)].title+"后开放");
            if (!IsPlotCleared(i)) throw new InvalidOperationException("请先开垦这块建地");
            int rank=SpecialistLevel(Mason);
            Pay(MasonryCost(b.coin,rank),MasonryCost(b.timber,rank)); p.kind = kind; p.level = 1;
            if (IsCrop(kind)) { p.fertility = DefaultFertility; p.rotation = "auto"; }
            if(kind=="granary"||kind=="chapel"||IsCraft(kind))Remember(kind,i);
            TrackProsperity();
        }
        public void Upgrade(int i)
        {
            var p = Plot(i); var b = Def(p.kind);
            if (p.level >= BuildingMaxLevel) throw new InvalidOperationException("已达最高等级");
            if (i == 0 && cleared < HallRequirement(p.level)) throw new InvalidOperationException("宅邸升级需通关 " + HallRequirement(p.level) + " 关");
            int rank=SpecialistLevel(Mason);
            Pay(MasonryCost(UpgradeAmount(b.coin,p.level),rank),MasonryCost(UpgradeAmount(b.timber,p.level),rank)); p.level++;
        }
        public void Demolish(int i)
        {
            var p = Plot(i);
            if (i == 0 || p.kind == null) throw new InvalidOperationException("此处无法拆除");
            var b = Def(p.kind);
            if (workers > Housing - WholeLevelValue(b.housing,p.level))
                throw new InvalidOperationException("请先在别处备足住房");
            Credit(ref timber,ref remainder.timber,UpgradeRefund(b,p.level) * 100);
            slots[i] = new ManorPlot();
        }
        public void Assign(int i, int delta)
        {
            var p = Plot(i);
            if (Math.Abs((long)delta) != 1 || p.workers + delta < 0 || p.workers + delta > StaffLimit(p.kind) || delta > FreeWorkers)
                throw new InvalidOperationException("没有空闲户或可用工位");
            p.workers += delta;
            seasonalWork=false;
        }
        public bool CanUpgradePlot(int index)
        {
            if((uint)index>=slots.Length||!IsPlotGranted(index))return false;
            var plot=slots[index];
            if(plot.kind==null||plot.level>=BuildingMaxLevel||index==0&&cleared<HallRequirement(plot.level))return false;
            var building=Def(plot.kind);
            int rank=SpecialistLevel(Mason);
            return coin>=MasonryCost(UpgradeAmount(building.coin,plot.level),rank)&&
                timber>=MasonryCost(UpgradeAmount(building.timber,plot.level),rank);
        }
        static int UpgradeRefund(Building b,int level) => (int)LevelScaling.Scale(b.timber * .5,level);
        static int Precise(int whole,int fraction) => whole * 100 + fraction;
        static void Credit(ref int whole,ref int fraction,int amount)
        {
            int total = Math.Min(MaxValue * 100,Math.Max(0,Precise(whole,fraction) + amount));
            whole = total / 100;
            fraction = total % 100;
        }
        static RecruitDef Offer(string id)
        {
            if (id == null || !recruits.TryGetValue(id, out var d)) throw new InvalidOperationException("未知征召项目");
            return d;
        }
        public static (string building,int level) RecruitRequirement(string id)
        {
            var offer=Offer(id);
            return (offer.kind,offer.level);
        }
        int RecruitCoins(RecruitDef d)
        {
            int discount = slots.Where(p => p.kind == d.kind && p.level >= d.level)
                .Select(p => WholeLevelValue(p.workers,p.level)).DefaultIfEmpty(0).Max();
            int baseCost = Math.Max(1,d.coin - discount);
            int amount = baseCost * (!d.prop && decree == "mobilize" ? 90 : 100);
            if(d.prop)return amount;
            int developmentRate=d.kind=="barracks"&&HasDevelopment(ManorPerk.BurgageMilitia)?88:100;
            return (amount * (100 - WatchTrainingDiscount) * developmentRate + 5000) / 10000;
        }
        public string RecruitPrice(string id) { var d = Offer(id); return FormatHundredths(RecruitCoins(d)) + " 金币 · " + d.arms + " 军械"; }
        public void Recruit(int i, string id)
        {
            var p = Plot(i); var d = Offer(id);
            if (d.kind != p.kind || p.level < d.level) throw new InvalidOperationException("建筑或等级尚未解锁此征召");
            if (!d.prop && ArmySize >= ArmyLimit || Stock(id) >= 60) throw new InvalidOperationException("库存或常备军已满");
            int coins = RecruitCoins(d);
            if (Precise(coin,remainder.coin) < coins || arms < d.arms) throw new InvalidOperationException("金币或军械不足");
            Credit(ref coin,ref remainder.coin,-coins);
            arms -= d.arms; stock[id]++;
        }

        const int FileLimit = 32768;
        const int SpecialistSaveVersion = 5, ExpansionSaveVersion = 6, FrontierSaveVersion = 7, DevelopmentSaveVersion = 8;
        const int RetinueSaveVersion = 9, ClaimsSaveVersion = 10, NobilitySaveVersion = 11, CraftSaveVersion = 12;
        internal const int ArmamentSaveVersion = 13;
        public const int SaveVersion = ArmamentSaveVersion;
        static readonly string[] LegacySaveFields = { "format","version","coin","food","timber","arms","workers","day","cleared","raidReady","slots","stock" };
        static readonly string[] ProgressSaveFields = LegacySaveFields.Concat(new[] { "remainder","training","heroes" }).ToArray();
        static readonly string[] OperationsSaveFields = ProgressSaveFields.Concat(new[] { "approval","tax","decree","market" }).ToArray();
        static readonly string[] EventSaveFields = OperationsSaveFields.Concat(new[] { "eventDay" }).ToArray();
        static readonly string[] SpecialistSaveFields = EventSaveFields.Concat(new[] { "specialists" }).ToArray();
        static readonly string[] EstateSaveFields = SpecialistSaveFields.Concat(new[] { "reclaimedPlots","milestoneClaims","frontierSwept" }).ToArray();
        static readonly string[] FrontierSaveFields = EstateSaveFields.Concat(new[] { "frontierTier","guideStep","guideDismissed" }).ToArray();
        static readonly string[] DevelopmentSaveFields = FrontierSaveFields.Concat(new[] { "prosperityPeak","developmentMask","chronicle" }).ToArray();
        static readonly string[] RetinueSaveFields = DevelopmentSaveFields.Concat(new[] { "retinue","influence","tithe" }).ToArray();
        static readonly string[] ClaimSaveFields = RetinueSaveFields.Concat(new[] { "clearedSectors","claimedSectors" }).ToArray();
        static readonly string[] NobleSaveFields = ClaimSaveFields.Concat(new[] { "nobility" }).ToArray();
        static readonly string[] SaveFields = NobleSaveFields.Concat(new[] { "seasonalWork" }).ToArray();
        static readonly string[] LegacyPlotSaveFields = { "kind","level","workers" };
        static readonly string[] BackyardPlotSaveFields = { "kind","level","workers","backyard" };
        static readonly string[] PlotSaveFields = { "kind","level","workers","backyard","fertility","rotation" };

        public string Export()
        {
            if (version != SaveVersion) throw new FormatException("庄园存档版本无效");
            var text = Json.Write(this);
            Import(text);
            return text;
        }

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
            var raw = Json.Parse(text,FileLimit) as Dictionary<string,object>;
            if (raw == null || !raw.TryGetValue("version",out var fileVersion)) throw new FormatException("庄园存档缺少版本");
            int schema = Number(fileVersion,1,SaveVersion);
            bool legacy = schema == 1;
            var root = ObjectWith(raw,schema switch {
                1 => LegacySaveFields, 2 => ProgressSaveFields, 3 => OperationsSaveFields, 4 => EventSaveFields,
                SpecialistSaveVersion => SpecialistSaveFields, ExpansionSaveVersion => EstateSaveFields,
                FrontierSaveVersion => FrontierSaveFields, DevelopmentSaveVersion => DevelopmentSaveFields,
                RetinueSaveVersion => RetinueSaveFields, ClaimsSaveVersion => ClaimSaveFields,
                NobilitySaveVersion or CraftSaveVersion => NobleSaveFields, _ => SaveFields
            });
            if (!(root["format"] is string f) || f != "frontline-manor" || !(root["raidReady"] is bool raid))
                throw new FormatException("无效的庄园存档标识");
            bool nobleSchema=schema>=NobilitySaveVersion;
            bool craftSchema=schema>=CraftSaveVersion;
            int layoutSize = schema < SpecialistSaveVersion ? OriginalPlotCount : schema == SpecialistSaveVersion ? FirstOuterPlot :
                craftSchema ? PlotCount : nobleSchema ? LegacyNoblePlotCount : FirstNoblePlot;
            if (!(root["slots"] is List<object> plots) || plots.Count != layoutSize && !(legacy && plots.Count == 10))
                throw new FormatException("庄园建地数量无效");
            int maxHouseholds = 2 + (legacy ? BuildingMaxLevel : WholeLevelValue(3,BuildingMaxLevel)) * (plots.Count - 1);
            var m = new Manor {
                coin = Number(root["coin"],0,MaxValue), food = Number(root["food"],0,MaxValue),
                timber = Number(root["timber"],0,MaxValue), arms = Number(root["arms"],0,MaxValue),
                workers = Number(root["workers"],2,maxHouseholds), day = Number(root["day"],1,MaxValue),
                cleared = Number(root["cleared"],0,Route.Length), raidReady = raid,
                nobility = nobleSchema ? Number(root["nobility"],0,NobleRanks.Count-1) : 0
            };
            bool developed = schema >= DevelopmentSaveVersion, hasRetinue = schema >= RetinueSaveVersion;
            var plotFields = hasRetinue ? PlotSaveFields : developed ? BackyardPlotSaveFields : LegacyPlotSaveFields;
            for (int i = 0; i < plots.Count; i++)
            {
                var p = ObjectWith(plots[i],plotFields);
                string kind = p["kind"] as string;
                if (p["kind"] != null && (kind == null || !buildings.ContainsKey(kind) ||
                    legacy && (kind == "irrigated_field" || kind == "pasture" || kind == "tavern" || kind == "drill_yard") ||
                    schema < SpecialistSaveVersion && (kind == "granary" || kind == "chapel") ||
                    !craftSchema && IsCraft(kind)) ||
                    (i == 0) != (kind == "manor_hall"))
                    throw new FormatException("建筑种类或位置无效");
                int level = Number(p["level"],kind == null ? 0 : 1, kind == null ? 0 : BuildingMaxLevel);
                int staff = StaffLimit(kind);
                string backyard=null;
                if(developed&&p["backyard"]!=null)
                {
                    backyard=p["backyard"] as string;
                    if(backyard==null||kind!="cottage"||BackyardIndex(backyard)<0)throw new FormatException("后院副业无效");
                }
                m.slots[i] = new ManorPlot { kind=kind, level=level, backyard=backyard,
                    workers=Number(p["workers"],0,legacy?Math.Min(staff,level):staff) };
                ReadFertility(m.slots[i],p,hasRetinue);
            }
            int housing = legacy ? 2 + m.slots.Where(p => p.kind == "cottage").Sum(p => p.level) : m.Housing;
            if (m.workers > housing || m.FreeWorkers < 0 || m.slots[0].level > 1 && m.cleared < HallRequirement(m.slots[0].level - 1))
                throw new FormatException("庄园人口、工位或宅邸进度不一致");
            if(developed)m.ReadDevelopment(root);
            var inventory = ObjectWith(root["stock"], recruits.Keys.ToArray());
            foreach (var id in recruits.Keys) m.stock[id] = Number(inventory[id],0,60);
            // Closing a farm can leave veterans above today's supply. The historical maximum still bounds a legal reserve.
            int usableLand=craftSchema?m.GrantedPlots:nobleSchema?FirstNoblePlot+m.nobility*LegacyNoblePlotsPerRank:plots.Count;
            int maxArmy = legacy ? 8 + 5 * m.slots[0].level + 2 * 27 :
                Math.Max(77,WholeLevelValue(13,m.slots[0].level) + WholeLevelValue(4,BuildingMaxLevel) * (usableLand - 1));
            if (m.ArmySize > maxArmy + m.DevelopmentArmyBonus + NobleRanks[m.nobility].armyBonus)
                throw new FormatException("常备军超出可达上限");
            if (!legacy)
            {
                var parts = ObjectWith(root["remainder"],"coin","food","timber","arms");
                m.remainder = new ManorRemainders {
                    coin = Number(parts["coin"],0,m.coin < MaxValue ? 99 : 0), food = Number(parts["food"],0,m.food < MaxValue ? 99 : 0),
                    timber = Number(parts["timber"],0,m.timber < MaxValue ? 99 : 0), arms = Number(parts["arms"],0,m.arms < MaxValue ? 99 : 0)
                };
                m.ReadProgress(root["training"],root["heroes"],schema>=ArmamentSaveVersion);
            }
            if(schema>=ArmamentSaveVersion)
                m.seasonalWork=root["seasonalWork"] is bool adaptive?adaptive:throw new FormatException("季候调配状态无效");
            if (schema >= 3) m.ReadOperations(root,schema == 3 ? BaseMarketSeasonLimit : schema == 4 ? TitleMarketSeasonLimit : MaxMarketSeasonLimit + m.DevelopmentMarketBonus);
            if (schema >= 4)
            {
                m.eventDay = Number(root["eventDay"],0,m.day);
                if (!ValidEventDay(m.eventDay,m.day)) throw new FormatException("领地急报日期无效");
            }
            if (schema >= SpecialistSaveVersion) m.ReadSpecialists(root["specialists"],craftSchema);
            if (schema >= ExpansionSaveVersion) m.ReadExpansion(root);
            if (schema >= FrontierSaveVersion) m.ReadProgression(root);
            else m.guideDismissed = true;
            if (schema >= ClaimsSaveVersion) m.ReadClaims(root);
            if(nobleSchema)m.ReadNobility();
            if(developed)
            {
                if(!m.ValidDevelopment())throw new FormatException("发展点或专精前置条件无效");
                m.ReadChronicle(root["chronicle"],schema);
            }
            else m.prosperityPeak=m.ProsperityTier;
            if (hasRetinue)
            {
                m.ReadRetinue(root["retinue"]);
                m.influence = Number(root["influence"],0,InfluenceMax);
                m.tithe = Number(root["tithe"],0,TitheRate);
                if (m.tithe != 0 && m.tithe != TitheRate) throw new FormatException("什一赈济税率无效");
            }
            return m;
        }
    }
}

