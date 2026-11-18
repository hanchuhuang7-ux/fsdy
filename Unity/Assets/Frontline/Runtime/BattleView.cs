using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView:MonoBehaviour
    {
        public Camera ViewCamera {get;private set;}[NonSerialized] public Catalog Data;public Deployment Config;public Frame Current;public string HiddenTeam;
        public readonly HashSet<int> Selection=new HashSet<int>();[NonSerialized] public Vector3? Marker;public float MarkerRadius=1;public bool Deploying=true;public int CameraMode;
        public int QualityLevel {get;private set;}=1;public int RenderSubmissions {get;private set;}public int RenderInstances {get;private set;}public int StaticRebuilds {get;private set;}
        const int Capacity=256;
        static readonly int HitId=Shader.PropertyToID("_HitFlash"),TeamId=Shader.PropertyToID("_TeamColor"),WindId=Shader.PropertyToID("_Wind"),SoftnessId=Shader.PropertyToID("_Softness");
        static readonly Color Red=new Color(.82f,.22f,.17f),Blue=new Color(.17f,.43f,.79f),Gold=new Color(1,.75f,.27f),White=Color.white;
        sealed class MeshSet {public Mesh high,low;public Batch moving,still,grid;public bool atmosphere;}
        sealed class Batch
        {
            public MeshSet mesh;public readonly Matrix4x4[] matrices=new Matrix4x4[Capacity];public readonly Vector4[] colors=new Vector4[Capacity];public readonly float[] flashes=new float[Capacity];public readonly float[] winds=new float[Capacity];public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();public int count;public bool dirty=true;public Batch next;
            public void Clear(){count=0;dirty=true;if(next!=null)next.Clear();}
            public void Add(Matrix4x4 matrix,Color color,float wind,float flash=0)
            {if(count==Capacity){if(next==null)next=new Batch{mesh=mesh};next.Add(matrix,color,wind,flash);return;}matrices[count]=matrix;colors[count]=color;winds[count]=wind;flashes[count]=flash;count++;dirty=true;}
        }
        sealed class UnitVisual {public MeshSet body,right;public UnitDef definition;}
        readonly Dictionary<string,MeshSet> meshes=new Dictionary<string,MeshSet>();readonly Dictionary<string,MeshSet> fxMeshes=new Dictionary<string,MeshSet>();readonly Dictionary<string,UnitVisual> units=new Dictionary<string,UnitVisual>();readonly Dictionary<string,MeshSet> props=new Dictionary<string,MeshSet>();
        readonly List<Batch> moving=new List<Batch>(),still=new List<Batch>(),grid=new List<Batch>();
        Material material,atmosphere,sky;Light sun,fill;MeshSet armLeft,leg,horse,horseLeg,catapult,catapultArm,shield,ring,dot,torso,head,limb,wreckHorse,wreckCart,arrow,bolt,bomb,stone;MapEcology.Mote[] motes;
        MeshSet dust;float activeFlash;struct MotionSample {public Vector3 p;public float time,speed,phase,direction;public bool valid;}readonly MotionSample[] oldMotion=new MotionSample[122];
        struct GroundSample {public string map;public float x,z,height;}
        readonly GroundSample[] unitFloors=new GroundSample[2*Catalog.MaxUnits+1];
        GroundSample[] propFloors=Array.Empty<GroundSample>();
        bool variedTerrain;
        public int RenderHeightSamples {get;private set;}
        float FloorAt(ref GroundSample sample,float x,float z)
        {
            if(sample.map!=Config.map||sample.x!=x||sample.z!=z)
            {
                sample.map=Config.map;sample.x=x;sample.z=z;sample.height=Data.Height(Config.map,x,z);
                RenderHeightSamples++;
            }
            return sample.height;
        }
        float UnitFloor(int id,float x,float z)
        {
            if((uint)id<unitFloors.Length)return FloorAt(ref unitFloors[id],x,z);
            RenderHeightSamples++;return Data.Height(Config.map,x,z);
        }
        Vector3 target=Vector3.zero;float yaw,distance=40,pitch=51;int cameraWidth,cameraHeight,cachedQuality=-1;string cachedMap;Vector3 cameraPosition;
        void Awake()
        {
            ViewCamera=new GameObject("Battle camera").AddComponent<Camera>();ViewCamera.transform.SetParent(transform,false);ViewCamera.clearFlags=CameraClearFlags.Skybox;ViewCamera.fieldOfView=Viewport.Compact?65:42;ViewCamera.nearClipPlane=.1f;ViewCamera.farClipPlane=160;ViewCamera.gameObject.AddComponent<AudioListener>();
            sun=new GameObject("Warm key light").AddComponent<Light>();sun.transform.SetParent(transform,false);sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.type=LightType.Directional;sun.color=new Color(1,.92f,.8f);sun.intensity=.95f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.46f;sun.shadowBias=.045f;
            fill=new GameObject("Cool rim light").AddComponent<Light>();fill.transform.SetParent(transform,false);fill.transform.rotation=Quaternion.Euler(30,145,0);fill.type=LightType.Directional;fill.color=new Color(.58f,.72f,1);fill.intensity=.28f;fill.shadows=LightShadows.None;fill.renderMode=LightRenderMode.ForceVertex;
            material=new Material(Resources.Load<Shader>("FrontlineInstanced")){enableInstancing=true};atmosphere=new Material(Resources.Load<Shader>("FrontlineAtmosphere")){enableInstancing=true};sky=new Material(Resources.Load<Shader>("FrontlineSky"));RenderSettings.skybox=sky;
            material.SetColor("_FillColor",fill.color*fill.intensity);material.SetVector("_FillDirection",-fill.transform.forward);RenderSettings.ambientLight=new Color(.46f,.53f,.59f);
            foreach(string type in new[]{"militia","shield","spear","archer","crossbow","bomber","medic","giant","cavalry","catapult"})units.Add(type,new UnitVisual{body=Load("body_"+type),right=Load("arm_right_"+type)});
            foreach(string type in new[]{"barrel","barricade","banner","spikes","spring"})props.Add(type,Load("prop_"+type));
            dust=Load("mist",true);armLeft=Load("arm_left");leg=Load("leg");horse=Load("horse");horseLeg=Load("horse_leg");catapult=Load("catapult");catapultArm=Load("catapult_arm");shield=Load("shield");ring=Load("ring");dot=Load("dot");torso=Load("torso");head=Load("head");limb=Load("limb");wreckHorse=Load("wreck_horse");wreckCart=Load("wreck_catapult");arrow=Load("arrow");bolt=Load("bolt");bomb=Load("bomb");stone=Load("stone");

            // 96 feedback effects x at most 12 strokes + 120 health bars x 2 fit in six pages.
            // Allocate these at view setup, never on a mass-contact frame.
            var feedback=Buffer(dot,moving,0);for(int i=1;i<6;i++){feedback.next=new Batch{mesh=dot};feedback=feedback.next;}
            SetQuality(Mathf.Clamp(PlayerPrefs.GetInt("frontline.visual.quality",1),0,2));
        }
        MeshSet Load(string name,bool transparent=false)
        {
            string key=transparent?name+"_transparent":name;if(meshes.TryGetValue(key,out var set))return set;
            var high=Resources.Load<Mesh>("Models/"+name);var low=Resources.Load<Mesh>("Models/"+name+"_lod1");if(!high)throw new InvalidOperationException("Missing baked visual mesh Models/"+name+". Run ModelBaker.Bake before building.");
            set=new MeshSet{high=high,low=low?low:high,atmosphere=transparent};meshes.Add(key,set);return set;
        }
        Batch Buffer(MeshSet mesh,List<Batch> list,int category)
        {
            var b=category==0?mesh.moving:category==1?mesh.still:mesh.grid;
            if(b==null){b=new Batch{mesh=mesh};if(category==0)mesh.moving=b;else if(category==1)mesh.still=b;else mesh.grid=b;list.Add(b);}return b;
        }
        void Add(MeshSet mesh,Matrix4x4 matrix,Color color,float wind=0){(mesh.moving??Buffer(mesh,moving,0)).Add(matrix,color,wind,activeFlash);}
        UnitVisual Model(string type)
        {
            var model=units[type];
            if(model.definition==null)model.definition=Data.Units[type];
            return model;
        }
        void Draw(Batch b,bool isStatic)
        {
            bool low=QualityLevel==0||distance>(QualityLevel==2?58:42);var mesh=low?b.mesh.low:b.mesh.high;
            for(var page=b;page!=null;page=page.next)if(page.count>0)
            {
                if(!isStatic||page.dirty)
                {
                    page.properties.SetVectorArray(TeamId,page.colors);
                    if(!b.mesh.atmosphere)page.properties.SetFloatArray(HitId,page.flashes);
                    page.properties.SetFloatArray(b.mesh.atmosphere?SoftnessId:WindId,page.winds);
                    page.dirty=false;
                }
                Graphics.DrawMeshInstanced(mesh,0,b.mesh.atmosphere?atmosphere:material,page.matrices,page.count,page.properties,b.mesh.atmosphere?ShadowCastingMode.Off:ShadowCastingMode.On,!b.mesh.atmosphere,0,ViewCamera);
                RenderSubmissions++;RenderInstances+=page.count;
            }
        }
        public void SetQuality(int quality)
        {
            QualityLevel=Mathf.Clamp(quality,0,2);PlayerPrefs.SetInt("frontline.visual.quality",QualityLevel);UpdateShadowDistance();QualitySettings.shadowResolution=QualityLevel==2?ShadowResolution.High:ShadowResolution.Medium;cachedQuality=-1;
        }
        void RebuildMap()
        {
            foreach(var b in still)b.Clear();foreach(var b in grid)b.Clear();
            var map=Data.Maps[Config.map];
            int propCapacity=map.props.Count+2*Catalog.MaxProps;
            if(propFloors.Length<propCapacity)propFloors=new GroundSample[propCapacity];
            variedTerrain=false;
            foreach(var platform in map.platforms)
                if(platform.angle!=0||platform.y+platform.hy!=map.platforms[0].y+map.platforms[0].hy){variedTerrain=true;break;}
            foreach(var d in MapEcology.Build(Data,Config.map,QualityLevel))Buffer(Load(d.mesh,d.atmosphere),still,1).Add(d.matrix,d.tint,d.wind);
            for(int i=-16;i<=16;i+=2)for(int j=-10;j<=10;j+=2){float h=Data.Height(Config.map,i,j);if(Catalog.Finite(h))Buffer(dot,grid,2).Add(Matrix4x4.TRS(new Vector3(i,h+.027f,j),Quaternion.identity,new Vector3(.095f,.025f,.095f)),i<0?new Color(.91f,.53f,.37f):new Color(.4f,.71f,.94f),0);}
            for(int j=-11;j<=11;j++){float h=Data.Height(Config.map,0,j);if(Catalog.Finite(h))Buffer(dot,grid,2).Add(Matrix4x4.TRS(new Vector3(0,h+.03f,j),Quaternion.identity,new Vector3(.065f,.025f,.35f)),new Color(.91f,.91f,.76f),0);}
            motes=MapEcology.Particles(Config.map,QualityLevel);foreach(var m in motes)if(!fxMeshes.ContainsKey(m.mesh))fxMeshes.Add(m.mesh,Load(m.mesh,true));
            bool snow=Config.map=="highland",marsh=Config.map=="marsh",ruins=Config.map=="ruins";
            Color horizon=snow?new Color(.7f,.8f,.86f):marsh?new Color(.54f,.65f,.58f):ruins?new Color(.69f,.59f,.47f):new Color(.67f,.77f,.73f);
            sky.SetColor("_Horizon",horizon);sky.SetColor("_Zenith",snow?new Color(.27f,.43f,.61f):new Color(.19f,.36f,.43f));sky.SetColor("_Nadir",new Color(.17f,.23f,.26f));RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=horizon;RenderSettings.fogDensity=marsh?.012f:.0068f;
            material.SetColor("_SkyLight",snow?new Color(.65f,.72f,.78f):new Color(.57f,.64f,.66f));material.SetColor("_GroundLight",new Color(.24f,.27f,.21f));cachedMap=Config.map;cachedQuality=QualityLevel;StaticRebuilds++;
        }
        static Color Team(string team){return team=="red"?Red:team=="blue"?Blue:Gold;}
        static Matrix4x4 Pose(Vector3 p,Quaternion rotation,Vector3 scale){return Matrix4x4.TRS(p,rotation,scale);}
        void Ring(Vector3 p,float radius,Color color){Add(ring,Pose(p,Quaternion.identity,new Vector3(radius,1,radius)),color);}
        void Soldier(UnitVisual model,string type,string team,Vector3 p,float angle,float attack,int id,float hp=1,bool inspired=false,bool fortified=false,string stance="advance",float speed=0,float direction=0,float gait=0,float hitFlash=0,float hitTiltX=0,float hitTiltZ=0,float charge=0,float guard=0,float floor=float.NaN)
        {
            activeFlash=hitFlash;float time=Current?.time??Time.time,scale=model.definition.scale;
            if(!Deploying&&gait==0&&id<oldMotion.Length){var m=oldMotion[id];if(time<m.time)m=default;if(m.valid&&time>m.time){var delta=p-m.p;float dt=time-m.time;float travel=new Vector2(delta.x,delta.z).magnitude;m.speed=travel/dt;m.phase+=travel/scale;m.direction=(delta.x*Mathf.Sin(angle)+delta.z*Mathf.Cos(angle))/Mathf.Max(.001f,travel);}m.p=p;m.time=time;m.valid=true;oldMotion[id]=m;speed=m.speed;gait=m.phase;direction=m.direction;}
            p.y+=Mathf.Sin(time*2.2f+id)*.008f*scale;if(type=="cavalry")p.y+=.55f*scale;var color=Team(team);float tilt=type=="cavalry"?Mathf.Clamp01(charge/1.1f)*7:0;
            if(type=="cavalry"&&variedTerrain)
            {
                float dx=Mathf.Sin(angle)*.6f*scale,dz=Mathf.Cos(angle)*.6f*scale;
                float front=Data.Height(Config.map,p.x+dx,p.z+dz),back=Data.Height(Config.map,p.x-dx,p.z-dz);RenderHeightSamples+=2;
                if(Catalog.Finite(front)&&Catalog.Finite(back))tilt-=Mathf.Atan2(front-back,1.2f*scale)*Mathf.Rad2Deg;
            }
            var root=Pose(p,Quaternion.Euler(hitTiltZ*Mathf.Rad2Deg,0,-hitTiltX*Mathf.Rad2Deg)*Quaternion.Euler(0,angle*Mathf.Rad2Deg,0)*Quaternion.Euler(tilt,0,0),Vector3.one*scale);float amplitude=speed<.12f?0:Mathf.Clamp01(speed/model.definition.speed);float walk=Deploying?0:Mathf.Sin(gait*4.4f*(direction<-.15f?-1:1)+id)*28*amplitude*(direction<-.15f?.65f:1);float swing=Mathf.Sin(Mathf.Clamp01(attack)*Mathf.PI)*65;
            Add(model.body,root,color);
            if(type=="catapult")
            {Add(catapult,root,White);Add(catapultArm,root*Pose(new Vector3(0,.59f,0),Quaternion.Euler(-35-swing*.8f,0,0),Vector3.one),White);}
            else
            {
                Add(leg,root*Pose(new Vector3(-.15f,-.53f,0),Quaternion.Euler(walk,0,0),Vector3.one),White);
                Add(leg,root*Pose(new Vector3(.15f,-.53f,0),Quaternion.Euler(-walk,0,0),Vector3.one),White);
                Add(armLeft,root*Pose(new Vector3(-.36f,.27f,0),Quaternion.Euler(-walk*.5f,0,0),Vector3.one)*Matrix4x4.Translate(new Vector3(.36f,-.27f,0)),color);
                Add(model.right,root*Pose(new Vector3(.36f,.27f,0),Quaternion.Euler(-swing+walk*.4f,0,-4),Vector3.one)*Matrix4x4.Translate(new Vector3(-.36f,-.27f,0)),color);
                if(type=="shield")Add(shield,root*Pose(new Vector3(0,guard*.1f,guard*.15f),Quaternion.Euler(-guard*14,0,0),Vector3.one),color);
                if(type=="cavalry")
                {
                    Add(horse,root,White);for(int i=0;i<4;i++){float x=i%2==0?-.24f:.24f,z=i<2?-.4f:.4f;Add(horseLeg,root*Pose(new Vector3(x,-.65f,z),Quaternion.Euler(i%2==0?walk:-walk,0,0),Vector3.one),White);}
                }
            }
            activeFlash=0;
            bool tracks=!Deploying&&type=="cavalry"&&charge>.55f&&speed>3.8f;
            bool selected=Selection.Contains(id),showStance=Deploying&&stance!="advance";
            if(tracks||selected||showStance||inspired||fortified)
            {
                if(float.IsNaN(floor))floor=UnitFloor(id,p.x,p.z);
                if(!Catalog.Finite(floor))floor=p.y-.8f*scale;
                var foot=new Vector3(p.x,floor+.045f,p.z);
                if(tracks){for(int i=0;i<2;i++){float age=Mathf.Repeat(gait*.65f+i*.5f,1);var dp=foot-new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*(.5f+age*.9f);dp.y+=.12f+age*.22f;Add(dust,Pose(dp,Quaternion.Euler(0,id*37,0),Vector3.one*(.25f+age*.45f)),new Color(.66f,.57f,.39f,.24f*(1-age)));}}
                if(selected)Ring(foot,.7f*scale,Gold);else if(showStance)Ring(foot,.48f*scale,stance=="hold"?new Color(.64f,.72f,.75f):White);
                if(inspired||fortified)Ring(foot,.65f*scale,fortified?new Color(.47f,.76f,1):Gold);
            }
            if(hp<.99f){Add(dot,Pose(p+Vector3.up*(1.1f*scale),Quaternion.identity,new Vector3(.7f*scale,.055f,.1f)),new Color(.12f,.15f,.18f));Add(dot,Pose(p+new Vector3(-.35f*scale*(1-hp),1.12f*scale,0),Quaternion.identity,new Vector3(.7f*scale*Mathf.Clamp01(hp),.06f,.11f)),color);}
        }
        internal UnitDef Definition(string type){return Model(type).definition;}
        public void Focus(Vector3 point,float zoom=19,float viewPitch=43,float viewYaw=0){target=point;distance=zoom;pitch=viewPitch;yaw=viewYaw;}
        // The baked dot is 0.02m tall; Y scales below convert stroke lengths to world metres.
        void Impact(EffectFrame e)
        {
            float f=1-e.life/e.maxLife;var p=new Vector3(e.x,e.y,e.z);bool block=e.type=="block",heavy=e.type=="heavy";
            if(e.type=="sweep"||e.type=="thrust"||e.type=="charge"){
                bool thrust=e.type=="thrust";int count=thrust?3:9;float fade=1-f;
                for(int i=0;i<count;i++){float a=e.angle+(thrust?(i-1)*.08f:(i/(float)(count-1)-.5f)*2.2f);var forward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var q=p+forward*(thrust?.45f+f*1.5f:1.05f+f*.65f);q.y+=thrust?.05f:.25f;
                    var direction=thrust?forward:new Vector3(Mathf.Cos(a),.08f,-Mathf.Sin(a));Add(dot,Pose(q,Quaternion.FromToRotation(Vector3.up,direction),new Vector3(.075f*fade,(thrust?.9f:.43f)*fade*50,.07f*fade)),i%2==0?new Color(1,.86f,.42f):new Color(1,.97f,.8f));}return;
            }
            if(e.type=="wall"){
                for(int i=0;i<4;i++){float a=i*2.4f+e.id;var q=p+new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a))*(.15f+f*.7f);Add(dust,Pose(q,Quaternion.Euler(0,a*40,0),Vector3.one*(.32f+f*.75f)),new Color(.67f,.6f,.47f,.44f*(1-f)));}return;
            }
            if(block||heavy||e.type=="hit"){
                int count=block?8:heavy?10:4;for(int i=0;i<count;i++){float a=i*2.39996f+e.id*1.13f;var d=new Vector3(Mathf.Cos(a),.35f+Mathf.Abs(Mathf.Sin(a*2)),Mathf.Sin(a));var q=p+d*f*(heavy?1.6f:1.2f)+Vector3.down*f*f*.65f;float length=(1-f)*(block?.55f:heavy?.48f:.26f),width=(1-f)*(block?.085f:heavy?.095f:.06f);Add(dot,Pose(q,Quaternion.FromToRotation(Vector3.up,d),new Vector3(width,length*50,width)),block?new Color(1,.88f,.46f):heavy?new Color(1,.54f,.18f):new Color(1,.94f,.85f));}
                if(block||heavy){var face=ViewCamera.transform.rotation;for(int i=0;i<2;i++)Add(dot,Pose(p,face*Quaternion.Euler(0,0,i==0?45:-45),new Vector3(.09f*(1-f),.95f*(1-f)*50,.07f)),block?new Color(1,.96f,.66f):new Color(1,.65f,.27f));}
                if(!heavy)return;
            }
            Ring(p+Vector3.up*.12f,.3f+f*(e.type=="heal"?1:2.5f),e.type=="heal"?new Color(.37f,.89f,.55f):new Color(1,.49f,.13f));
        }
        void Prop(string type,string team,float x,float z,float ground,bool active)
        {if(!Catalog.Finite(ground))return;Add(props[type],Pose(new Vector3(x,ground,z),Quaternion.identity,Vector3.one),active&&type=="barrel"?new Color(1,.45f,.12f):Team(team),type=="banner"?.025f:0);}
        void DrawDeployment(List<UnitPlacement> roster,string team,float angle,int id)
        {
            foreach(var u in roster)
            {
                var model=Model(u.type);
                float floor=UnitFloor(id,u.x,u.z);
                Soldier(model,u.type,team,new Vector3(u.x,floor+.8f*model.definition.scale,u.z),angle,0,id++,stance:u.stance,floor:floor);
            }
        }
        void LateUpdate()
        {
            if(Data==null||Config==null)return;foreach(var b in moving)b.Clear();RenderSubmissions=RenderInstances=RenderHeightSamples=0;
            if(cameraWidth!=Screen.width||cameraHeight!=Screen.height){cameraWidth=Screen.width;cameraHeight=Screen.height;ViewCamera.fieldOfView=Viewport.Compact?65:42;if(manor==null)distance=Viewport.Compact?70:40;else FrameManor();}
            if(manor==null&&CameraMode==2&&Current!=null){UnitFrame best=null;float bestCost=0;foreach(var u in Current.units)if(u.alive){float cost=Model(u.type).definition.cost;if(best==null||cost>bestCost){best=u;bestCost=cost;}}if(best?.t!=null)target=Vector3.Lerp(target,new Vector3(best.t[0],1,best.t[2]),.1f);}
            var rotation=Quaternion.Euler(CameraMode==1?87:pitch,yaw,0);cameraPosition=target-rotation*Vector3.forward*distance;
            if(manor!=null)cameraPosition+=rotation*Vector3.up*(manorScreenShift*distance);
            ViewCamera.transform.SetPositionAndRotation(cameraPosition,rotation);
            if(manor!=null){DrawManor();return;}
            if(cachedMap!=Config.map||cachedQuality!=QualityLevel)RebuildMap();
            foreach(var b in still)Draw(b,true);if(Deploying)foreach(var b in grid)Draw(b,true);
            var map=Data.Maps[Config.map];
            if(Current==null)
            {
                if(HiddenTeam!="red"&&HiddenTeam!="both")DrawDeployment(Config.red,"red",Mathf.PI/2,1);
                if(HiddenTeam!="blue"&&HiddenTeam!="both")DrawDeployment(Config.blue,"blue",-Mathf.PI/2,Config.red.Count+1);
                int propIndex=0;
                foreach(var p in map.props)Prop(p.type,p.team,p.x,p.z,FloorAt(ref propFloors[propIndex++],p.x,p.z),false);
                foreach(var p in Config.props)if(p.team!=HiddenTeam&&HiddenTeam!="both")Prop(p.type,p.team,p.x,p.z,FloorAt(ref propFloors[propIndex++],p.x,p.z),false);
            }
            else
            {
                foreach(var u in Current.units)if(u.alive&&u.t!=null)
                {
                    var model=Model(u.type);
                    Soldier(model,u.type,u.team,new Vector3(u.t[0],u.t[1],u.t[2]),u.angle,u.attack,u.id,u.hp/model.definition.hp,u.inspired,u.fortified,u.stance,u.moveSpeed,u.moveForward,u.gait,u.hitFlash,u.hitTiltX,u.hitTiltZ,u.charge,u.guard);
                }
                foreach(var p in Current.props)if(p.alive)Prop(p.type,p.team,p.x,p.z,p.floor,p.active);
                foreach(var s in Current.projectiles){var t=s.t;var mesh=s.type=="stone"?stone:s.type=="bomb"?bomb:s.type=="bolt"?bolt:arrow;var velocity=UnityWorld.Vector(s.v);Add(mesh,Pose(new Vector3(t[0],t[1],t[2]),velocity.sqrMagnitude>.0001f?Quaternion.LookRotation(velocity):Quaternion.identity,Vector3.one),White);}
                foreach(var r in Current.ragdolls)
                {
                    float scale=Model(r.type).definition.scale;
                    if(r.wreck!=null&&r.wreck.Length==4&&Catalog.Finite(r.wreck[1])){var w=r.wreck;Add(r.type=="cavalry"?wreckHorse:wreckCart,Pose(new Vector3(w[0],w[1],w[2]),Quaternion.Euler(0,w[3]*Mathf.Rad2Deg,0),Vector3.one*scale),Team(r.team));}
                    foreach(var part in r.parts){var t=part.t;Add(part.name=="torso"?torso:part.name=="head"?head:limb,Pose(new Vector3(t[0],t[1],t[2]),new Quaternion(t[3],t[4],t[5],t[6]),Vector3.one*scale),part.name=="head"?White:Team(r.team));}
                }
                foreach(var e in Current.effects)Impact(e);
                foreach(var o in Current.commander.active)if(o.endTick>Current.tick)Ring(new Vector3(o.x,Data.Height(Config.map,o.x,o.z)+.08f,o.z),o.type=="focus"?4:6,Team(o.team));
            }
            float time=Time.time;
            bool snow=Config.map=="highland",spark=Config.map=="ruins"||Config.map=="island",bridge=Config.map=="bridge";
            foreach(var mote in motes)
            {
                var p=mote.origin;
                p.y=snow?7-Mathf.Repeat(time*mote.speed+mote.phase,7):spark?Mathf.Repeat(time*mote.speed+mote.phase,3):.4f+Mathf.Sin(time*.4f+mote.phase)*.22f;
                p.x+=Mathf.Sin(time*.6f+mote.phase)*.5f;if(bridge){p.x=Mathf.Sin(mote.phase)*2;p.y=-3.8f+Mathf.Repeat(time*mote.speed+mote.phase,1)*.55f;}
                Add(fxMeshes[mote.mesh],Pose(p,Quaternion.Euler(0,mote.phase,0),Vector3.one*mote.size),mote.tint);
            }
            if(Marker.HasValue)Ring(Marker.Value+Vector3.up*.06f,MarkerRadius,Gold);foreach(var b in moving)Draw(b,false);
        }
        public void Orbit(Vector2 delta){CameraMode=0;yaw+=delta.x*.22f;pitch=Mathf.Clamp(pitch-delta.y*.15f,25,84);}
        public void Zoom(float amount){distance=Mathf.Clamp(distance-amount*3,manor==null?18:12,90);if(manor!=null)UpdateShadowDistance();}
        public void ResetCamera(){if(manor!=null){FrameManor();return;}target=Vector3.zero;yaw=0;pitch=51;distance=Viewport.Compact?70:40;CameraMode=0;}
        public bool Ground(Vector2 screen,out Vector3 p)
        {
            var ray=ViewCamera.ScreenPointToRay(screen);p=Vector3.zero;
            if(manor!=null)
            {
                if(!ManorGround.Raycast(ray,out float t))return false;
                p=ray.GetPoint(t);return Mathf.Abs(p.x)<=13f&&Mathf.Abs(p.z)<=9f;
            }
            float nearest=float.PositiveInfinity;foreach(var platform in Data.Maps[Config.map].platforms){var normal=Quaternion.Euler(0,0,platform.angle*Mathf.Rad2Deg)*Vector3.up;var plane=new Plane(normal,new Vector3(platform.x,platform.y,platform.z)+normal*platform.hy);if(plane.Raycast(ray,out float hitDistance)&&hitDistance<nearest){var hit=ray.GetPoint(hitDistance);float h=Data.Height(Config.map,hit.x,hit.z);if(Catalog.Finite(h)&&Mathf.Abs(h-hit.y)<.1f){nearest=hitDistance;p=hit;}}}return nearest<float.PositiveInfinity;
        }
        void OnDestroy(){if(RenderSettings.skybox==sky)RenderSettings.skybox=null;if(material)Destroy(material);if(atmosphere)Destroy(atmosphere);if(sky)Destroy(sky);}
    }
}


