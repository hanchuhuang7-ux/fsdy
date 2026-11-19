using System;
using System.Collections.Generic;
using System.Globalization;

namespace Frontline.Core
{
    public struct ManorApprovalFactors
    {
        public int tax, food, tavern, heating, decree, chapel, settled, herbalist, market;
        public int Total => tax + food + tavern + heating + decree + chapel + settled + herbalist + market;
    }

    public readonly struct ManorMarketNeeds
    {
        public readonly bool rations, variedDiet, fuel, spirit;
        public bool Prosperous => rations && fuel && spirit;

        internal ManorMarketNeeds(bool rations,bool variedDiet,bool fuel,bool spirit)
        {
            this.rations=rations; this.variedDiet=variedDiet; this.fuel=fuel; this.spirit=spirit;
        }
    }

    public struct ManorTradeVolume
    {
        public int bought, sold;
    }

    public struct ManorMarket
    {
        public ManorTradeVolume food, timber, arms;
    }

    public readonly struct ManorTradeQuote
    {
        // Prices are in hundredths of a coin. Limits and inventory are whole units.
        public readonly int inventory, buyPrice, sellPrice, buyRemaining, sellRemaining, buyLimit, sellLimit;

        internal ManorTradeQuote(int inventory,int buyPrice,int sellPrice,int buyRemaining,int sellRemaining,int buyLimit,int sellLimit)
        {
            this.inventory = inventory;
            this.buyPrice = buyPrice; this.sellPrice = sellPrice;
            this.buyRemaining = buyRemaining; this.sellRemaining = sellRemaining;
            this.buyLimit = buyLimit; this.sellLimit = sellLimit;
        }
    }

    public sealed partial class Manor
    {
        public const int UnrestApproval = 40, FlourishingApproval = 75;
        const int BaseMarketSeasonLimit = 20, TitleMarketSeasonLimit = 35, MaxMarketSeasonLimit = 50;
        public int approval = 60;
        public string tax = "normal", decree = "none";
        public ManorMarket market;

        static readonly int[] CropSeasonRates = { 100, 115, 130, 50 };
        public int CropSeasonPercent => CropSeasonRates[Season];
        public int PastureSeasonPercent => Season == 3 ? 80 : 100;
        public int ApprovalProductionPercent => approval < UnrestApproval ? 85 : approval >= FlourishingApproval ? 110 : 100;
        public int TaxRevenuePercent => tax == "low" ? 50 : tax == "high" ? 145 : 100;
        public int TaxApprovalChange => tax == "low" ? 3 : tax == "high" ? -3 : 0;
        static readonly string[] ProsperityTitles = { "拓荒营地","安宁村落","丰饶市镇","边疆雄镇" };
        public string ProsperityTitle => ProsperityTitles[ProsperityTier];
        public int MarketSeasonLimit => MarketQuota(SpecialistLevel("merchant"));
        int MarketQuota(int merchant) => BaseMarketSeasonLimit + 5 * (ProsperityTier + merchant) + DevelopmentMarketBonus;
        int ProsperityTier
        {
            get
            {
                int buildings = BuildingCount + (HasBuilding("chapel") ? 1 : 0);
                if (workers >= 12 && approval >= FlourishingApproval && cleared >= 3 && buildings >= 10) return 3;
                if (workers >= 8 && approval >= 60 && cleared >= 1 && buildings >= 7) return 2;
                if (workers >= 4 && approval >= UnrestApproval && buildings >= 4) return 1;
                return 0;
            }
        }

        static bool ValidTax(string id) => id == "low" || id == "normal" || id == "high";
        static bool ValidDecree(string id) => id == "none" || id == "agrarian" || id == "mobilize" || id == "trade";
        public void SetTax(string id)
        {
            if (!ValidTax(id)) throw new InvalidOperationException("未知税率政策");
            tax = id;
        }
        public void SetDecree(string id)
        {
            if (!ValidDecree(id)) throw new InvalidOperationException("未知领地政令");
            decree = id;
        }

