using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        MeshSet bridgeFittings;
        static readonly Vector3 ManorOrigin=new Vector3(FrontierMap.ManorX,0,0);
        static float FrontierFov => Viewport.Compact?72:48;
        public int FrontierFocus { get; private set; }
        public event System.Action FrontierFocusChanged;
        public bool ViewingFrontier => manor!=null;
        public V3? FrontierDestination { get; set; }

        CameraBookmark blendFrom,blendTo;
        bool cameraBlending;
        float blendElapsed;
        MeshSet campRings,campFlags,campSentries,campSupplies;
        readonly Bounds[] campFlagPickBounds=new Bounds[Manor.Sectors.Count];
        static readonly Color OutpostRed=new Color(.86f,.22f,.14f);
        static readonly Quaternion CampFlagTurn=Quaternion.Euler(0,17,0);
        static readonly Quaternion CampSupplyTurn=Quaternion.Euler(0,-17,0);
        const float CampSupplyScale=1.96f;
        int campTier;
        internal int AlertCampMask {get;private set;}=-1;
        internal int ClaimedFlagMask {get;private set;}
        internal int OutpostSentries => campSentries?.still?.count??0;
        internal int OutpostSupplies => campSupplies?.still?.count??0;
        public bool CameraMoving => cameraBlending;

        public void FrameFrontier(int focus,bool animate=false)
        {
            if(manor==null)return;
            var previous=Bookmark();
            cameraBlending=false;
            bool changed=FrontierFocus!=focus;
            FrontierFocus=focus;
            bool compact=Viewport.Compact;
            float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
            float virtualHeight=(compact?390:1280)/aspect;
            bool patrol=Config?.map==FrontierMap.Id;
            float top=compact?(patrol?192:264):(patrol?132:178);
            float bottom=compact?(patrol?172:340):(patrol?112:focus==0?163:148);
            ViewCamera.fieldOfView=FrontierFov;
            float tangent=Mathf.Tan(FrontierFov*.5f*Mathf.Deg2Rad);
            float tanX=tangent*aspect*(compact?.975f:.965f);
            float tanY=tangent*Mathf.Clamp((virtualHeight-top-bottom)/virtualHeight,.28f,.95f)*(compact?.93f:.97f);
            manorScreenShift=(top-bottom)/virtualHeight*tangent;
            CameraMode=0;
            yaw=compact?(focus==0?-72:focus==2?-57:-8):(focus==0?-10:focus==1?-10:-7);
            pitch=compact?58:focus==0?49:46;
            var rotation=Quaternion.Inverse(Quaternion.Euler(pitch,yaw,0));
            float left=focus==2?FrontierMap.BridgeStart-1:FrontierMap.West-2;
            float right=focus==1?FrontierMap.BridgeStart+2:FrontierMap.East+2;
            target=new Vector3(focus==1?FrontierMap.ManorX:(left+right)*.5f,.25f,0);
            distance=20;
            if(focus!=2)
            {
                FitRegion(left,focus==1?right:FrontierMap.BridgeStart,Manor.PlateauHalfDepth+1.1f,4.4f,rotation,tanX,tanY);
                for(int side=-1;side<=1;side+=2)
                    FitPoint(ManorOrigin+DockOrigin+new Vector3(side*1.3f*DockScale.x,0,-4.85f*DockScale.z),rotation,tanX,tanY);
            }
            if(focus!=1)FitRegion(FrontierMap.Boundary-1,right,FrontierMap.HalfDepth+1,3.1f,rotation,tanX,tanY);
            distance=Mathf.Clamp(distance,20,270);
            if(animate)BlendCamera(previous);
            UpdateShadowDistance();
            if(changed)FrontierFocusChanged?.Invoke();
        }

        internal void FocusManorPlot(int index)
        {
            if(manor==null||(uint)index>=Manor.PlotCount)return;
            var previous=Bookmark();
            bool changed=FrontierFocus!=1;
            FrontierFocus=1;
            CameraMode=0;
            target=ManorOrigin+Plot(index)+Vector3.up*.72f;
            distance=Viewport.Compact?19.5f:17f;
            pitch=Viewport.Compact?56:46;
            yaw=-10;
            manorScreenShift=Viewport.Compact?-.055f:.017f;
            BlendCamera(previous);
            if(changed)FrontierFocusChanged?.Invoke();
        }

        internal void FocusManorRetinue()
        {
            if(manor==null)return;
            var previous=Bookmark();
            bool changed=FrontierFocus!=1;
            FrontierFocus=1;
            CameraMode=0;
            target=ManorOrigin+new Vector3(0,1.22f,-2.70f);
            bool extended=manor.RetinueReady>2;
            distance=Viewport.Compact?(extended?17f:11.3f):(extended?13.2f:9.2f);
            pitch=Viewport.Compact?35:25;
            yaw=6;
            manorScreenShift=Viewport.Compact?-.025f:.02f;
            BlendCamera(previous);
            if(changed)FrontierFocusChanged?.Invoke();
        }

        void BlendCamera(CameraBookmark previous)
        {
            blendTo=Bookmark();
            blendFrom=previous;
            if(previous.mode==1)blendFrom.pitch=87;
            Restore(blendFrom);
            CameraMode=0;
            pitch=blendFrom.pitch;
            cameraBlending=true;
            blendElapsed=0;
        }

        void AdvanceCameraBlend()
        {
            if(!cameraBlending)return;
            blendElapsed+=Time.unscaledDeltaTime;
            float t=Mathf.SmoothStep(0,1,blendElapsed/.28f);
            target=Vector3.Lerp(blendFrom.focus,blendTo.focus,t);
            pitch=Mathf.Lerp(blendFrom.pitch,blendTo.pitch,t);yaw=Mathf.LerpAngle(blendFrom.yaw,blendTo.yaw,t);
            distance=Mathf.Lerp(blendFrom.distance,blendTo.distance,t);
            manorScreenShift=Mathf.Lerp(blendFrom.shift,blendTo.shift,t);
            if(blendElapsed>=.28f)cameraBlending=false;
            UpdateShadowDistance();
        }

        void FitRegion(float left,float right,float halfDepth,float height,Quaternion rotation,float tanX,float tanY)
        {
            for(int i=0;i<2;i++)for(int j=-1;j<=1;j+=2)
            {
                float x=i==0?left:right;
                FitPoint(new Vector3(x,-1.3f,halfDepth*j),rotation,tanX,tanY);
                FitPoint(new Vector3(x,height,halfDepth*j),rotation,tanX,tanY);
            }
        }

        public void Pan(Vector2 delta)
        {
            cameraBlending=false;
            if(CameraMode==2)CameraMode=0;
            float perPixel=2*distance*Mathf.Tan(ViewCamera.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,Screen.height);
            float tilt=CameraMode==1?87:pitch;
            var move=Quaternion.Euler(0,yaw,0)*new Vector3(delta.x,0,delta.y/Mathf.Max(.48f,Mathf.Sin(tilt*Mathf.Deg2Rad)))*perPixel;
            target.x=Mathf.Clamp(target.x-move.x,ViewingFrontier?FrontierMap.West-3:-17,ViewingFrontier?FrontierMap.East+3:17);
            target.z=Mathf.Clamp(target.z-move.z,ViewingFrontier?-FrontierMap.HalfDepth-5:-10,ViewingFrontier?FrontierMap.HalfDepth+5:10);
        }

        void FrontierTile(Vector3 point,Vector3 size,Color color) => Still(surveyBlock,point,Quaternion.identity,size,color);
        void FrontierTrail(float x,float z,float toX,float toZ,float width)
        {
            var span=new Vector3(toX-x,0,toZ-z);
            var at=new Vector3((x+toX)*.5f,.012f,(z+toZ)*.5f);
            var direction=Quaternion.LookRotation(span);
            Still(surveyBlock,at,direction,new Vector3(width,.021f,span.magnitude+.26f),new Color(.50f,.44f,.29f));
            Still(surveyBlock,at+Vector3.up*.012f,direction,new Vector3(width*.71f,.013f,span.magnitude+.12f),new Color(.61f,.52f,.34f));
        }

        internal static Vector3 CampCenter(int sector) =>
            UnityWorld.Vector(FrontierMap.Guards[sector*FrontierMap.GroupSize].camp);
        internal static Vector3 CampFlagAt(int sector) =>
            CampCenter(sector)+new Vector3(sector==1?-1.8f:-4.5f,0,4.35f);
        internal static Vector3 CampSentryAt(int sector) =>
            CampFlagAt(sector)+new Vector3(2.42f,0,.45f);
        static Vector3 CampSupplyAt(int sector) =>
            CampFlagAt(sector)+new Vector3(.45f,0,1.57f);
        static Quaternion CampSentryTurn(int sector) => Quaternion.Euler(0,201+sector*7,0);
        static Vector3 CampYardAt(int sector) => CampCenter(sector)+
            (sector==0?new Vector3(5.05f,0,4.40f):sector==1?new Vector3(-4.91f,0,4.78f):new Vector3(6f,0,-4.6f));
        static string CampYardModel(int sector) => sector==0?"barracks":sector==1?"timber_camp":"smithy";
        static Quaternion CampYardTurn(int sector) => Quaternion.Euler(0,sector==1?-24:sector==2?32:8,0);
        static Vector3 WithGroundedSupply(Bounds local,int sector) =>
            CampSupplyAt(sector)+Vector3.up*(.014f-local.min.y*CampSupplyScale);

        static Bounds PlacedBounds(Bounds local,Vector3 point,Quaternion rotation,float scale)
        {
            var x=rotation*Vector3.right*(local.extents.x*scale);
            var y=rotation*Vector3.up*(local.extents.y*scale);
            var z=rotation*Vector3.forward*(local.extents.z*scale);
            return new Bounds(point+rotation*(local.center*scale),2*new Vector3(
                Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),
                Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
        }

        internal int PickManorSector(Vector2 screen,bool hasGround,Vector3 point)
        {
            if(manor==null||Config.map==FrontierMap.Id||campFlags==null)return -1;
            var ray=ViewCamera.ScreenPointToRay(screen);
            int sector=-1;
            float nearest=float.PositiveInfinity;
            for(int i=0;i<campFlagPickBounds.Length;i++)
                if(campFlagPickBounds[i].IntersectRay(ray,out float distance)&&distance<nearest)
                {
                    nearest=distance;
                    sector=i;
                }
            if(sector>=0||!hasGround||point.x<FrontierMap.Boundary)return sector;
            for(int i=0;i<Manor.Sectors.Count;i++)
            {
                var offset=point-CampCenter(i);
                if(Mathf.Abs(offset.x)<=4.3f&&Mathf.Abs(offset.z)<=3.6f||
                    (point-CampYardAt(i)).sqrMagnitude<=7.3f)return i;
            }
            return -1;
        }

        void BuildFrontierLand()
        {
            var coast=Data.Frontier.platforms[2];
            var bridge=Data.Frontier.platforms[1];
            var water=new Color(.19f,.39f,.49f);
            Still(waterBlock,new Vector3(4,-1.7f,0),Quaternion.identity,new Vector3(900,.12f,900),water);
            FrontierTile(new Vector3(coast.x,-.18f,0),new Vector3(coast.hx*2,.36f,coast.hz*2),
                Color.Lerp(ManorSoils[builtSeason],new Color(.37f,.44f,.30f),.57f));
            FrontierTile(new Vector3(coast.x,-.64f,0),new Vector3(coast.hx*2-.1f,.6f,coast.hz*2-.1f),new Color(.59f,.49f,.36f));
            Still(Load("cliff"),new Vector3(coast.x,-1.19f,0),Quaternion.identity,new Vector3(coast.hx*2,1.15f,coast.hz*2),White);
            FrontierTile(new Vector3(bridge.x,-.34f,0),new Vector3(bridge.hx*2,.68f,bridge.hz*2),new Color(.37f,.29f,.19f));
            for(int i=0;i<21;i++)
                FrontierTile(new Vector3(FrontierMap.BridgeStart+.23f+i*.576f,.01f,0),
                    new Vector3(.55f,.027f,bridge.hz*2-.18f),Color.Lerp(Timber,Track,.44f+i%4*.092f));
            FrontierTile(new Vector3(FrontierMap.Boundary-.11f,.044f,0),new Vector3(.085f,.028f,bridge.hz*2),new Color(.66f,.42f,.17f));
            FrontierTile(new Vector3(FrontierMap.Boundary,.05f,0),new Vector3(.17f,.033f,bridge.hz*2),Gold);
            FrontierTile(new Vector3(FrontierMap.Boundary+.105f,.041f,0),new Vector3(.07f,.027f,bridge.hz*2),Survey);
            bridgeFittings??=Load("bridge_fittings").Variant(false,true,near:true);
            Still(bridgeFittings,new Vector3(bridge.x,0,0),Quaternion.identity,Vector3.one,White);
            Still(Load("frontier_gate"),new Vector3(FrontierMap.Boundary,0,0),Quaternion.identity,Vector3.one,White);
            var bedrock=Load("rock");
            var erodedRim=bedrock.WithoutShadows();
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*(FrontierMap.BridgeHalfWidth+.075f);
                FrontierTile(new Vector3(bridge.x-.32f,.33f,z),new Vector3(11.46f,.16f,.185f),Timber);
                for(int i=0;i<6;i++)
                {
                    float x=FrontierMap.BridgeStart+.54f+i*2.09f;
                    FrontierTile(new Vector3(x,.205f,z),new Vector3(.24f,.53f,.23f),new Color(.50f,.37f,.25f));
                    if(i%2==0)FrontierTile(new Vector3(x,-.74f,z*.89f),new Vector3(.58f,1.03f,.54f),Track);
                }
                for(int i=0;i<17;i++)
                {
                    float x=FrontierMap.Boundary+1.4f+i*4.33f;
                    float jitter=Mathf.Sin(i*5.1f)*.82f;
                    Still(erodedRim,new Vector3(x,-.59f,side*(coast.hz+jitter)),Quaternion.Euler(0,49*i,0),
                        new Vector3(2.1f+i%3,.98f,2.15f),White);
                }
            }
            foreach(var rock in Data.Frontier.rocks)
                Still(bedrock,new Vector3(rock.x,rock.r*.2f,rock.z),Quaternion.Euler(0,CombatMotion.RockYaw(rock.x,rock.z),0),Vector3.one*rock.r*2,White);

            FrontierTrail(FrontierMap.Boundary,0,10,0,2);
            FrontierTrail(10,0,25,13,1.55f);
            FrontierTrail(10,0,31,-7,1.48f);
            FrontierTrail(31,-7,47,-12,1.43f);
            FrontierTrail(10,0,5.2f,4.10f,1.04f);
            for(int i=0;i<4;i++)
                FrontierTile(new Vector3(9.45f+i*.36f,.029f,-.34f+i*.24f),
                    new Vector3(.32f,.021f,.26f),new Color(.70f,.60f,.42f));
            var trees=Load(builtSeason==3?"pine":"tree");
            var grass=Load("grass");
            int count=QualityLevel==0?42:84;
            for(int i=0;i<count;i++)
            {
                float x=-5+66*Mathf.Repeat(i*.618f+.1f,1);
                float z=(i%2==0?1:-1)*(15+7*Mathf.Repeat(i*.418f,1));
                bool tree=i%3==0;
                if(tree&&x>17&&x<34&&z>0)continue;
                Still(tree?trees:grass,new Vector3(x,0,z),Quaternion.Euler(0,i*137.5f,0),
                    Vector3.one*(tree?1.05f+i%4*.18f:1.25f),White,tree?.014f:.03f);
                if(tree)StillShade(new Vector3(x,0,z),2.8f,2.2f);
            }
            foreach(var wall in Data.Frontier.walls)
            {
                var point=new Vector3(wall.x,0,wall.z);
                var turn=Quaternion.Euler(0,wall.hz>wall.hx?90:0,0);
                float span=Mathf.Max(wall.hx,wall.hz)*2,depth=Mathf.Min(wall.hx,wall.hz)*2;
                Still(solidBlock,point+Vector3.up*wall.hy*.71f,turn,new Vector3(span,wall.hy*1.42f,depth*.95f),Timber);
                for(int i=0;i<7;i++)
                {
                    float x=(span-.23f)*(i/6f-.5f);
                    Still(solidBlock,point+turn*new Vector3(x,wall.hy,0),turn,new Vector3(.23f,wall.hy*2,depth),Timber);
                }
                Still(solidBlock,point+Vector3.up*(wall.hy*1.92f-.06f),turn,new Vector3(span,.12f,depth),Track);
            }
            if(campRings==null)
            {
                campRings=ring.Variant(false,false);
                campFlags=props["banner"].Variant(true,true);
                campSentries=Load("retinue_guard").Variant(true,true);
                campSupplies=crate.Variant(false,true);
            }
            AlertCampMask=-1;
            var wave=Load("water_ripple");
            for(int sector=0;sector<Manor.Sectors.Count;sector++)
            {
                var camp=CampCenter(sector);
                var plan=sector==0?new Color(.60f,.49f,.34f):sector==1?new Color(.50f,.50f,.32f):new Color(.53f,.45f,.38f);
                FrontierTile(camp+new Vector3(0,.018f,0),new Vector3(8,.028f,6.7f),plan);
                var yard=CampYardAt(sector);
                Still(Load(CampYardModel(sector)),yard,CampYardTurn(sector),Vector3.one*1.10f,White);
                StillShade(yard,4.1f,3.0f);
                if(sector==0)
                {
                    var beacon=yard+new Vector3(-2.23f,0,.65f);
                    Still(solidBlock,beacon+Vector3.up*.81f,Quaternion.identity,new Vector3(.35f,1.62f,.35f),Timber);
                    Still(Load("brazier"),beacon+Vector3.up*1.62f,Quaternion.identity,Vector3.one*.85f,White);
                }
                else if(sector==1)
                    Still(Load("timber_camp_tower"),yard+new Vector3(-1.25f,0,.53f),Quaternion.Euler(0,-24,0),Vector3.one*.9f,White);
                else
                    Still(props["barricade"],yard+new Vector3(2.55f,0,1.22f),Quaternion.Euler(0,60,0),Vector3.one*.85f,White);
            }
            for(int i=0;i<35;i++)
            {
                float x=-66+144*Mathf.Repeat(i*.617f,1);
                float z=(i%2==0?1:-1)*(9+24*Mathf.Repeat(i*.317f,1));
                if(FrontierMap.HasGround(x,z)||FrontierMap.HasGround(x,z+3)||FrontierMap.HasGround(x,z-3))continue;
                Still(wave,new Vector3(x,-1.62f,z),Quaternion.identity,new Vector3(2.8f,.8f,1.10f),new Color(.45f,.63f,.67f),.07f);
            }
        }

        void SyncCampMarkers()
        {
            bool live=Config.map==FrontierMap.Id&&Current!=null;
            int claimed=live?0:manor.claimedSectors;
            int mask=Manor.SectorMask&~claimed;
            if(live)
            {
                mask=0;
                int blueStart=Config.red.Count;
                for(int i=blueStart;i<Current.units.Count;i++)
                    if(Current.units[i].alive)mask|=1<<((i-blueStart)/FrontierMap.GroupSize);
            }
            int tier=live?manor.frontierTier:1;
            if(mask==AlertCampMask&&tier==campTier&&claimed==ClaimedFlagMask)return;
            Color alert=tier==1?Blue:tier==2?new Color(.79f,.61f,.25f):new Color(.66f,.37f,.80f);
            campRings.still?.Clear();campFlags.still?.Clear();
            campSentries.still?.Clear();campSupplies.still?.Clear();
            for(int i=0;i<Manor.Sectors.Count;i++)
            {
                var camp=CampCenter(i);
                bool active=(mask&(1<<i))!=0;
                bool owned=(claimed&(1<<i))!=0;
                if(active)Still(campRings,camp+Vector3.up*.034f,Quaternion.identity,
                    new Vector3(FrontierMap.AlertRadius,.8f,FrontierMap.AlertRadius),new Color(.52f,.62f,.50f));
                var flag=CampFlagAt(i);
                float flagScale=owned?1.92f:active?1.45f:1.18f;
                Still(campFlags,flag,CampFlagTurn,Vector3.one*flagScale,owned?OutpostRed:active?alert:new Color(.35f,.82f,.55f),.04f);
                campFlagPickBounds[i]=PlacedBounds(campFlags.localBounds,flag,CampFlagTurn,flagScale);
                campFlagPickBounds[i].Expand(.40f);
                if(owned)
                {
                    var post=CampSentryAt(i);
                    Still(campSentries,post,CampSentryTurn(i),Vector3.one,OutpostRed);
                    var cargo=WithGroundedSupply(campSupplies.localBounds,i);
                    Still(campSupplies,cargo,CampSupplyTurn,Vector3.one*CampSupplyScale,White);
                }
            }
            AlertCampMask=mask;campTier=tier;ClaimedFlagMask=claimed;
        }

        internal static int CheckOutpostClearance(Catalog data)
        {
            var sentry=Resources.Load<Mesh>("Models/retinue_guard").bounds;
            var supply=Resources.Load<Mesh>("Models/carry_crate").bounds;
            var banner=Resources.Load<Mesh>("Models/prop_banner").bounds;
            for(int i=0;i<Manor.Sectors.Count;i++)
            {
                var flag=PlacedBounds(banner,CampFlagAt(i),CampFlagTurn,1.92f);
                var guard=PlacedBounds(sentry,CampSentryAt(i),CampSentryTurn(i),1);
                var cargo=PlacedBounds(supply,WithGroundedSupply(supply,i),CampSupplyTurn,CampSupplyScale);
                var yard=PlacedBounds(Resources.Load<Mesh>("Models/"+CampYardModel(i)).bounds,CampYardAt(i),CampYardTurn(i),1.10f);
                if(flag.Intersects(guard)||flag.Intersects(cargo)||guard.Intersects(cargo)||
                    guard.Intersects(yard)||cargo.Intersects(yard))
                    throw new System.InvalidOperationException("Outpost props overlap at sector "+i);
                CheckFootprint(guard,i);
                CheckFootprint(cargo,i);
            }
            return Manor.Sectors.Count;

            void CheckFootprint(Bounds box,int sector)
            {
                if(!FrontierMap.HasGround(box.min.x,box.min.z)||!FrontierMap.HasGround(box.max.x,box.max.z)||box.min.y<-.025f)
                    throw new System.InvalidOperationException("Outpost prop is off the coast or below ground at sector "+sector);
                foreach(var rock in data.Frontier.rocks)
                    if(Mathf.Abs(rock.x-box.center.x)<rock.r+box.extents.x&&Mathf.Abs(rock.z-box.center.z)<rock.r+box.extents.z)
                        throw new System.InvalidOperationException("Outpost prop intersects a rock at sector "+sector);
                foreach(var wall in data.Frontier.walls)
                    if(Mathf.Abs(wall.x-box.center.x)<wall.hx+box.extents.x&&Mathf.Abs(wall.z-box.center.z)<wall.hz+box.extents.z)
                        throw new System.InvalidOperationException("Outpost prop intersects a wall at sector "+sector);
            }
        }

        bool HeroOnPatrol(string type)
        {
            if(Config.map!=FrontierMap.Id)return false;
            foreach(var ally in Config.red)if(ally.type==type)return true;
            return false;
        }

        void DrawPreviewGuards()
        {
            var guards=FrontierMap.Guards;
            var roster=FrontierMap.Roster(manor.frontierTier);
            for(int i=0;i<guards.Count;i+=FrontierMap.GroupSize)
            {
                if(manor.ClaimedSector(i/FrontierMap.GroupSize))continue;
                if(!InView(UnityWorld.Vector(guards[i].camp),9))continue;
                var offset=guards[i].PatrolOffset(manorTime,out var heading);
                for(int j=i;j<i+FrontierMap.GroupSize;j++)
                {
                    var guard=guards[j];var p=guard.spawn+offset;
                    var model=Model(roster[j]);
                    Soldier(model,"blue",UnityWorld.Vector(p)+Vector3.up*(model.definition.scale*.8f),
                        heading,0,220+j,speed:.48f,direction:1,gait:manorTime*.54f);
                }
            }
        }
        void DrawFrontierPatrol()
        {
            if(Current!=null&&Config.map==FrontierMap.Id)DrawLiveFrame();
            if(FrontierDestination.HasValue)
                Ring(UnityWorld.Vector(FrontierDestination.Value)+Vector3.up*.065f,1.2f,Gold);
            DrawPointer(new Color(.64f,.83f,.93f));
        }
    }
}
