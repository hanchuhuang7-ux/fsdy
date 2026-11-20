using System;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        public const string Bakery="bakery", Weaver="weaver", Mason="mason", Minstrel="minstrel";
        public const int BreadInput=1, BreadOutput=3, WeaverCoin=2;
        public const int BreadCompanionPercent=12, PastureWeavePercent=10, WeaverHeatCap=2;

        static bool IsCraft(string kind) => kind==Bakery||kind==Weaver;
        static int MasonDiscount(int rank) => rank*4;
        static int MasonTimberBonus(int rank) => rank*3;
        static int MasonryCost(int price,int rank) => rank==0?price:Math.Max(1,price*(100-MasonDiscount(rank))/100);

        void BakeBread(ReadOnlySpan<int> bakeries,int grainAvailable,int approval,ref ManorDay balance)
        {
            // Each bakery draws from the remaining primary grain, so today's bread cannot feed another oven.
            foreach(int index in bakeries)
            {
                var plot=slots[index];
                int working=Math.Min(plot.workers,Math.Max(0,grainAvailable)/(BreadInput*100));
                if(working==0)break;
                int consumed=working*BreadInput*100;
                grainAvailable-=consumed;
                balance.bakeryGrain+=consumed;
                balance.food+=NobleYield(ScaleYield(Yield(BreadOutput,plot.level)*working,approval,
                    synergyPercent:100+balance.synergy.breadPercent))-consumed;
            }
        }
    }
}
