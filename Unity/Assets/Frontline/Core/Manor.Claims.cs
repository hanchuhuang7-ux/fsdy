using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    [Flags]
    public enum ManorTodo { None=0, Dispatch=1, Development=2, Milestone=4, Claim=8 }

    public readonly struct ManorSector
    {
        public readonly string id, name, resource;
        public readonly int influence, timber, daily;

        internal ManorSector(string id,string name,string resource,int influence,int timber,int daily)
        {
            this.id=id; this.name=name; this.resource=resource;
            this.influence=influence; this.timber=timber; this.daily=daily;
        }
    }

    public sealed partial class Manor
    {
        const int WestOutpost=0, NorthGlade=1, EastRidge=2;
        public const int SectorMask=(1<<3)-1;
        public int clearedSectors, claimedSectors;

        public static IReadOnlyList<ManorSector> Sectors {get;}=Array.AsReadOnly(new[]{
            new ManorSector("west_outpost","西桥前哨","建材",35,16,150),
            new ManorSector("north_glade","北林猎场","粮食",45,20,200),
            new ManorSector("east_ridge","东岭铁脉","军械",55,24,100)
        });

        public bool ClearedSector(int index) => (uint)index<Sectors.Count&&(clearedSectors&(1<<index))!=0;
        public bool ClaimedSector(int index) => (uint)index<Sectors.Count&&(claimedSectors&(1<<index))!=0;

        public string SectorClaimBlock(int index)
        {
            if((uint)index>=Sectors.Count)return "未知外域疆域";
            if(ClaimedSector(index))return "已宣称";
            if(!ClearedSector(index))return "待清剿并结算";
            var sector=Sectors[index];
            return influence<sector.influence||timber<sector.timber?"威望或建材不足":null;
        }

        public void ClaimSector(int index)
        {
            string blocked=SectorClaimBlock(index);
            if(blocked!=null)throw new InvalidOperationException(blocked);
            var sector=Sectors[index];
            influence-=sector.influence;
            timber-=sector.timber;
            claimedSectors|=1<<index;
            Remember("claim",index);
        }

        public ManorTodo Todos
        {
            get
            {
                ManorTodo todo=ManorTodo.None;
                if(CanResolveDispatch)todo|=ManorTodo.Dispatch;
                if(DevelopmentPointsAvailable>0)todo|=ManorTodo.Development;
                if(ReadyMilestones!=0)todo|=ManorTodo.Milestone;
                int eligible=clearedSectors&~claimedSectors;
                for(int i=0;i<Sectors.Count;i++)
                    if((eligible&(1<<i))!=0&&SectorClaimBlock(i)==null)return todo|ManorTodo.Claim;
                return todo;
            }
        }

        public bool CanResolveDispatch => eventDay!=0&&
            (EventChoiceBlock(eventDay,0)==null||EventChoiceBlock(eventDay,1)==null);

        bool AddSectorSupplies(ref ManorDay balance)
        {
            if(ClaimedSector(WestOutpost))balance.timber+=Sectors[WestOutpost].daily;
            if(ClaimedSector(EastRidge))balance.arms+=Sectors[EastRidge].daily;
            if(!ClaimedSector(NorthGlade))return false;
            balance.food+=Sectors[NorthGlade].daily;
            return true;
        }

        static int PatrolClearances(Sortie sortie,IReadOnlyList<UnitReport> units,out int defeated)
        {
            int seen=0, cleared=SectorMask, firstEnemy=sortie.roster.Length+1;
            var roster=FrontierMap.Roster(sortie.FrontierTier);
            defeated=0;
            foreach(var unit in units)
            {
                if(unit.team!="blue")continue;
                int index=unit.id-firstEnemy;
                if((uint)index>=roster.Count||(seen&(1<<index))!=0||unit.type!=roster[index])
                    throw new InvalidOperationException("战报与边疆敌情不一致");
                seen|=1<<index;
                if(unit.alive)cleared&=~(1<<(index/FrontierMap.GroupSize));
                else defeated++;
            }
            if(seen!=(1<<roster.Count)-1)throw new InvalidOperationException("战报与边疆敌情不一致");
            return cleared;
        }

        void ReadClaims(Dictionary<string,object> root)
        {
            clearedSectors=Number(root["clearedSectors"],0,SectorMask);
            claimedSectors=Number(root["claimedSectors"],0,SectorMask);
            if((claimedSectors&~clearedSectors)!=0)throw new FormatException("疆域须先清剿后宣称");
        }
    }
}