        static int ClampApproval(int value) => Math.Min(100,Math.Max(0,value));
        static int Yield(int value,int level) => value * (level == 3 ? 121 : level == 2 ? 110 : 100);

        // Combine rates before rounding once to the nearest hundredth per staffed plot or tax bill.
        static int ScaleYield(int amount,int approvalPercent,int policyPercent = 100,int seasonPercent = 100,
            int supportPercent = 100,int synergyPercent = 100,int developmentPercent = 100)
        {
            const long divisor = 100L * 100 * 100 * 100 * 100 * 100;
            return (int)(((long)amount * approvalPercent * policyPercent * seasonPercent * supportPercent *
                synergyPercent * developmentPercent + divisor / 2) / divisor);
        }

        public ManorDay Forecast()
        {
            var hall = slots[0];
            int production = ApprovalProductionPercent, season = Season;
            bool agrarian = decree == "agrarian", trade = decree == "trade", mobilize = decree == "mobilize";
            ManorSynergy synergy=ReadSynergies(out bool hasClinic);
            var balance = new ManorDay {
                food = (1 - workers) * 100, timber = 100, synergy=synergy,
                coin = ScaleYield(Yield(8 + 2 * hall.workers,hall.level),production,TaxRevenuePercent,trade ? 120 : 100,
                    100+StewardBonus(SpecialistRank("steward")))
            };
            string firstFood = null;
            bool variedDiet = false, poultry=false, staffedChapel=false;
            bool sustainableWoods=HasDevelopment(ManorPerk.SustainableWoods);
            int cropDevelopment=HasDevelopment(ManorPerk.HeavyPlough)?115:100;
            int forgeDevelopment=HasDevelopment(ManorPerk.GuildSmiths)?120:100;
            int tavern = 0, housing = 2, forgeCount = 0;
            int cropRate = CropSeasonRates[season], pastureRate = season == 3 ? 80 : 100;
            Span<int> staffedForges = stackalloc int[PlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                var plot = slots[i];
                if (plot.kind == "cottage")
                {
                    housing += WholeLevelValue(3,plot.level);
                    AddBackyardYield(plot,ref balance,ref poultry);
                }
                else if (plot.kind == "pasture") housing += WholeLevelValue(2,plot.level);
                if (plot.workers == 0) continue;
                switch (plot.kind)
                {
                    case "farm": case "irrigated_field": case "pasture":
                        bool pasture = plot.kind == "pasture";
                        int policy = agrarian ? 120 : trade && !pasture ? 90 : 100;
                        balance.food += ScaleYield(Yield(Def(plot.kind).harvest,plot.level) * plot.workers,production,policy,pasture ? pastureRate : cropRate,
                            100+synergy.grainPercent,100+(pasture?0:synergy.cropPercent),pasture?100:cropDevelopment);
                        variedDiet |= firstFood != null && firstFood != plot.kind;
                        firstFood ??= plot.kind;
                        break;
                    case "timber_camp":
                        balance.timber += ScaleYield(Yield(4,plot.level) * plot.workers,production,developmentPercent:sustainableWoods?115:100);
                        break;
                    case "tavern":
                        tavern = 2;
                        break;
                    case "chapel":
                        staffedChapel=true;
                        break;
                    case "smithy":
                        staffedForges[forgeCount++] = i;
                        break;
                }
            }
            int storedTimber = Precise(timber,remainder.timber);
            int timberAvailable = Math.Min(MaxValue * 100,storedTimber + balance.timber);
            if (season == 3)
            {
                int firewood=(workers+3)/4;
                if(sustainableWoods)firewood=Math.Max(1,firewood*65/100);
                balance.winterTimberRequired=firewood*100;
                balance.winterTimber = Math.Min(timberAvailable,balance.winterTimberRequired);
                timberAvailable -= balance.winterTimber;
            }
            // Heating has first claim on incoming material. Forges use one whole unit per worker, in plot order.
            int forgePolicy = mobilize ? 125 : agrarian ? 85 : 100;
            for (int i = 0; i < forgeCount; i++)
            {
                var plot = slots[staffedForges[i]];
                int working = Math.Min(timberAvailable / 100,plot.workers);
                timberAvailable -= working * 100;
                balance.forgeTimber += working * 100;
                balance.arms += ScaleYield(Yield(2,plot.level) * working,production,forgePolicy,
                    supportPercent:100+synergy.forgePercent,developmentPercent:forgeDevelopment);
            }
            balance.timber = timberAvailable - storedTimber;

            int storedFood = Precise(food,remainder.food);
            int foodAvailable = storedFood + balance.food;
            // Preserve up to the last stored ration. Emergency reserves do not create new stock for the market.
            int reserveDeficit=Math.Min(100,storedFood)-foodAvailable;
            if(synergy.granaryLevel>0&&reserveDeficit>0)
            {
                balance.reserveFood=Math.Min(reserveDeficit,synergy.granaryLevel*100);
                balance.food+=balance.reserveFood;
                foodAvailable+=balance.reserveFood;
            }
            bool hasRations=food>0&&foodAvailable>=100;
            bool diet=hasRations&&(variedDiet||poultry&&firstFood!=null);
            balance.marketNeeds=new ManorMarketNeeds(hasRations,diet,balance.winterTimber>=balance.winterTimberRequired,
                tavern>0||staffedChapel);
            balance.approvalFactors = new ManorApprovalFactors {
                tax = TaxApprovalChange, food = !hasRations ? -5+synergy.granaryLevel : diet ? 2 : 0,
                tavern = tavern, heating = balance.winterTimber < balance.winterTimberRequired ? -3+synergy.granaryLevel : 0,
                decree = mobilize ? -1 : 0, chapel=synergy.chapelApproval, settled=synergy.settledApproval,
                herbalist=hasClinic ? SpecialistRank("herbalist") : 0, market=balance.marketNeeds.Prosperous?1:0
            };
            balance.approvalAfter = ClampApproval(approval + balance.approvalFactors.Total);
            // Production and immigration read the morning's approval. Today's changes take effect tomorrow.
            balance.newHousehold = approval >= UnrestApproval && workers < housing && foodAvailable >= 200 * (workers + 1);
            if (balance.newHousehold) balance.food -= 200;
            return balance;
        }

