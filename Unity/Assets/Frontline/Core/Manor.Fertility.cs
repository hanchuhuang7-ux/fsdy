using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        public const int MinFertility = 60, MaxFertility = 120, RichFertility = 110;
        public const int DefaultFertility = 100, RotationThreshold = 80;
        const int IntensiveWear = 12, RotationWear = 8, FallowRecovery = 25, PastureRecovery = 5;

        public static bool IsCrop(string kind) => kind == "farm" || kind == "irrigated_field";
        static bool ValidRotation(string rotation) => rotation == "auto" || rotation == "intensive" || rotation == "fallow";
        static bool IsResting(ManorPlot plot,int season) => plot.rotation == "fallow" ||
            plot.rotation == "auto" && (season == 3 || plot.fertility < RotationThreshold);

        public bool IsRestingField(int index)
        {
            var plot = Plot(index);
            return IsCrop(plot.kind) && IsResting(plot,Season);
        }

        public void SetRotation(int index,string rotation)
        {
            var plot = Plot(index);
            if (!IsCrop(plot.kind) || !ValidRotation(rotation)) throw new InvalidOperationException("请选择农田与耕作方式");
            plot.rotation = rotation;
        }

        void AdvanceFertility(int endingSeason)
        {
            bool staffedPasture = false;
            foreach (var plot in slots)
                if (plot.kind == "pasture" && plot.workers > 0) { staffedPasture = true; break; }
            foreach (var plot in slots)
            {
                if (!IsCrop(plot.kind)) continue;
                int change = IsResting(plot,endingSeason) ? FallowRecovery :
                    plot.rotation == "intensive" ? -IntensiveWear : plot.workers > 0 ? -RotationWear : 0;
                if (staffedPasture && plot.rotation != "intensive") change += PastureRecovery;
                plot.fertility = Math.Max(MinFertility,Math.Min(MaxFertility,plot.fertility + change));
            }
        }

        static void ReadFertility(ManorPlot plot,Dictionary<string,object> fields,bool current)
        {
            bool crop = IsCrop(plot.kind);
            if (!current)
            {
                if (crop) { plot.fertility = DefaultFertility; plot.rotation = "auto"; }
                return;
            }
            plot.fertility = Number(fields["fertility"],crop ? MinFertility : 0,crop ? MaxFertility : 0);
            plot.rotation = fields["rotation"] as string;
            if (crop ? !ValidRotation(plot.rotation) : fields["rotation"] != null)
                throw new FormatException("农田轮作无效");
        }
    }
}
