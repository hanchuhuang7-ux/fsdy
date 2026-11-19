using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        public int frontierTier=1, guideStep;
        public bool guideDismissed;

        public int MaxFrontierTier => frontierSwept?FrontierMap.MaxTier:1;
        public void SelectFrontierTier(int tier)
        {
            if(tier<1||tier>MaxFrontierTier)throw new InvalidOperationException("先在一次巡边中击退全部 15 名守军");
            frontierTier=tier;
        }

        public bool GuideActive => !guideDismissed&&guideStep<3;
        public bool CompleteGuideStep(int step)
        {
            if(!GuideActive||guideStep!=step)return false;
            if(step==0&&!HasStaffedCrop())return false;
            guideStep++;
            return true;
        }

        bool HasStaffedCrop()
        {
            foreach(var plot in slots)
                if(plot.workers>0&&(plot.kind=="farm"||plot.kind=="irrigated_field"))return true;
            return false;
        }

        public bool DismissGuide()
        {
            if(!GuideActive)return false;
            guideDismissed=true;
            return true;
        }

        bool ValidProgression() => frontierTier>=1&&frontierTier<=MaxFrontierTier&&(uint)guideStep<=3;

        void ReadProgression(Dictionary<string,object> root)
        {
            frontierTier=Number(root["frontierTier"],1,MaxFrontierTier);
            guideStep=Number(root["guideStep"],0,3);
            if(!(root["guideDismissed"] is bool dismissed))throw new FormatException("庄园引导状态无效");
            guideDismissed=dismissed;
        }
    }
}
