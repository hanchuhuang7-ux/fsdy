using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        static readonly Vector3 WellOrigin=new Vector3(-7.14f,.046f,-.56f);
        static readonly Vector3 MarketNorth=new Vector3(7.30f,.046f,1.76f),MarketSouth=new Vector3(7.35f,.046f,-2.15f);
        static readonly Vector3 DockOrigin=DockSite();
        static readonly Vector3 DockApproach=new Vector3(DockOrigin.x,0,DockOrigin.z+.28f);
        static readonly Vector3 DockScale=new Vector3(1.32f,1,1.21f);
        static readonly Vector3 GatehouseOrigin=new Vector3(BridgeLocalX-1.44f,.021f,0);
        static readonly Vector3 CargoWest=new Vector3(DockOrigin.x-1.95f,.034f,DockOrigin.z+.54f);
        static readonly Vector3 CargoEast=new Vector3(DockOrigin.x+1.98f,.032f,DockOrigin.z+.40f);
        static readonly Vector3 YardOrigin=new Vector3(-1.51f,0,-1.48f);
        static readonly Color[] SpecialistTrim={Color.white,new Color(.72f,.86f,.97f),new Color(1f,.79f,.39f)};
        static readonly Color RetinueCloth=new Color(.33f,.40f,.58f);
        static readonly Vector3[] RetinuePosts={
            new Vector3(-1.73f,0,-6.23f),new Vector3(1.73f,0,-6.23f),
            new Vector3(-3.24f,0,-6.23f),new Vector3(3.24f,0,-6.23f),
            new Vector3(-4.75f,0,-6.23f),new Vector3(4.75f,0,-6.23f)
        };
        readonly WalkRoute[] specialistRoutes=new WalkRoute[Manor.SpecialistIds.Length];
        readonly int[] specialistLevels=new int[Manor.SpecialistIds.Length];
        readonly Vector3[] specialistPoints=new Vector3[Manor.SpecialistIds.Length];
        readonly bool[] heroBayClear=new bool[3];
        int manorGuardsAway;
        internal int VisibleRetinue {get;private set;}
        internal static Vector3 RetinuePost(int index) => ManorOrigin+RetinuePosts[index];
        internal static Vector3 QuayPoint => ManorOrigin+DockOrigin+new Vector3(1.3f*DockScale.x,.12f,-2.27f*DockScale.z);
        internal int VisibleSpecialistMask {get;private set;}
        internal Vector3 SpecialistPoint(int role) => specialistPoints[role];
        internal float SpecialistFacing(int role) => localHeading[SpecialistFirst+role];

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

        static Vector3 DockSite()
        {
            const float quayX=-5.45f;
            FrontierMap.ShoreAt(FrontierMap.ManorX+quayX,out float south,out _);
            return new Vector3(quayX,-.67f,south+.54f);
        }

        static EstateObstacle PlotObstacle(int index)
        {
            float hx=index==0?FrontierMap.CastleHalfWidth:ManorPlotArt.BuiltPadHalfWidth;
            float hz=index==0?FrontierMap.CastleHalfDepth:ManorPlotArt.BuiltPadHalfDepth;
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
            ManorStill(Load("manor_plaza").WithoutShadows(),Vector3.zero,Quaternion.identity,new Vector3(1.46f,1,1.44f),White);
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

            var hedge=Load("shrub").WithoutShadows();
            var reed=Load("reeds").WithoutShadows();
            for(int i=0;i<13;i++)
            {
                float x=(i/12f*2-1)*(Manor.PlateauHalfWidth-5.2f);
                FrontierMap.ShoreAt(ManorOrigin.x+x,out float south,out float north);
                var rotation=Quaternion.Euler(0,i*47,0);
                ManorStill(hedge,new Vector3(x,0,north-.63f),rotation,new Vector3(1.17f,.43f,.63f),White,.016f);
                if(Mathf.Abs(x-DockOrigin.x)>3.8f)
                    ManorStill(reed,new Vector3(x,-.35f,south-.17f),rotation,Vector3.one*.75f,White,.026f);
            }
        }

        void DrawUnclearedPlot(int index,Vector3 center,Quaternion facing)
        {
            MarkUnbuilt(center,facing);
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
            var scale=ManorPlotArt.BuildingScale(kind);
            if(backyard!=null)
                ManorStill(Load("backyard_"+backyard).WithoutShadows(),basePoint+facing*Vector3.Scale(ManorPlotArt.BackyardOrigin,scale),
                    facing,Vector3.Scale(ManorPlotArt.BackyardScale,scale),White);
        }

        static WalkRoute Trace(params Vector3[] points)
        {
            var route=new WalkRoute();
            for(int i=1;i<points.Length;i++)route.Segment(points[i-1],points[i]);
            return route;
        }

        static WalkRoute[] FixedSpecialistRoutes() => new[] {
            Trace(Door(0),LaneMouth(0),new Vector3(-CrossX,0,-LaneZ),new Vector3(-CrossX,0,2.35f)),
            Trace(new Vector3(CrossX,0,1.84f),new Vector3(CrossX,0,-LaneZ),LaneMouth(Gate),new Vector3(BridgeLocalX-1.04f,0,-.48f)),
            Trace(new Vector3(-CrossX,0,1.45f),new Vector3(-CrossX,0,-LaneZ),LaneMouth(0),Door(0)),
            Trace(new Vector3(BridgeLocalX-5.65f,0,-LaneZ),new Vector3(BridgeLocalX-2.90f,0,-2.0f),
                new Vector3(BridgeLocalX-.60f,0,-1.50f),new Vector3(BridgeLocalX-.60f,0,1.54f),
                new Vector3(BridgeLocalX-3.96f,0,1.50f)),
            WalkBetween(Manor.LegacyNoblePlotCount+1,5),
            Trace(new Vector3(-CrossX,0,1.38f),new Vector3(-CrossX,0,-LaneZ),new Vector3(CrossX,0,-LaneZ),
                new Vector3(CrossX,0,LaneZ),LaneMouth(3),Door(3))
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
                        route=walks[home*RouteCount]; break;
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
                float pace=speed*Ease(Mathf.Min(along,route.total-along)/.52f);
                float heading=LocalHeading(SpecialistFirst+role,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
                ContactAt(specialistPoints[role],.90f);
                QueuePose(new PoseInput {artist=true,town=TownRole.Specialist,survey=true,id=SpecialistFirst+role,
                    position=specialistPoints[role]+Vector3.up*(.83f*.79f),angle=heading*Mathf.Deg2Rad,scale=.83f,
                    gait=phase/.83f,speed=pace,maxSpeed=2.8f,direction=1,hp=1,color=SpecialistTrim[rank-1]},
                    new PoseDraw{costume=collection[Manor.SpecialistIds[role]]});
            }
        }

        readonly WalkRoute[] watchRoutes=new WalkRoute[6];
        readonly float[] watchProgress=new float[6];
        readonly Vector3[] watchPoints=new Vector3[6];
        float watchTick=-1;
        internal Vector3 GuardOnDuty(int i)=>ManorOrigin+watchPoints[i];

        static WalkRoute WatchRoute(int index)
        {
            var post=RetinuePosts[index];
            float lane=(index%2==0?-1:1)*(1.23f+.24f*(index/2));
            return Trace(post,new Vector3(post.x,0,-LaneZ),LaneMouth(Gate),
                new Vector3(BridgeLocalX-4.85f,0,lane),new Vector3(BridgeLocalX+.85f,0,lane),
                new Vector3(FrontierMap.Boundary-ManorOrigin.x-1.80f,0,lane));
        }

        void DrawRetinue()
        {
            VisibleRetinue=0;
            float dt=watchTick<0?0:Mathf.Clamp(manorTime-watchTick,0,.25f);watchTick=manorTime;
            for(int i=manorGuardsAway;i<manor.RetinueReady;i++)
            {
                var path=watchRoutes[i];float length=path.total;
                float pace=manor.raidReady?3.65f:1.46f;
                float phase=Mathf.Repeat(manorTime+i*23,2*length/1.46f+33f);
                float duty=phase<12?0:phase<12+length/1.46f?(phase-12)*1.46f:
                    phase<33+length/1.46f?length:Mathf.Max(0,2*length-(phase-33)*1.46f);
                float before=watchProgress[i];
                float next=Mathf.MoveTowards(before,manor.raidReady?0:duty,pace*dt);
                path.Sample(next,out var p,out var dir);
                bool walking=Mathf.Abs(next-before)>.0005f;
                if(next<before)dir=-dir;
                float angle=walking?Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg:
                    (next<.1f?180:88)+Mathf.Sin(manorTime*.48f+i*2.3f)*13;
                // A second watch holds position until its file clears the narrow gate throat.
                bool clear=true;
                for(int j=0;j<occupiedWalkCount;j++)if((p-occupiedWalkSpace[j]).sqrMagnitude<.56f*.56f){clear=false;break;}
                if(!clear){p=watchPoints[i];walking=false;}else watchProgress[i]=next;
                watchPoints[i]=p;occupiedWalkSpace[occupiedWalkCount++]=p;
                var point=ManorOrigin+p;
                if(!InView(point,2.4f))continue;
                WatchAt(point,angle,300+i,RetinueCloth,walking?pace:0,next);
                ContactAt(point,.92f);VisibleRetinue++;
            }
        }

        // These are the same routes and footprint reservations used by the visible population.
        internal static object CheckEstateRoutes()
        {
            var blockers=new List<EstateObstacle>(LandmarkObstacles);
            for(int index=0;index<Manor.PlotCount;index++)
            {
                var pad=PlotObstacle(index);
                float width=index==0?4.89f:2.23f,depth=index==0?4.10f:1.68f;
                if(WestPlot(index)){float swap=width;width=depth;depth=swap;}
                for(int dx=-1;dx<=1;dx+=2)
                    for(int dz=-1;dz<=1;dz+=2)
                        if(!FrontierMap.HasGround(ManorOrigin.x+pad.center.x+dx*width,pad.center.z+dz*depth))
                            throw new InvalidOperationException("Manor plot outside the coast: "+index);
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
                string model=detail.kind==CivicKind.Cart?"market_cart":detail.kind==CivicKind.Wall?"verge_wall":
                    detail.kind==CivicKind.Flowers?"verge_flowers":null;
                if(model!=null)foreach(string lod in new[]{"","_lod1"})
                {
                    var mesh=Resources.Load<Mesh>("Models/"+model+lod);
                    var b=mesh.bounds;
                    if(Mathf.Abs(b.center.x)+b.extents.x>obstacle.hx+.002f||
                        Mathf.Abs(b.center.z)+b.extents.z>obstacle.hz+.002f||b.min.y<-.008f)
                        throw new InvalidOperationException("Roadside mesh exceeds its reserved verge: "+model+lod);
                }
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
            int patrolPoints=0;
            for(int i=0;i<RetinuePosts.Length;i++)
            {
                var route=WatchRoute(i);
                for(float t=0;t<route.total;t+=.19f)
                {
                    route.Sample(t,out var point,out _);
                    if(!FrontierMap.HasGround(ManorOrigin.x+point.x,point.z))
                        throw new InvalidOperationException("Watch patrol leaves the road "+i+" "+point);
                    foreach(var block in blockers)if(block.Contains(point,.27f))
                        throw new InvalidOperationException("Watch patrol intersects "+block.center+" at "+point);
                    patrolPoints++;
                }
            }
            foreach(var post in RetinuePosts)blockers.Add(new EstateObstacle(post,guardRadius,guardRadius));
            int checkedPoints=0,checkedRoutes=0;
            var sampledRoutes=new List<WalkRoute>();
            void Check(WalkRoute route,string label)
            {
                sampledRoutes.Add(route);
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
            for(int first=0;first<RouteCount;first++)
                for(int last=0;last<RouteCount;last++)
                    if(first!=last)Check(WalkBetween(first,last),first+"→"+last);
            var specialists=FixedSpecialistRoutes();
            for(int i=0;i<specialists.Length;i++)Check(specialists[i],Manor.SpecialistIds[i]);
            Check(Trace(new Vector3(OuterConnectorX,0,-OuterLaneZ),
                new Vector3(DockOrigin.x,0,-OuterLaneZ),DockApproach),"south quay");
            Check(Trace(new Vector3(WestLaneX,0,-OuterLaneZ),new Vector3(WestLaneX,0,OuterLaneZ)),"west lane");
            for(int side=-1;side<=1;side+=2)
                Check(Trace(new Vector3(WestLaneX,0,side*OuterLaneZ),new Vector3(OuterLaneEast,0,side*OuterLaneZ)),
                    side>0?"north fields":"south workshops");
            var verges=ChooseVillageVerges(sampledRoutes,System.Array.Empty<WalkRoute>());
            if(verges.Length<18)throw new InvalidOperationException("Too few clear planted verges: "+verges.Length);
            return new {checkedRoutes,checkedPoints,patrolPoints,plantedVerges=verges.Length,retinuePosts=RetinuePosts.Length,outerPlots=Manor.FirstNoblePlot-Manor.FirstOuterPlot,
                noblePlots=Manor.PlotCount-Manor.FirstNoblePlot,civicDetails=CivicDetails.Length,specialistRoutes=specialists.Length};
        }
    }
}
