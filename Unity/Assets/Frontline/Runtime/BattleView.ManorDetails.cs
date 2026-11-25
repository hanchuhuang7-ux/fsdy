using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        enum CivicKind { Bench, Barrel, Grain, Waypost, Banner, LanternPillar, Planter, Cart, Wall, Flowers }

        readonly struct CivicDetail
        {
            public readonly CivicKind kind;
            public readonly EstateObstacle Footprint;
            public readonly int rank;

            public CivicDetail(CivicKind kind,float x,float z,float halfWidth,float halfDepth,int rank=0)
            {
                this.kind=kind;
                Footprint=new EstateObstacle(rank==0?new Vector3(x*Manor.TownScaleX/1.27f,0,z*Manor.TownScaleZ/1.40f):new Vector3(x,0,z),halfWidth,halfDepth);
                this.rank=rank;
            }
        }

        static readonly CivicDetail[] CivicDetails={
            new CivicDetail(CivicKind.Cart,14.3f,2.46f,.43f,.87f),
            new CivicDetail(CivicKind.Cart,-3.20f,-23.25f,.43f,.87f),
            new CivicDetail(CivicKind.Wall,18.15f,3.24f,.83f,.23f),
            new CivicDetail(CivicKind.Wall,-13.53f,2.48f,.83f,.23f),
            new CivicDetail(CivicKind.Wall,11.25f,15.23f,.83f,.23f),
            new CivicDetail(CivicKind.Flowers,9.40f,3.53f,.53f,.43f),
            new CivicDetail(CivicKind.Flowers,-8.32f,3.51f,.53f,.43f),
            new CivicDetail(CivicKind.Flowers,14.23f,-14.07f,.53f,.43f),
            new CivicDetail(CivicKind.Bench,-7.36f,1.30f,.73f,.24f),
            new CivicDetail(CivicKind.Barrel,7.96f,3.02f,.34f,.34f),
            new CivicDetail(CivicKind.Grain,-7.68f,-24.7f,.43f,.35f),
            new CivicDetail(CivicKind.Barrel,-3.61f,-24.5f,.34f,.34f),
            new CivicDetail(CivicKind.Waypost,31.3f,-1.65f,.29f,.24f),
            new CivicDetail(CivicKind.Waypost,35.3f,8.10f,.29f,.24f),
            new CivicDetail(CivicKind.Banner,-6.24f,-3.21f,.23f,.24f,1),
            new CivicDetail(CivicKind.LanternPillar,-7.95f,2.79f,.25f,.25f,2),
            new CivicDetail(CivicKind.Banner,6.25f,-3.19f,.23f,.24f,3),
            new CivicDetail(CivicKind.LanternPillar,-8.02f,-2.72f,.25f,.25f,3),
            new CivicDetail(CivicKind.Planter,-1.49f,4.31f,.67f,.29f,4),
            new CivicDetail(CivicKind.Planter,1.49f,4.31f,.67f,.29f,4),
        };
        static readonly Color[] NobleHeraldry={
            new Color(.52f,.55f,.46f),new Color(.57f,.35f,.20f),new Color(.70f,.22f,.16f),
            new Color(.67f,.19f,.31f),new Color(.54f,.27f,.79f)
        };
        static readonly Color CutLimestone=new Color(.58f,.57f,.48f), StoneCap=new Color(.76f,.71f,.54f);
        static readonly Vector3[] HallLanterns={
            new Vector3(-1.33f,1.41f,-3.71f),new Vector3(1.33f,1.41f,-3.71f)
        };
        internal int NobleBoundaryCount {get;private set;}
        internal int NobleDetailCount {get;private set;}
        bool WeatherMakesLampsVisible => Weather==ManorWeather.Drizzle||Weather==ManorWeather.Flurry;

        void DetailBlock(Vector3 point,Vector3 size,Color tint) =>
            ManorStill(surveyPeg,point,Quaternion.identity,size,tint);

        void DrawNobleBoundary(int index,Vector3 center,Quaternion facing)
        {
            int rank=Manor.PlotRank(index);
            MarkUnbuilt(center,facing);
            DetailBlock(center+Vector3.up*.12f,new Vector3(.82f,.22f,.56f),CutLimestone);
            DetailBlock(center+Vector3.up*.45f,new Vector3(.40f,.55f,.32f),StoneCap);
            DetailBlock(center+new Vector3(0,.47f,-.18f),new Vector3(.28f,.22f,.047f),NobleHeraldry[rank]);
            DetailBlock(center+Vector3.up*.74f,new Vector3(.50f,.1f,.43f),Gold);
            NobleBoundaryCount++;
        }

        void BuildCivicDetails()
        {
            NobleBoundaryCount=NobleDetailCount=0;
            foreach(var detail in CivicDetails)
            {
                if(detail.rank>manor.nobility)continue;
                estateObstacles[estateObstacleCount++]=detail.Footprint;
                if(detail.rank>0)NobleDetailCount++;
                var at=detail.Footprint.center;
                switch(detail.kind)
                {
                    case CivicKind.Cart:
                        ManorStill(Load("market_cart").WithoutShadows(),at,Quaternion.identity,Vector3.one,White);break;
                    case CivicKind.Wall:
                        ManorStill(Load("verge_wall").WithoutShadows(),at,Quaternion.identity,Vector3.one,White);break;
                    case CivicKind.Flowers:
                        ManorStill(Load("verge_flowers").WithoutShadows(),at,Quaternion.identity,Vector3.one,White,.032f);break;
                    case CivicKind.Bench:
                        ManorStill(Load("manor_bench").WithoutShadows(),at,Quaternion.identity,Vector3.one,White);break;
                    case CivicKind.Barrel:
                        ManorStill(props["barrel"].WithoutShadows(),at,Quaternion.identity,Vector3.one*.71f,White);
                        break;
                    case CivicKind.Grain:
                        for(int side=-1;side<=1;side+=2)
                            ManorStill(basket.WithoutShadows(),at+new Vector3(side*.21f,.33f,side*.09f),
                                Quaternion.Euler(0,side*21,0),Vector3.one*.92f,White);
                        break;
                    case CivicKind.Waypost:
                        ManorStill(Load("waypost").WithoutShadows(),at,Quaternion.identity,Vector3.one,White);break;
                    case CivicKind.Banner:
                        ManorStill(props["banner"].WithoutShadows(),at,Quaternion.Euler(0,at.x<0?-12:166,0),
                            Vector3.one*1.23f,NobleHeraldry[manor.nobility],.024f);
                        break;
                    case CivicKind.LanternPillar:
                        ManorStill(Load("manor_pillar").WithoutShadows(),at,Quaternion.identity,Vector3.one,White);break;
                    case CivicKind.Planter:
                        DetailBlock(at+Vector3.up*.12f,new Vector3(1.28f,.22f,.55f),CutLimestone);
                        for(int side=-1;side<=1;side+=2)
                        {
                            ManorStill(Load("shrub").WithoutShadows(),at+new Vector3(side*.30f,.23f,0),
                                Quaternion.identity,new Vector3(.44f,.34f,.38f),White);
                            DetailBlock(at+new Vector3(side*.31f,.53f,0),new Vector3(.16f,.065f,.14f),NobleHeraldry[manor.nobility]);
                        }
                        break;
                }
            }
        }

        void DrawWetWeatherLights()
        {
            if(!WeatherMakesLampsVisible)return;
            for(int i=0;i<HallLanterns.Length;i++)
            {
                var point=ManorOrigin+HallLanterns[i]-Vector3.up*.13f;
                if(InView(point,1))
                    Add(forgeFlame,Pose(point,Quaternion.identity,new Vector3(.47f,.32f,.38f)),new Color(1,.58f,.19f,.38f));
            }
            for(int side=-1;side<=1;side+=2)
            {
                var point=ManorOrigin+new Vector3(BridgeLocalX-1.73f,.64f,side*4.69f);
                if(!InView(point,1.4f))continue;
                float pulse=.43f+.055f*Mathf.Sin(manorTime*5.8f+side);
                Add(forgeFlame,Pose(point,Quaternion.identity,new Vector3(.78f,.64f,.74f)),new Color(1,.59f,.17f,pulse));
            }
        }
    }
}
