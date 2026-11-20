using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        enum CivicKind { Bench, Barrel, Grain, Waypost, Banner, LanternPillar, Planter }

        readonly struct CivicDetail
        {
            public readonly CivicKind kind;
            public readonly EstateObstacle Footprint;
            public readonly int rank;

            public CivicDetail(CivicKind kind,float x,float z,float halfWidth,float halfDepth,int rank=0)
            {
                this.kind=kind;
                Footprint=new EstateObstacle(new Vector3(x,0,z),halfWidth,halfDepth);
                this.rank=rank;
            }
        }

        static readonly CivicDetail[] CivicDetails={
            new CivicDetail(CivicKind.Bench,-5.82f,1.46f,.73f,.24f),
            new CivicDetail(CivicKind.Barrel,6.11f,2.01f,.34f,.34f),
            new CivicDetail(CivicKind.Grain,-7.62f,-15.67f,.43f,.35f),
            new CivicDetail(CivicKind.Barrel,-3.65f,-16.01f,.34f,.34f),
            new CivicDetail(CivicKind.Waypost,19.9f,.95f,.29f,.24f),
            new CivicDetail(CivicKind.Waypost,23.98f,4.73f,.29f,.24f),
            new CivicDetail(CivicKind.Banner,-2.63f,1.57f,.23f,.24f,1),
            new CivicDetail(CivicKind.LanternPillar,-6.58f,2.07f,.25f,.25f,2),
            new CivicDetail(CivicKind.Banner,2.63f,1.57f,.23f,.24f,3),
            new CivicDetail(CivicKind.LanternPillar,-6.41f,-1.92f,.25f,.25f,3),
            new CivicDetail(CivicKind.Planter,-.48f,2.09f,.67f,.29f,4),
            new CivicDetail(CivicKind.Planter,-4.28f,1.83f,.67f,.29f,4),
        };
        static readonly Color[] NobleHeraldry={
            new Color(.52f,.55f,.46f),new Color(.57f,.35f,.20f),new Color(.70f,.22f,.16f),
            new Color(.67f,.19f,.31f),new Color(.54f,.27f,.79f)
        };
        static readonly Color CutLimestone=new Color(.58f,.57f,.48f), StoneCap=new Color(.76f,.71f,.54f);
        static readonly Color LanternGlass=new Color(1,.64f,.20f);
        static readonly Vector3[] HallLanterns={
            new Vector3(-1.04f,1.39f,-1.70f),new Vector3(1.04f,1.39f,-1.70f)
        };
        internal int NobleBoundaryCount {get;private set;}
        internal int NobleDetailCount {get;private set;}
        bool WeatherMakesLampsVisible => Weather==ManorWeather.Drizzle||Weather==ManorWeather.Flurry;

        void DetailBlock(Vector3 point,Vector3 size,Color tint) =>
            ManorStill(surveyPeg,point,Quaternion.identity,size,tint);

        void DrawNobleBoundary(int index,Vector3 center,Quaternion facing)
        {
            int rank=Manor.PlotRank(index);
            ManorStill(surveyPeg,center+Vector3.up*.024f,facing,new Vector3(3.54f,.027f,2.61f),
                builtSeason==3?new Color(.55f,.62f,.64f):new Color(.44f,.52f,.34f));
            DetailBlock(center+Vector3.up*.12f,new Vector3(.82f,.22f,.56f),CutLimestone);
            DetailBlock(center+Vector3.up*.45f,new Vector3(.40f,.55f,.32f),StoneCap);
            DetailBlock(center+new Vector3(0,.47f,-.18f),new Vector3(.28f,.22f,.047f),NobleHeraldry[rank]);
            DetailBlock(center+Vector3.up*.74f,new Vector3(.50f,.1f,.43f),Gold);
            NobleBoundaryCount++;
        }

        void BuildCivicDetails()
        {
            NobleBoundaryCount=NobleDetailCount=0;
            foreach(var light in HallLanterns)
            {
                DetailBlock(light,new Vector3(.17f,.22f,.18f),LanternGlass);
                DetailBlock(light+Vector3.up*.14f,new Vector3(.24f,.073f,.27f),Timber);
                DetailBlock(light-Vector3.up*.13f,new Vector3(.23f,.062f,.23f),Timber);
            }
            foreach(var detail in CivicDetails)
            {
                if(detail.rank>manor.nobility)continue;
                estateObstacles[estateObstacleCount++]=detail.Footprint;
                if(detail.rank>0)NobleDetailCount++;
                var at=detail.Footprint.center;
                switch(detail.kind)
                {
                    case CivicKind.Bench:
                        DetailBlock(at+Vector3.up*.37f,new Vector3(1.43f,.13f,.45f),StoneCap);
                        for(int side=-1;side<=1;side+=2)
                            DetailBlock(at+new Vector3(side*.49f,.15f,0),new Vector3(.23f,.31f,.32f),CutLimestone);
                        break;
                    case CivicKind.Barrel:
                        ManorStill(props["barrel"].WithoutShadows(),at,Quaternion.identity,Vector3.one*.71f,White);
                        break;
                    case CivicKind.Grain:
                        for(int side=-1;side<=1;side+=2)
                            ManorStill(basket.WithoutShadows(),at+new Vector3(side*.21f,.33f,side*.09f),
                                Quaternion.Euler(0,side*21,0),Vector3.one*.92f,White);
                        break;
                    case CivicKind.Waypost:
                        DetailBlock(at+Vector3.up*.10f,new Vector3(.54f,.18f,.44f),CutLimestone);
                        DetailBlock(at+Vector3.up*.64f,new Vector3(.12f,1.1f,.12f),Timber);
                        DetailBlock(at+new Vector3(.025f,1.08f,0),new Vector3(.48f,.16f,.075f),StoneCap);
                        break;
                    case CivicKind.Banner:
                        ManorStill(props["banner"].WithoutShadows(),at,Quaternion.Euler(0,at.x<0?-12:166,0),
                            Vector3.one*1.23f,NobleHeraldry[manor.nobility],.024f);
                        break;
                    case CivicKind.LanternPillar:
                        DetailBlock(at+Vector3.up*.12f,new Vector3(.48f,.24f,.48f),CutLimestone);
                        DetailBlock(at+Vector3.up*.50f,new Vector3(.28f,.72f,.29f),CutLimestone);
                        DetailBlock(at+Vector3.up*.92f,new Vector3(.39f,.13f,.39f),StoneCap);
                        DetailBlock(at+Vector3.up*1.055f,new Vector3(.18f,.16f,.18f),LanternGlass);
                        DetailBlock(at+Vector3.up*1.17f,new Vector3(.28f,.08f,.28f),Timber);
                        break;
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
