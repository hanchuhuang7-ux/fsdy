using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        static readonly Vector3 WellOrigin=new Vector3(-5.02f,.046f,-.30f);
        static readonly Vector3 MarketNorth=new Vector3(5.44f,.046f,.91f), MarketSouth=new Vector3(5.50f,.046f,-1.37f);
        static readonly Vector3 DockOrigin=new Vector3(-5.45f,-.67f,-17.46f);
        static readonly Vector3 DockApproach=new Vector3(DockOrigin.x,0,-17.18f);
        static readonly Vector3 DockScale=new Vector3(1.32f,1,1.21f);
        static readonly Vector3 GatehouseOrigin=new Vector3(BridgeLocalX-1.44f,.021f,0);
        static readonly Vector3 CargoWest=new Vector3(DockOrigin.x-1.95f,.034f,-16.92f);
        static readonly Vector3 CargoEast=new Vector3(DockOrigin.x+1.98f,.032f,-17.06f);
        static readonly Vector3 YardOrigin=new Vector3(-1.51f,0,-1.63f);
        static readonly Color[] SpecialistTrim={Color.white,new Color(.72f,.86f,.97f),new Color(1f,.79f,.39f)};
        static readonly Color RetinueCloth=new Color(.33f,.40f,.58f);
        // The wider gap on the west flank keeps the south fields' access lane open.
        static readonly Vector3[] RetinuePosts={
            new Vector3(-2.50f,0,-3.77f),new Vector3(2.50f,0,-3.77f),
            new Vector3(-1.22f,0,-3.77f),new Vector3(3.76f,0,-3.77f),
            new Vector3(-4.89f,0,-3.77f),new Vector3(5.02f,0,-3.77f)
        };
        readonly MeshSet[] specialistMeshes=new MeshSet[4];
        readonly WalkRoute[] specialistRoutes=new WalkRoute[4];
        readonly int[] specialistLevels=new int[4];
        readonly Vector3[] specialistPoints=new Vector3[4];
        readonly bool[] heroBayClear=new bool[3];
        MeshSet specialistLeg;
        MeshSet retinueGuard;
        int manorGuardsAway;
        internal int VisibleRetinue {get;private set;}
        internal static Vector3 RetinuePost(int index) => ManorOrigin+RetinuePosts[index];
        internal int VisibleSpecialistMask {get;private set;}
        internal Vector3 SpecialistPoint(int role) => specialistPoints[role];

        readonly struct EstateObstacle
        {
            public readonly Vector3 center;
            public readonly float hx,hz;
            public EstateObstacle(Vector3 center,float hx,float hz)
            {
                this.center=center;this.hx=hx;this.hz=hz;
            }
            public bool Contains(Vector3 point,float margin=.23f) =>
                Mathf.Abs(point.x-center.x)<hx+margin&&Mathf.Abs(point.z-center.z)<hz+margin;
        }
        static readonly EstateObstacle[] LandmarkObstacles={
            new EstateObstacle(WellOrigin,.89f,.69f),
            new EstateObstacle(MarketNorth,1.04f,.71f),
            new EstateObstacle(MarketSouth,.83f,.58f),
            new EstateObstacle(GatehouseOrigin+new Vector3(0,0,-3.48f),.69f,.34f),
            new EstateObstacle(GatehouseOrigin+new Vector3(0,0,3.48f),.69f,.34f),
            new EstateObstacle(new Vector3(BridgeLocalX-1.73f,0,-4.69f),.42f,.42f),
            new EstateObstacle(new Vector3(BridgeLocalX-1.73f,0,4.69f),.42f,.42f),
            new EstateObstacle(CargoWest,.68f,.55f),
            new EstateObstacle(CargoEast,.68f,.55f)
        };
        readonly EstateObstacle[] estateObstacles=new EstateObstacle[Manor.PlotCount*2+LandmarkObstacles.Length+CivicDetails.Length];
        int estateObstacleCount;

        static EstateObstacle PlotObstacle(int index)
        {
            float hx=ManorPlotArt.HalfWidth(index==0),hz=ManorPlotArt.HalfDepth(index==0);
            return new EstateObstacle(Plot(index),WestPlot(index)?hz:hx,WestPlot(index)?hx:hz);
        }
        static EstateObstacle YardObstacle(int index)
        {
            var center=Plot(index)+Quaternion.Euler(0,FacadeYaw(index),0)*YardOrigin;
            return new EstateObstacle(center,WestPlot(index)?.26f:.29f,WestPlot(index)?.29f:.26f);
        }
        void AddPlotObstacle(int index) => estateObstacles[estateObstacleCount++]=PlotObstacle(index);

        void BuildEstateDetails()
        {
            foreach(var obstacle in LandmarkObstacles)estateObstacles[estateObstacleCount++]=obstacle;
            ManorStill(Load("manor_plaza").WithoutShadows(),Vector3.zero,Quaternion.identity,Vector3.one,White);
            ManorStill(Load("manor_well").WithoutShadows(),WellOrigin,Quaternion.identity,Vector3.one,White);
            var stall=Load("manor_market").WithoutShadows();
            ManorStill(stall,MarketNorth,Quaternion.identity,Vector3.one,new Color(.94f,.76f,.61f));
            ManorStill(stall,MarketSouth,Quaternion.identity,Vector3.one*.80f,new Color(.56f,.84f,.82f));
            ManorStill(Load("manor_dock").WithoutShadows(),DockOrigin,Quaternion.identity,DockScale,White);
            var cargo=Load("carry_crate").WithoutShadows();
            ManorStill(cargo,CargoWest,Quaternion.Euler(0,11,0),Vector3.one*2.15f,White);
            ManorStill(cargo,CargoWest+new Vector3(.22f,.45f,.04f),Quaternion.Euler(0,-14,0),Vector3.one*1.55f,White);
            ManorStill(cargo,CargoEast,Quaternion.Euler(0,-19,0),Vector3.one*1.89f,White);
            ManorStill(Load("manor_gatehouse"),GatehouseOrigin,Quaternion.identity,Vector3.one,White);
            var brazier=Load("brazier").WithoutShadows();
            for(int side=-1;side<=1;side+=2)
                ManorStill(brazier,new Vector3(BridgeLocalX-1.73f,.025f,side*4.69f),Quaternion.identity,Vector3.one*.73f,White);

            var fence=Load("manor_fence").WithoutShadows();
            var hedge=Load("shrub").WithoutShadows();
            for(int i=0;i<6;i++)
            {
                float x=-22.8f+i*8.55f, z=-14.7f+i*5.85f;
                ManorStill(fence,new Vector3(x,0,17.46f),Quaternion.Euler(0,i%2==0?3:-5,0),Vector3.one*.98f,White);
                ManorStill(fence,new Vector3(-27.38f,0,z),Quaternion.Euler(0,i%2==0?87:92,0),Vector3.one*.94f,White);
                if(Mathf.Abs(x-DockOrigin.x)>4.4f)
                    ManorStill(fence,new Vector3(x,0,-17.42f),Quaternion.Euler(0,i%2==0?-8:5,0),Vector3.one,White);
                ManorStill(hedge,new Vector3(x+2.46f,0,17.30f),Quaternion.Euler(0,i*51,0),
                    new Vector3(1.44f,.53f,.66f),new Color(.72f,.84f,.60f),.016f);
            }
            var reed=Load("reeds").WithoutShadows();
            for(int i=0;i<9;i++)
            {
                float x=-22.3f+i*5.04f, z=-15.65f+i*3.91f;
                ManorStill(reed,new Vector3(-27.95f,-.27f,z),Quaternion.Euler(0,i*47,0),Vector3.one*(.64f+i%3*.13f),White,.029f);
                if(i%3!=1&&Mathf.Abs(x-DockOrigin.x)>2.8f)
                    ManorStill(reed,new Vector3(x,-.35f,-17.83f),Quaternion.Euler(0,i*31,0),Vector3.one*.75f,White,.026f);
            }
            for(int i=0;i<specialistMeshes.Length;i++)
                specialistMeshes[i]=Load("specialist_"+Manor.SpecialistIds[i]).WithoutShadows();
            specialistLeg=leg.WithoutShadows();
        }

        void DrawUnclearedPlot(int index,Vector3 center,Quaternion facing)
        {
            Tile(center+Vector3.up*.019f,new Vector3(3.40f,.026f,2.48f),
                builtSeason==3?new Color(.63f,.69f,.69f):new Color(.47f,.50f,.31f),FacadeYaw(index));
            var shrub=Load("shrub").WithoutShadows();
            var logs=Load("manor_firewood").WithoutShadows();
            ManorStill(shrub,center+facing*new Vector3(-1.02f,.025f,.53f),facing,Vector3.one*.84f,White,.021f);
            ManorStill(shrub,center+facing*new Vector3(.83f,.025f,.37f),facing,Vector3.one*.69f,White,.021f);
            ManorStill(logs,center+facing*new Vector3(.38f,.046f,-.61f),facing*Quaternion.Euler(0,19,0),Vector3.one*1.93f,White);
        }

        void DecoratePlot(int index,string kind,Vector3 basePoint,Quaternion facing)
        {
            string asset=kind=="cottage"?"manor_laundry":kind=="smithy"?"manor_firewood":
                builtSeason==2&&IsFarm(kind)&&!manor.IsRestingField(index)?"manor_straw":null;
            if(asset!=null)
            {
                ManorStill(Load(asset).WithoutShadows(),basePoint+facing*YardOrigin,facing,Vector3.one*.76f,White);
                estateObstacles[estateObstacleCount++]=YardObstacle(index);
            }
            var backyard=manor.slots[index].backyard;
            if(backyard!=null)
                ManorStill(Load("backyard_"+backyard).WithoutShadows(),basePoint+facing*ManorPlotArt.BackyardOrigin,
                    facing,ManorPlotArt.BackyardScale,White);
            if(kind=="cottage"&&builtSeason==3)
                ManorStill(Load("manor_roof_snow").WithoutShadows(),basePoint,facing,Vector3.one,White);
        }

        static WalkRoute Trace(params Vector3[] points)
        {
            var route=new WalkRoute();
            for(int i=1;i<points.Length;i++)route.Segment(points[i-1],points[i]);
            return route;
        }

        static WalkRoute[] FixedSpecialistRoutes() => new[] {
            Trace(Door(0),new Vector3(-3.24f,0,-LaneZ),new Vector3(-3.24f,0,1.94f)),
            Trace(new Vector3(3.97f,0,1.26f),new Vector3(3.97f,0,-LaneZ),LaneMouth(Gate),new Vector3(BridgeLocalX-1.04f,0,-.48f)),
            Trace(new Vector3(-3.12f,0,1.38f),new Vector3(-3.12f,0,-LaneZ),Door(0)),
            Trace(new Vector3(BridgeLocalX-5.65f,0,-LaneZ),new Vector3(BridgeLocalX-2.90f,0,-2.0f),
                new Vector3(BridgeLocalX-.60f,0,-1.50f),new Vector3(BridgeLocalX-.60f,0,1.54f),
                new Vector3(BridgeLocalX-3.96f,0,1.50f))
        };

        void BuildSpecialistRoutes()
        {
            var routes=FixedSpecialistRoutes();
            Array.Copy(routes,specialistRoutes,routes.Length);
        }

        void DrawSpecialists()
        {
            VisibleSpecialistMask=0;
            for(int role=0;role<specialistLevels.Length;role++)
            {
                int rank=specialistLevels[role];
                if(rank==0)continue;
                var route=specialistRoutes[role];
                if(role==2)
                    for(int i=0;i<homeCount;i++)
                    {
                        int home=homes[i];
                        if(manor.slots[home].kind!="cottage")continue;
                        route=walks[home*(Gate+1)]; break;
                    }
                float speed=.87f+role*.07f;
                float phase=Mathf.Repeat(manorTime*speed+role*3.17f,route.total*2);
                bool returning=phase>route.total;
                float along=returning?route.total*2-phase:phase;
                route.Sample(along,out var pos,out var direction);
                if(returning)direction=-direction;
                specialistPoints[role]=ManorOrigin+pos;
                if(!InView(specialistPoints[role],2.4f)||!TryPlaceWalker(pos,direction,out pos))continue;
                specialistPoints[role]=ManorOrigin+pos;
                VisibleSpecialistMask|=1<<role;
                float gait=Mathf.Sin(along*4.8f+role);
                var rotation=Quaternion.Euler(0,LocalHeading(SpecialistFirst+role,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg),0);
                var root=Pose(specialistPoints[role]+Vector3.up*(.735f+.028f*Mathf.Abs(gait)),rotation,Vector3.one*.96f);
                Add(specialistMeshes[role],root,SpecialistTrim[rank-1]);
                ContactAt(specialistPoints[role],.90f);
                if(lowActors)continue;
                for(int side=-1;side<=1;side+=2)
                    Add(specialistLeg,root*Pose(new Vector3(side*.124f,-.50f,0),Quaternion.Euler(gait*side*25,0,0),Vector3.one),White);
            }
        }

        void DrawRetinue()
        {
            VisibleRetinue=0;
            int ready=manor.RetinueReady;
            for(int i=manorGuardsAway;i<ready;i++)
            {
                var point=RetinuePost(i);
                if(!InView(point,2.4f))continue;
                var facing=Quaternion.Euler(0,i%2==0?174:186,0);
                retinueGuard??=Load("retinue_guard");
                Add(retinueGuard,Pose(point,facing,Vector3.one),RetinueCloth);
                ContactAt(point,.92f);
                VisibleRetinue++;
            }
        }

        // These are the same routes and footprint reservations used by the visible population.
        internal static object CheckEstateRoutes()
        {
            var blockers=new List<EstateObstacle>(LandmarkObstacles);
            for(int index=0;index<Manor.PlotCount;index++)
            {
                var pad=PlotObstacle(index);
                float width=index==0?2.28f:1.925f,depth=index==0?1.77f:1.485f;
                if(WestPlot(index)){float swap=width;width=depth;depth=swap;}
                if(Mathf.Abs(pad.center.x)+width>Manor.PlateauHalfWidth-.25f||
                    Mathf.Abs(pad.center.z)+depth>Manor.PlateauHalfDepth-.25f)
                    throw new InvalidOperationException("Manor plot outside the shelf: "+index);
                for(int other=0;other<index;other++)
                {
                    var neighbour=PlotObstacle(other);
                    if(Mathf.Abs(neighbour.center.x-pad.center.x)<pad.hx+neighbour.hx+.40f&&
                        Mathf.Abs(neighbour.center.z-pad.center.z)<pad.hz+neighbour.hz+.40f)
                        throw new InvalidOperationException("Manor plot access overlaps: "+index+" and "+other);
                }
                blockers.Add(pad);
                if(index!=0)blockers.Add(YardObstacle(index));
            }
            foreach(var detail in CivicDetails)
            {
                var obstacle=detail.Footprint;
                for(int x=-1;x<=1;x+=2)
                    for(int z=-1;z<=1;z+=2)
                        if(!FrontierMap.HasGround(ManorOrigin.x+obstacle.center.x+x*obstacle.hx,obstacle.center.z+z*obstacle.hz))
                            throw new InvalidOperationException("Civic detail leaves the shelf: "+obstacle.center);
                foreach(var blocker in blockers)
                    if(Mathf.Abs(obstacle.center.x-blocker.center.x)<obstacle.hx+blocker.hx&&
                        Mathf.Abs(obstacle.center.z-blocker.center.z)<obstacle.hz+blocker.hz)
                        throw new InvalidOperationException("Civic detail overlaps a plot or landmark: "+obstacle.center+" near "+blocker.center);
                blockers.Add(obstacle);
            }
            const float guardRadius=ManorPlotArt.RetinuePostRadius;
            foreach(var post in RetinuePosts)
            {
                if(!FrontierMap.HasGround(ManorOrigin.x+post.x,post.z))
                    throw new InvalidOperationException("Retinue post leaves the shelf: "+post);
                foreach(var blocker in blockers)
                    if(blocker.Contains(post,guardRadius))
                        throw new InvalidOperationException("Retinue post intersects the estate: "+post+" near "+blocker.center);
            }
            for(int i=0;i<RetinuePosts.Length;i++)
                for(int j=0;j<i;j++)
                    if((RetinuePosts[i]-RetinuePosts[j]).sqrMagnitude<4*guardRadius*guardRadius)
                        throw new InvalidOperationException("Retinue posts overlap");
            foreach(var post in RetinuePosts)blockers.Add(new EstateObstacle(post,guardRadius,guardRadius));
            int checkedPoints=0,checkedRoutes=0;
            void Check(WalkRoute route,string label)
            {
                int steps=Mathf.CeilToInt(route.total/.22f);
                for(int step=0;step<=steps;step++)
                {
                    route.Sample(route.total*step/steps,out var p,out _);
                    if(!FrontierMap.HasGround(ManorOrigin.x+p.x,p.z))
                        throw new InvalidOperationException("Manor walk leaves the shelf: "+label+" "+p);
                    foreach(var blocker in blockers)
                        if(blocker.Contains(p,.21f))throw new InvalidOperationException("Manor route contacts a plot or landmark: "+label+" "+p+" near "+blocker.center);
                    checkedPoints++;
                }
                checkedRoutes++;
            }
            for(int first=0;first<=Gate;first++)
                for(int last=0;last<=Gate;last++)
                    if(first!=last)Check(WalkBetween(first,last),first+"→"+last);
            var specialists=FixedSpecialistRoutes();
            for(int i=0;i<specialists.Length;i++)Check(specialists[i],Manor.SpecialistIds[i]);
            Check(Trace(new Vector3(OuterConnector(-OuterLaneZ),0,-OuterLaneZ),
                new Vector3(DockOrigin.x,0,-OuterLaneZ),DockApproach),"south quay");
            Check(Trace(new Vector3(WestLaneX,0,-OuterLaneZ),new Vector3(WestLaneX,0,OuterLaneZ)),"west lane");
            for(int side=-1;side<=1;side+=2)
                Check(Trace(new Vector3(WestLaneX,0,side*OuterLaneZ),new Vector3(OuterLaneEast,0,side*OuterLaneZ)),
                    side>0?"north fields":"south workshops");
            return new {checkedRoutes,checkedPoints,retinuePosts=RetinuePosts.Length,outerPlots=Manor.FirstNoblePlot-Manor.FirstOuterPlot,
                noblePlots=Manor.PlotCount-Manor.FirstNoblePlot,civicDetails=CivicDetails.Length};
        }
    }
}