        public void Advance(int days)
        {
            if (days < 1 || days > SeasonDays || day > MaxValue - days) throw new InvalidOperationException("推进天数超出范围");
            for (int n = 0; n < days; n++)
            {
                var balance = Forecast();
                Credit(ref coin,ref remainder.coin,balance.coin);
                Credit(ref food,ref remainder.food,balance.food);
                Credit(ref timber,ref remainder.timber,balance.timber);
                Credit(ref arms,ref remainder.arms,balance.arms);
                approval = balance.approvalAfter;
                if (balance.newHousehold) workers++;
                day++;
                if ((day - 1) % 6 == 0) raidReady = true;
                if ((day - 1) % SeasonDays == 0) market = default;
                UpdateSeasonEvents();
                TrackProsperity();
            }
        }

        public static string FormatHundredths(int amount) => (amount / 100m).ToString("0.##",CultureInfo.InvariantCulture);

        public ManorTradeQuote QuoteTrade(string resource)
        {
            switch (resource)
            {
                case "food": return Quote(food,remainder.food,market.food,3,2);
                case "timber": return Quote(timber,remainder.timber,market.timber,4,2);
                case "arms": return Quote(arms,remainder.arms,market.arms,8,5);
                default: throw new InvalidOperationException("未知集市物资");
            }
        }

