namespace Frontline.Core
{
    public readonly struct CaravanOffer
    {
        public readonly string title, resource;
        // A signed buy adjustment is a discount. A signed sell adjustment is a premium.
        public readonly int buyPercent, sellPercent;

        internal CaravanOffer(string title,string resource,int buyPercent,int sellPercent)
        {
            this.title=title; this.resource=resource;
            this.buyPercent=buyPercent; this.sellPercent=sellPercent;
        }
    }

    public sealed partial class Manor
    {
        static readonly CaravanOffer[] CaravanOffers = {
            new CaravanOffer("筑城商队","timber",0,20),
            new CaravanOffer("军械商队","arms",0,14),
            new CaravanOffer("丰收商队","food",-8,0)
        };
        static readonly int[] SeasonalPreference={0,1,2,1};
        static readonly int[] AlternateOffer={1,2,0,0};

        public CaravanOffer SeasonalCaravan
        {
            get
            {
                int seasonNumber=(day-1)/SeasonDays;
                uint seed=unchecked((uint)seasonNumber*2246822519u+3266489917u);
                seed^=seed>>16;
                return CaravanOffers[(seed&3)==0?AlternateOffer[Season]:SeasonalPreference[Season]];
            }
        }
    }
}
