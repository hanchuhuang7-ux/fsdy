namespace Frontline.Core
{
    public readonly struct ManorMerit
    {
        public readonly int ranks, sectors, milestones, specialists, armaments, expeditions;

        internal ManorMerit(int ranks,int sectors,int milestones,int specialists,int armaments,int expeditions)
        {
            this.ranks=ranks;this.sectors=sectors;this.milestones=milestones;
            this.specialists=specialists;this.armaments=armaments;this.expeditions=expeditions;
        }

        public static int Total => Manor.NobleRanks.Count+Manor.Sectors.Count+Manor.Milestones.Count+
            Manor.SpecialistIds.Length+Manor.HeroIds.Length+Manor.Route.Length;
        public int Completed => ranks+sectors+milestones+specialists+armaments+expeditions;
        public int Percent => (Completed*100+Total/2)/Total;
        public string Title => Completed==Total?"黄金公爵":Completed*3>=2*Total?"铁血统领":
            Completed*3>=Total?"磐石领主":"边疆新贵";
    }

    public sealed partial class Manor
    {
        public ManorMerit MeritOverview()
        {
            int mastery=0,forged=0;
            // Training persists when a prerequisite building is temporarily unavailable.
            foreach(string id in SpecialistIds)if(SpecialistRank(id)==SpecialistMaxLevel)mastery++;
            foreach(string id in HeroIds)if(HeroArmament(id)==MaxHeroArmament)forged++;
            return new ManorMerit(nobility+1,ClaimedSectorCount,CountFlags(milestoneClaims),mastery,forged,cleared);
        }
    }
}
