using System;

namespace Frontline.Core
{
    public enum ManorLedgerSource
    {
        Hall, Settlement, Fields, Pasture, Timber, Forge, Bakery, Weaver, Backyard, Frontier,
        Synergies, Development, Specialists, Nobility, Rations, Winter, ForgeInput, BakeryInput,
        Reserves, Immigration, Tithe, Tax, Diet, Tavern, Chapel, Policy, Market, Limits, Count
    }

    public enum ManorLedgerResource { Coin, Food, Timber, Arms, Approval }

    public struct ManorLedgerLine
    {
        public int coin, food, timber, arms, approval;

        public int Amount(ManorLedgerResource resource) => resource switch {
            ManorLedgerResource.Coin=>coin, ManorLedgerResource.Food=>food, ManorLedgerResource.Timber=>timber,
            ManorLedgerResource.Arms=>arms, _=>approval
        };

        internal void Add(ManorLedgerResource resource,int amount)
        {
            switch(resource)
            {
                case ManorLedgerResource.Coin: coin+=amount; break;
                case ManorLedgerResource.Food: food+=amount; break;
                case ManorLedgerResource.Timber: timber+=amount; break;
                case ManorLedgerResource.Arms: arms+=amount; break;
                case ManorLedgerResource.Approval: approval+=amount; break;
            }
        }
    }

    public sealed partial class Manor
    {
        public ManorLedgerLine[] DailyLedger()
        {
            var rows=new ManorLedgerLine[(int)ManorLedgerSource.Count];
            Forecast(rows);
            return rows;
        }

        static void Record(Span<ManorLedgerLine> rows,ManorLedgerSource source,ManorLedgerResource resource,int amount)
        {
            if(!rows.IsEmpty)rows[(int)source].Add(resource,amount);
        }

        int DailyOutput(Span<ManorLedgerLine> rows,ManorLedgerSource source,ManorLedgerResource resource,
            int amount,int production,int policy=100,int season=100,int support=100,int synergy=100,int development=100,
            int fertility=100,bool specialist=false)
        {
            int scaled=ScaleYield(amount,production,policy,season,support,synergy,development,fertility);
            int finished=NobleYield(scaled);
            if(rows.IsEmpty)return finished;

            // Each modifier gets the exact marginal hundredths it adds. The columns therefore reconcile
            // after rounding even when several rates apply to the same household.
            int baseline=ScaleYield(amount,production,policy,season,fertilityPercent:fertility);
            int supported=support==100?baseline:ScaleYield(amount,production,policy,season,support,fertilityPercent:fertility);
            int combined=development==100?scaled:ScaleYield(amount,production,policy,season,support,synergy,fertilityPercent:fertility);
            Record(rows,source,resource,baseline);
            Record(rows,specialist?ManorLedgerSource.Specialists:ManorLedgerSource.Synergies,resource,supported-baseline);
            Record(rows,ManorLedgerSource.Synergies,resource,combined-supported);
            Record(rows,ManorLedgerSource.Development,resource,scaled-combined);
            Record(rows,ManorLedgerSource.Nobility,resource,finished-scaled);
            return finished;
        }

        int BackyardOutput(Span<ManorLedgerLine> rows,ManorLedgerResource resource,int amount)
        {
            int result=NobleYield(amount);
            Record(rows,ManorLedgerSource.Backyard,resource,amount);
            Record(rows,ManorLedgerSource.Nobility,resource,result-amount);
            return result;
        }

        static int WithinStorage(int stock,int fraction,int amount)
        {
            int held=Precise(stock,fraction);
            return Math.Min(MaxValue*100,Math.Max(0,held+amount))-held;
        }

        void FinishLedger(Span<ManorLedgerLine> rows,in ManorDay balance)
        {
            if(rows.IsEmpty)return;
            var factors=balance.approvalFactors;
            Record(rows,ManorLedgerSource.Tax,ManorLedgerResource.Approval,factors.tax);
            Record(rows,ManorLedgerSource.Diet,ManorLedgerResource.Approval,factors.food);
            Record(rows,ManorLedgerSource.Tavern,ManorLedgerResource.Approval,factors.tavern);
            Record(rows,ManorLedgerSource.Winter,ManorLedgerResource.Approval,factors.heating);
            Record(rows,ManorLedgerSource.Policy,ManorLedgerResource.Approval,factors.decree);
            Record(rows,ManorLedgerSource.Chapel,ManorLedgerResource.Approval,factors.chapel);
            Record(rows,ManorLedgerSource.Settlement,ManorLedgerResource.Approval,factors.settled);
            Record(rows,ManorLedgerSource.Specialists,ManorLedgerResource.Approval,factors.herbalist+factors.minstrel);
            Record(rows,ManorLedgerSource.Market,ManorLedgerResource.Approval,factors.market);

            ref var limits=ref rows[(int)ManorLedgerSource.Limits];
            limits.coin+=WithinStorage(coin,remainder.coin,balance.coin)-balance.coin;
            limits.food+=WithinStorage(food,remainder.food,balance.food)-balance.food;
            limits.timber+=WithinStorage(timber,remainder.timber,balance.timber)-balance.timber;
            limits.arms+=WithinStorage(arms,remainder.arms,balance.arms)-balance.arms;
            limits.approval+=balance.approvalAfter-approval-factors.Total;
        }
    }
}
