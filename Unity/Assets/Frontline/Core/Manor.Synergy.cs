using System;

namespace Frontline.Core
{
    public struct ManorSynergy
    {
        public int granaryLevel, grainPercent, cropPercent, forgePercent, settledApproval, chapelApproval, chapelRecoveryPercent;
        public int breadPercent, weavePercent, winterClothing;
    }

    public sealed partial class Manor
    {
        public const int CropCompanionPercent=8, TimberSmithyPercent=10, ProsperousMarketApproval=1;
        static int GranaryPercent(int level) => level==0?0:6+2*level;
        static int ChapelApproval(int level,bool staffed) => 2+level+(staffed?1:0);

        public ManorSynergy Synergies() => ReadSynergies(out _);

        ManorSynergy ReadSynergies(out bool hasClinic)
        {
            int granary=0,cottages=0,chapelMorale=0,chapelRecovery=0,looms=0;
            bool crops=false,pasture=false,logging=false,smith=false,social=false,bakery=false;
            foreach(var plot in slots)
            {
                bool staffed=plot.workers>0;
                switch(plot.kind)
                {
                    case "farm":case "irrigated_field":crops|=staffed;break;
                    case "pasture":pasture|=staffed;break;
                    case "timber_camp":logging|=staffed;break;
                    case "smithy":smith|=staffed;break;
                    case Bakery:bakery|=staffed;break;
                    case Weaver:if(staffed)looms++;break;
                    case "granary":if(staffed)granary=Math.Max(granary,plot.level);break;
                    case "cottage":cottages+=plot.level;break;
                    case "tavern":social=true;break;
                    case "chapel":
                        social=true;
                        chapelMorale=Math.Max(chapelMorale,ChapelApproval(plot.level,staffed));
                        chapelRecovery=Math.Max(chapelRecovery,staffed?3+2*plot.level:3);
                        break;
                }
            }
            hasClinic=social;
            return new ManorSynergy {
                granaryLevel=granary,grainPercent=GranaryPercent(granary),
                cropPercent=crops&&(pasture||granary>0)?CropCompanionPercent:0,
                breadPercent=bakery&&(crops||granary>0)?BreadCompanionPercent:0,
                weavePercent=looms>0&&pasture?PastureWeavePercent:0,
                winterClothing=Math.Min(WeaverHeatCap,looms),
                forgePercent=logging&&smith?TimberSmithyPercent:0,settledApproval=social&&cottages>=3?1:0,
                chapelApproval=chapelMorale,chapelRecoveryPercent=chapelRecovery
            };
        }
    }
}