        ManorTradeQuote Quote(int inventory,int fraction,ManorTradeVolume volume,int buy,int sell)
        {
            int merchant=SpecialistLevel("merchant");
            int sellPrice = sell * (decree == "trade" ? 110 : 100);
            int perkRate=HasDevelopment(ManorPerk.FreeMarket)?95:100;
            int buyPrice=Math.Max(sellPrice+1,(buy * (decree=="trade"?90:100)*(100-2*merchant)*perkRate+5000)/10000);
            int quota=MarketQuota(merchant);
            // Demotion closes the remaining quota while keeping this season's completed trades.
            int buyRemaining = Math.Max(0,quota - volume.bought), sellRemaining = Math.Max(0,quota - volume.sold);
            int purse = Precise(coin,remainder.coin);
            int storage = (MaxValue * 100 - Precise(inventory,fraction)) / 100;
            int buyLimit = Math.Min(buyRemaining,Math.Min(purse / buyPrice,storage));
            int sellLimit = Math.Min(sellRemaining,Math.Min(inventory,(MaxValue * 100 - purse) / sellPrice));
            return new ManorTradeQuote(inventory,buyPrice,sellPrice,buyRemaining,sellRemaining,buyLimit,sellLimit);
        }

        public void Trade(string resource,int deltaUnits)
        {
            if (deltaUnits == 0 || Math.Abs((long)deltaUnits) > MaxMarketSeasonLimit+DevelopmentMarketBonus)
                throw new InvalidOperationException("交易数量超出范围");
            var quote = QuoteTrade(resource);
            if (deltaUnits > quote.buyRemaining || deltaUnits < -quote.sellRemaining)
                throw new InvalidOperationException("本季交易额度不足");
            if (deltaUnits > quote.buyLimit || deltaUnits < -quote.sellLimit)
                throw new InvalidOperationException("金币、物资或仓储额度不足");
            switch (resource)
            {
                case "food": ApplyTrade(ref food,ref market.food,deltaUnits); break;
                case "timber": ApplyTrade(ref timber,ref market.timber,deltaUnits); break;
                case "arms": ApplyTrade(ref arms,ref market.arms,deltaUnits); break;
            }
            int price = deltaUnits > 0 ? quote.buyPrice : quote.sellPrice;
            Credit(ref coin,ref remainder.coin,-price * deltaUnits);
        }

        static void ApplyTrade(ref int inventory,ref ManorTradeVolume volume,int units)
        {
            inventory += units;
            if (units > 0) volume.bought += units;
            else volume.sold -= units;
        }

        bool ValidVolume(ManorTradeVolume volume)
        {
            int limit=MaxMarketSeasonLimit+DevelopmentMarketBonus;
            return (uint)volume.bought<=limit&&(uint)volume.sold<=limit;
        }

        bool ValidOperations() => approval >= 0 && approval <= 100 && ValidTax(tax) && ValidDecree(decree) &&
            ValidVolume(market.food) && ValidVolume(market.timber) && ValidVolume(market.arms);

        static ManorTradeVolume ReadVolume(object value,int maxVolume)
        {
            var row = ObjectWith(value,"bought","sold");
            return new ManorTradeVolume {
                bought = Number(row["bought"],0,maxVolume), sold = Number(row["sold"],0,maxVolume)
            };
        }

        void ReadOperations(Dictionary<string,object> root,int maxVolume)
        {
            approval = Number(root["approval"],0,100);
            tax = root["tax"] as string;
            decree = root["decree"] as string;
            if (!ValidTax(tax) || !ValidDecree(decree)) throw new FormatException("庄园税率或政令无效");
            var volumes = ObjectWith(root["market"],"food","timber","arms");
            market = new ManorMarket {
                food = ReadVolume(volumes["food"],maxVolume), timber = ReadVolume(volumes["timber"],maxVolume), arms = ReadVolume(volumes["arms"],maxVolume)
            };
        }
    }
}
