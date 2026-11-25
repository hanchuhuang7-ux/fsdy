using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        Vector3[] villageVerges;

        // The survey depends only on the island and its fixed right-of-way. Keep it through
        // building, camera and season changes. All meshes are added to the normal static chunks.
        void PlantVillageVerges()
        {
            villageVerges??=ChooseVillageVerges(walks,specialistRoutes);
            var flowers=Load("verge_flowers").WithoutShadows();
            var grass=Load("grass").WithoutShadows();
            var bush=Load("shrub").WithoutShadows();
            var tint=builtSeason==3?WinterPlot:White;
            for(int i=0;i<villageVerges.Length;i++)
            {
                var p=villageVerges[i];
                var turn=Quaternion.Euler(0,137.5f*i,0);
                bool thicket=i%5==0;
                ManorStill(thicket?bush:grass,p,turn,thicket?new Vector3(.57f,.44f,.45f):Vector3.one*.91f,tint,.022f);
                if(i%3!=0&&builtSeason!=3)
                    ManorStill(flowers,p+turn*new Vector3(.23f,.005f,.065f),turn,Vector3.one*.64f,White,.024f);
            }
        }

        void PlantOrchards()
        {
            var oak=Load("tree");var herb=Load("flowers").WithoutShadows();var grass=Load("grass");
            for(int grove=0;grove<3;grove++)for(int i=0;i<8;i++)
            {
                float x=(grove==0?-26:grove==1?9:-13)+(i%4-1.5f)*3.5f+.56f*Mathf.Sin(i*2.75f);
                FrontierMap.ShoreAt(ManorOrigin.x+x,out float south,out float north);
                float z=grove==2?south+2.4f+i/4*3.0f:north-2.8f-i/4*2.75f;
                var p=new Vector3(x,0,z);
                if(Mathf.Abs(x-DockOrigin.x)<3.6f||!ClearOrchardSite(p))continue;
                var turn=Quaternion.Euler(0,i*133+grove*37,0);float scale=.68f+Scatter((uint)(i+grove*73))*.38f;
                ManorStill(oak,p,turn,new Vector3(scale*1.12f,scale*.86f,scale),builtSeason==2?new Color(.96f,.79f,.53f):White,.012f);
                if(QualityLevel>0)
                {
                    ManorStill(grass,p+turn*new Vector3(.72f,0,.42f),turn,Vector3.one*1.36f,White,.033f);
                    if(builtSeason!=3)ManorStill(herb,p+turn*new Vector3(-.63f,0,.51f),turn,Vector3.one*.75f,White,.025f);
                }
            }
        }
        static bool ClearOrchardSite(Vector3 point)
        {
            for(int j=0;j<Manor.PlotCount;j++)if(PlotObstacle(j).Contains(point,1.4f))return false;
            return Mathf.Abs(point.z)>OuterLaneZ+2.6f;
        }

        static Vector3[] ChooseVillageVerges(IReadOnlyList<WalkRoute> roads,IReadOnlyList<WalkRoute> specialists)
        {
            var result=new List<Vector3>(72);
            var quay=Trace(new Vector3(DockOrigin.x,0,-OuterLaneZ),DockApproach);
            for(int row=0;row<4;row++)for(int i=0;i<30;i++)
            {
                float lane=row==0?-OuterLaneZ:row==1?-LaneZ:row==2?LaneZ:OuterLaneZ;
                float x=WestLaneX+1.4f+i*2.55f+.39f*Mathf.Sin(i*2.39f+row);
                float off=(i%2==0?-1:1)*(1.42f+.34f*Mathf.Cos(i*.83f+row));
                var p=new Vector3(x,0,lane+off);
                if(quay.NearVerge(p,1.12f)||!ClearSite(p))continue;
                result.Add(p);
            }
            return result.ToArray();

            bool ClearSite(Vector3 p)
            {
                const float margin=.67f;
                if(!FrontierMap.IsIsland(ManorOrigin.x+p.x,p.z-margin)||!FrontierMap.IsIsland(ManorOrigin.x+p.x,p.z+margin))return false;
                for(int j=0;j<Manor.PlotCount;j++)
                    if(PlotObstacle(j).Contains(p,margin)||j!=0&&YardObstacle(j).Contains(p,margin))return false;
                foreach(var item in CivicDetails)if(item.Footprint.Contains(p,margin))return false;
                foreach(var item in LandmarkObstacles)if(item.Contains(p,margin))return false;
                foreach(var post in RetinuePosts)if((p-post).sqrMagnitude<1.75f)return false;
                return ClearRoutes(roads,p)&&ClearRoutes(specialists,p);
            }
            static bool ClearRoutes(IReadOnlyList<WalkRoute> routes,Vector3 p)
            {
                for(int j=0;j<routes.Count;j++)
                    if(routes[j]!=null&&routes[j].NearVerge(p,1.12f))return false;
                return true;
            }
        }
    }
}
