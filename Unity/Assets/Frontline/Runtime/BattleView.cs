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
        public int PropSelection {get;set;} = -1;
        internal string MarkerOrder {get;set;}
        static readonly Color InspireTint=new Color(.98f,.73f,.31f),FocusTint=new Color(1,.49f,.31f),FortifyTint=new Color(.53f,.78f,1);
        internal bool MeasureCasterVertices {get;set;}
        internal long CasterVertices {get;private set;}
        internal static int PlatformDefaultQuality => Application.platform==RuntimePlatform.WebGLPlayer?1:2;
        string qualityKey="frontline.visual.quality";
        internal int PreferredQuality => Mathf.Clamp(PlayerPrefs.GetInt(qualityKey,PlatformDefaultQuality),0,2);
#if UNITY_WEBGL && !UNITY_EDITOR
        internal double WebShaderWarmupMs {get;private set;}
#endif
        public int QualityLevel {get;private set;}
        public int RenderSubmissions {get;private set;}public int RenderInstances {get;private set;}public int StaticRebuilds {get;private set;}
#if UNITY_WEBGL && !UNITY_EDITOR
        // Match the GLES3 shader pages and stay below WebGL 2's minimum uniform-block limit.
        const int Capacity=64;
#else
        const int Capacity=512;
#endif
        internal const int InstancingPageSize=Capacity;
        const int MinShadowVertexSavings=2400;
        internal long MainVertices {get;private set;}
        internal int ShadowOnlySubmissions {get;private set;}
        bool splitFineShadows=false;
        static readonly int HitId=Shader.PropertyToID("_HitFlash"),TeamId=Shader.PropertyToID("_TeamColor"),WindId=Shader.PropertyToID("_Wind"),SoftnessId=Shader.PropertyToID("_Softness");
        static readonly Color Red=new Color(.82f,.22f,.17f),Blue=new Color(.17f,.43f,.79f),Gold=new Color(1,.75f,.27f),White=Color.white;
        sealed class MeshSet
        {
            public Mesh high,low;
            public int highVertices,lowVertices;
            public Batch moving,still,grid,ambient,outer;
            MeshSet shadowless;
            public bool atmosphere,castShadow=true,receiveShadow=true,nearDetail,detailProxy;
            public Bounds localBounds;
            public MeshSet Variant(bool cast,bool receive,bool near=false) => new MeshSet {
                high=high,low=low,highVertices=highVertices,lowVertices=lowVertices,atmosphere=atmosphere,
                localBounds=localBounds,castShadow=cast,receiveShadow=receive,nearDetail=near,detailProxy=detailProxy
            };
            public MeshSet WithoutShadows()=>castShadow?(shadowless??=Variant(false,receiveShadow,nearDetail)):this;
        }
        sealed class Batch
        {
            public MeshSet mesh;public readonly Matrix4x4[] matrices=new Matrix4x4[Capacity];public readonly Vector4[] colors=new Vector4[Capacity];public readonly float[] flashes=new float[Capacity];public readonly float[] winds=new float[Capacity];public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
            public int count,visibilityVersion=-1;
            public bool fixedPage,visible,hasWind,colorDirty=true,flashDirty=true,windDirty=true;
            public Bounds bounds;public Batch next;
            public StaticChunks chunks;
            public void Clear(){count=0;if(fixedPage)visibilityVersion=-1;hasWind=false;chunks?.Clear();if(next!=null)next.Clear();}
            public void Add(Matrix4x4 matrix,Color color,float wind,float flash=0,int chunk=-1)
            {
                if(count==Capacity){if(next==null)next=new Batch{mesh=mesh,fixedPage=fixedPage};next.Add(matrix,color,wind,flash,chunk);return;}
                matrices[count]=matrix;
                Vector4 rgba=color;
                if(!colors[count].Equals(rgba)){colors[count]=rgba;colorDirty=true;}
                if(winds[count]!=wind){winds[count]=wind;windDirty=true;}
                hasWind|=wind!=0;
                if(flashes[count]!=flash){flashes[count]=flash;flashDirty=true;}
                if(fixedPage)
                {
                    var local=mesh.localBounds;
                    var e=local.extents;
                    var extent=new Vector3(Mathf.Abs(matrix.m00)*e.x+Mathf.Abs(matrix.m01)*e.y+Mathf.Abs(matrix.m02)*e.z,
                        Mathf.Abs(matrix.m10)*e.x+Mathf.Abs(matrix.m11)*e.y+Mathf.Abs(matrix.m12)*e.z,
                        Mathf.Abs(matrix.m20)*e.x+Mathf.Abs(matrix.m21)*e.y+Mathf.Abs(matrix.m22)*e.z);
                    // Manor casters may have longer shadows under the winter sun.
                    var center=matrix.MultiplyPoint3x4(local.center);
                    float padding=!mesh.castShadow?1f:chunk>=0?Mathf.Max(12f,(center.y+extent.y)*3.34f):12f;
                    var instance=new Bounds(center,extent*2+Vector3.one*padding);
                    if(count==0)bounds=instance;else bounds.Encapsulate(instance);
                    if(chunk>=0)(chunks??=new StaticChunks(mesh)).Add(count,chunk,instance);
                }
                count++;
            }
        }
        sealed class UnitVisual
        {
            public readonly MeshSet body,right,crest;
            public readonly SoldierKind kind;
            public UnitDef definition;
            public UnitVisual(string type,MeshSet body,MeshSet right,MeshSet crest=null)
            {
                this.body=body;this.right=right;this.crest=crest;
                kind=type=="cavalry"?SoldierKind.Cavalry:type=="catapult"?SoldierKind.Catapult:
                    type=="shield"?SoldierKind.Shield:SoldierKind.Infantry;
            }
        }
        readonly Dictionary<string,MeshSet> meshes=new Dictionary<string,MeshSet>();readonly Dictionary<string,MeshSet> fxMeshes=new Dictionary<string,MeshSet>();readonly Dictionary<string,UnitVisual> units=new Dictionary<string,UnitVisual>();readonly Dictionary<string,MeshSet> props=new Dictionary<string,MeshSet>();
        readonly List<Batch> moving=new List<Batch>(),still=new List<Batch>(),grid=new List<Batch>();
        Material material,atmosphere,groundingMaterial,sky;Light sun;MeshSet armLeft,leg,horse,horseLeg,catapult,catapultArm,shield,ring,dot,torso,head,limb,wreckHorse,wreckCart,arrow,bolt,bomb,stone;MapEcology.Mote[] motes;
        MeshSet dust;float activeFlash;struct MotionSample {public Vector3 p;public float time,speed,phase,direction;public bool valid;}readonly MotionSample[] oldMotion=new MotionSample[122];
        struct GroundSample {public string map;public float x,z,height;}
        readonly GroundSample[] unitFloors=new GroundSample[2*Catalog.MaxUnits+1];
        GroundSample[] propFloors=Array.Empty<GroundSample>();
        readonly float[] manorMaxHp=new float[Catalog.MaxUnits], guardMaxHp=new float[FrontierMap.Guards.Count];
        Deployment scaledFormation;
        bool variedTerrain;
        readonly Plane[] frustum=new Plane[6];
        Matrix4x4 previousClip;
        int visibilityVersion;
        bool lowScenery,lowActors;
        internal bool LowActorLod => lowActors;
        internal bool LowSceneryLod => lowScenery;
        internal float CameraDistance => distance;
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
        float sidebarWeight;
        bool autoArenaDistance=true;
        bool HasDesktopDeck => !Viewport.Compact&&manor==null&&Deploying&&Config!=null&&Config.manorHall==0;

        float ArenaDistance(float deckWeight)
        {
            if(Viewport.Compact)return 62;
            float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
            float span=Mathf.Lerp(.94f,.75f,deckWeight);
            // Include the near corners of the 36 m arena and the desktop deployment sidebar.
            return Mathf.Clamp(7.2f+18f/(Mathf.Tan(21*Mathf.Deg2Rad)*aspect*span),35,58);
        }
        void Awake()
        {
            ViewCamera=new GameObject("Battle camera").AddComponent<Camera>();ViewCamera.transform.SetParent(transform,false);ViewCamera.clearFlags=CameraClearFlags.Skybox;ViewCamera.fieldOfView=Viewport.Compact?65:42;ViewCamera.nearClipPlane=.5f;ViewCamera.farClipPlane=160;ViewCamera.gameObject.AddComponent<AudioListener>();
            sun=new GameObject("Warm key light").AddComponent<Light>();sun.transform.SetParent(transform,false);sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.type=LightType.Directional;sun.color=new Color(1,.92f,.8f);sun.intensity=.95f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.46f;sun.shadowBias=.045f;
#if !UNITY_WEBGL
            splitFineShadows=SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D11;
#endif
            material=new Material(Resources.Load<Shader>("FrontlineInstanced")){enableInstancing=true};atmosphere=new Material(Resources.Load<Shader>("FrontlineAtmosphere")){enableInstancing=true};sky=new Material(Resources.Load<Shader>("FrontlineSky"));RenderSettings.skybox=sky;
            groundingMaterial=new Material(atmosphere){enableInstancing=true};
            var fillDirection=(Quaternion.Euler(30,145,0)*Vector3.back).normalized;
            material.SetColor("_FillColor",new Color(.58f,.72f,1)*.28f);
            material.SetVector("_FillDirection",fillDirection);
            RenderSettings.ambientLight=new Color(.46f,.53f,.59f);
            foreach(string type in new[]{"militia","shield","spear","archer","crossbow","bomber","medic","giant","cavalry","catapult"})units.Add(type,new UnitVisual(type,Load("body_"+type),Load("arm_right_"+type)));
            foreach(string type in new[]{"barrel","barricade","banner","spikes","spring"})props.Add(type,Load("prop_"+type));
            dust=Load("mist",true);armLeft=Load("arm_left");leg=Load("leg");horse=Load("horse");horseLeg=Load("horse_leg");catapult=Load("catapult");catapultArm=Load("catapult_arm");shield=Load("shield");ring=Load("ring");dot=Load("dot");torso=Load("torso");head=Load("head");limb=Load("limb");wreckHorse=Load("wreck_horse");wreckCart=Load("wreck_catapult");arrow=Load("arrow");bolt=Load("bolt");bomb=Load("bomb");stone=Load("stone");
            weatherPage=new Batch { mesh=Load("snow",true) };
            InitializeAccents();

            // Reserve up to 12 strokes per effect and two marks per health bar before contact.
            const int feedbackInstances=96*12+120*2;
            var feedback=Buffer(dot,moving,0);
            for(int reserved=Capacity;reserved<feedbackInstances;reserved+=Capacity)
            {feedback.next=new Batch{mesh=dot};feedback=feedback.next;}
            ViewCamera.allowMSAA=true;
            if(VisualValidation.Requested)
            {
                qualityKey="frontline.qa.visual.quality";
                PlayerPrefs.DeleteKey(qualityKey);
            }
            SetQuality(PreferredQuality,false);
#if UNITY_WEBGL && !UNITY_EDITOR
            WarmWebCombatShaders();
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        void WarmWebCombatShaders()
        {
            // Cover the runtime's directional light, caster pass and translucent effects.
            // Unity can select the non-instanced variant for a batch with one visible item.
            var variants=new ShaderVariantCollection();
            void Add(Shader shader,PassType pass,params string[] keywords)
            {
                var instanced=new string[keywords.Length+1];
                keywords.CopyTo(instanced,0);
                instanced[keywords.Length]="INSTANCING_ON";
                variants.Add(new ShaderVariantCollection.ShaderVariant(shader,pass,keywords));
                variants.Add(new ShaderVariantCollection.ShaderVariant(shader,pass,instanced));
            }
            Add(material.shader,PassType.ForwardBase,"DIRECTIONAL");
            Add(material.shader,PassType.ForwardBase,"DIRECTIONAL","SHADOWS_SCREEN");
            Add(material.shader,PassType.ShadowCaster,"SHADOWS_DEPTH");
            Add(atmosphere.shader,PassType.Normal);
            double started=Time.realtimeSinceStartupAsDouble;
            variants.WarmUp();
            WebShaderWarmupMs=(Time.realtimeSinceStartupAsDouble-started)*1000;
            Destroy(variants);
        }
#endif
        MeshSet Load(string name,bool transparent=false)
        {
            string key=transparent?name+"_transparent":name;if(meshes.TryGetValue(key,out var set))return set;
            var high=Resources.Load<Mesh>("Models/"+name);var low=Resources.Load<Mesh>("Models/"+name+"_lod1");if(!high)throw new InvalidOperationException("Missing baked visual mesh Models/"+name+". Run ModelBaker.Bake before building.");
            set=new MeshSet{high=high,low=low?low:high,atmosphere=transparent,localBounds=high.bounds};
            set.localBounds.Encapsulate(set.low.bounds);
            set.highVertices=set.high.vertexCount;set.lowVertices=set.low.vertexCount;
            bool small=name=="grass"||name=="flowers"||name=="reeds"||name=="water_ripple"||
                name=="ring"||name=="dot"||name=="arrow"||name=="bolt"||name=="bomb"||name=="stone";
            bool secondary=name=="base"||name=="island_root"||name=="cliff"||name=="icicle"||name=="shrub"||
                name=="snow"||name=="flame"||name=="mist"||name=="civilian_hat"||name=="villager_wrap"||
                name=="resident_sleeve"||name=="civilian_pack"||name.StartsWith("carry_",StringComparison.Ordinal);
            set.castShadow=!transparent&&!small&&!secondary&&name!="platform";
            set.receiveShadow=!transparent&&!small;
            set.nearDetail=name=="flowers"||name=="grass";
            set.detailProxy=set.castShadow&&set.high!=set.low&&DetailedCaster(name);
            meshes.Add(key,set);return set;
        }
        static bool DetailedCaster(string name) =>
            name.StartsWith("body_",StringComparison.Ordinal)||name.StartsWith("arm_right_",StringComparison.Ordinal)||
            name=="leg"||name=="horse"||name=="horse_leg"||name=="wreck_horse"||name=="shield"||
            name=="catapult"||name=="catapult_arm"||name=="wreck_catapult"||name=="retinue_guard"||name=="frontier_gate"||name=="manor_hall"||
            name=="head"||name=="tree"||name=="pine"||name=="rock"||name=="prop_barrel"||name=="prop_barricade"||name=="prop_banner"||
            name.StartsWith("specialist_",StringComparison.Ordinal)||
            System.Array.IndexOf(Manor.Buildings,name)>=0||name.EndsWith("_wing",StringComparison.Ordinal)||name.EndsWith("_tower",StringComparison.Ordinal);
        Batch Buffer(MeshSet mesh,List<Batch> list,int category)
        {
            var b=category==0?mesh.moving:category==1?mesh.still:category==2?mesh.grid:mesh.outer;
            if(b==null)
            {
                b=new Batch{mesh=mesh,fixedPage=category!=0};
                if(category==0)mesh.moving=b;else if(category==1)mesh.still=b;else if(category==2)mesh.grid=b;else mesh.outer=b;
                list.Add(b);
            }
            return b;
        }
        void Add(MeshSet mesh,Matrix4x4 matrix,Color color,float wind=0)
        {
            if(fillingAmbient)
            {
                if(mesh.ambient==null){mesh.ambient=new Batch{mesh=mesh};ambient.Add(mesh.ambient);}
                mesh.ambient.Add(matrix,color,wind,activeFlash);
            }
            else (mesh.moving??Buffer(mesh,moving,0)).Add(matrix,color,wind,activeFlash);
        }
        UnitVisual Model(string type)
        {
            if(!units.TryGetValue(type,out var model))
            {
                var crest=Data.Heroes.ContainsKey(type)?Load("crest_"+type).WithoutShadows():null;
                model=new UnitVisual(type,Load("body_"+type),Load("arm_right_"+type),crest);
                units.Add(type,model);
            }
            if(model.definition==null)model.definition=Data.Unit(type);
            return model;
        }
        void Draw(Batch b,bool isStatic)
        {
            var set=b.mesh;
            if(manor!=null&&(set.nearDetail&&lowScenery||set==raidPennant&&!manor.raidReady))return;
            bool showLow=isStatic?lowScenery:lowActors;
            var mesh=showLow?set.low:set.high;
            int mainVertexCount=showLow?set.lowVertices:set.highVertices;
            bool detailProxy=!showLow&&set.castShadow&&set.detailProxy;
            bool eligible=splitFineShadows&&QualityLevel==2&&!showLow&&set.castShadow&&set.lowVertices<set.highVertices;
            for(var source=b;source!=null;source=source.next)if(source.count>0)
            {
                var page=source;
                if(isStatic)
                {
                    page=VisibleStaticPage(source);
                    if(page==null)continue;
                }
                if(page.colorDirty){page.properties.SetVectorArray(TeamId,page.colors);page.colorDirty=false;}
                if(page.flashDirty&&!set.atmosphere){page.properties.SetFloatArray(HitId,page.flashes);page.flashDirty=false;}
                if(page.windDirty){page.properties.SetFloatArray(set.atmosphere?SoftnessId:WindId,page.winds);page.windDirty=false;}
                var surface=set==contactShade?groundingMaterial:set.atmosphere?atmosphere:material;
                bool split=detailProxy||eligible&&(set.highVertices-set.lowVertices)*page.count>=MinShadowVertexSavings;
                Graphics.DrawMeshInstanced(mesh,0,surface,page.matrices,page.count,page.properties,
                    set.castShadow&&!split?ShadowCastingMode.On:ShadowCastingMode.Off,set.receiveShadow,0,ViewCamera,LightProbeUsage.Off);
                RenderSubmissions++;RenderInstances+=page.count;
                if(split)
                {
                    // Preserve the visible mesh, placement and receiving shadows. Only change the caster mesh.
                    Graphics.DrawMeshInstanced(set.low,0,surface,page.matrices,page.count,page.hasWind?page.properties:null,
                        ShadowCastingMode.ShadowsOnly,false,0,ViewCamera,LightProbeUsage.Off);
                    RenderSubmissions++;ShadowOnlySubmissions++;
                }
                if(MeasureCasterVertices)
                {
                    MainVertices+=(long)mainVertexCount*page.count;
                    if(set.castShadow)CasterVertices+=(long)(split?set.lowVertices:mainVertexCount)*page.count;
                }
            }
        }
        bool InView(Vector3 point,float radius)
        {
            for(int i=0;i<6;i++)if(frustum[i].GetDistanceToPoint(point)<-radius)return false;
            return true;
        }
        // The build defaults and the in-game controls use the same three graphics presets.
        public static void ApplyQualityPreset(int level)
        {
            QualitySettings.antiAliasing=level==2?4:level==1?2:0;
            QualitySettings.shadowResolution=level==2?ShadowResolution.VeryHigh:level==1?ShadowResolution.High:ShadowResolution.Medium;
            QualitySettings.shadowCascades=level==2?4:level==1?2:0;
            QualitySettings.shadows=level==0?ShadowQuality.HardOnly:ShadowQuality.All;
#if !UNITY_WEBGL
            if(level==2)QualitySettings.shadowCascade4Split=new Vector3(.14f,.39f,.68f);
#endif
        }
        public void SetQuality(int quality,bool remember=true)
        {
            QualityLevel=Mathf.Clamp(quality,0,2);
            ApplyQualityPreset(QualityLevel);UpdateShadowDistance();cachedQuality=-1;
            if(remember)
            {
                PlayerPrefs.SetInt(qualityKey,QualityLevel);
                PlayerPrefs.Save();
            }
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
            foreach(var d in MapEcology.Build(Data,Config.map,QualityLevel))
            {
                var set=Load(d.mesh,d.atmosphere);
                Buffer(d.castsShadow?set:set.WithoutShadows(),still,1).Add(d.matrix,d.tint,d.wind);
            }
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

        static Color OrderTint(string type)=>type=="focus"?FocusTint:type=="fortify"?FortifyTint:InspireTint;

        void DrawPointer(Color ordinary)
        {
            if(!Marker.HasValue)return;
            var at=Marker.Value+Vector3.up*.065f;
            if(MarkerOrder==null){Ring(at,MarkerRadius,ordinary);return;}
            var tint=OrderTint(MarkerOrder);
            Add(dust,Pose(at,Quaternion.identity,new Vector3(MarkerRadius*2,1,MarkerRadius*2)),
                new Color(tint.r,tint.g,tint.b,.14f),1);
            Ring(at,MarkerRadius,tint);
            Ring(at+Vector3.up*.015f,.35f,tint);
        }

        internal UnitDef Definition(string type){return Model(type).definition;}
        internal float MaxHp(UnitDef def,int id)
        {
            if(Config.manorHall==0||id<1)return def.hp;
            int guard=id-1-Config.red.Count;
            bool patrol=Config.map==FrontierMap.Id;
            if(guard>=0&&(!patrol||guard>=Config.blue.Count||Config.blue[guard].level<=1))return def.hp;
            if(scaledFormation!=Config)
            {
                for(int i=0;i<Config.red.Count;i++)
                {
                    var unit=Config.red[i];
                    float health=(float)LevelScaling.Scale(Data.Unit(unit.type).hp,unit.level);
                    if(unit.armament>0)health=health*(100+Manor.Armament(unit.type,unit.armament).life)/100;
                    manorMaxHp[i]=health;
                }
                if(patrol)
                    for(int i=0;i<Config.blue.Count;i++)
                    {
                        var unit=Config.blue[i];
                        guardMaxHp[i]=(float)LevelScaling.Scale(Data.Unit(unit.type).hp,Mathf.Max(1,unit.level));
                    }
                scaledFormation=Config;
            }
            return guard>=0?guardMaxHp[guard]:manorMaxHp[id-1];
        }
        public void Focus(Vector3 point,float zoom=19,float viewPitch=43,float viewYaw=0){cameraBlending=false;zoomTarget=0;target=point;distance=zoom;pitch=viewPitch;yaw=viewYaw;if(manor!=null)UpdateShadowDistance();else autoArenaDistance=false;}
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
                return;
            }
            if(e.type!="slam"&&e.type!="explosion")
                Ring(p+Vector3.up*.12f,.3f+f*(e.type=="heal"?1:2.5f),e.type=="heal"?new Color(.37f,.89f,.55f):new Color(1,.49f,.13f));
        }
        void Prop(string type,string team,float x,float z,float ground,bool active)
        {if(!Catalog.Finite(ground))return;Add(props[type],Pose(new Vector3(x,ground,z),Quaternion.identity,Vector3.one),active&&type=="barrel"?new Color(1,.45f,.12f):Team(team),type=="banner"?.025f:0);}
        void DrawDeployment()
        {
            bool red=HiddenTeam!="red"&&HiddenTeam!="both",blue=HiddenTeam!="blue"&&HiddenTeam!="both";
            bool jobs=BeginSoldierPoses((red?Config.red.Count:0)+(blue?Config.blue.Count:0));
            if(red)DrawDeploymentTeam(Config.red,"red",Mathf.PI/2,1);
            if(blue)DrawDeploymentTeam(Config.blue,"blue",-Mathf.PI/2,Config.red.Count+1);
            if(jobs)FinishSoldierPoses();
        }

        void DrawDeploymentTeam(List<UnitPlacement> roster,string team,float angle,int id)
        {
            foreach(var u in roster)
            {
                var model=Model(u.type);
                float floor=UnitFloor(id,u.x,u.z);
                Soldier(model,team,new Vector3(u.x,floor+.8f*model.definition.scale,u.z),angle,0,id++,stance:u.stance,floor:floor,armament:u.armament);
            }
        }
        void DrawLiveFrame()
        {
            bool jobs=false;
            if(Current.units.Count>=ParallelPoseThreshold&&ParallelPoseWorkers>1)
            {
                int alive=0;
                foreach(var u in Current.units)if(u.alive&&u.t!=null)alive++;
                jobs=BeginSoldierPoses(alive);
            }
            foreach(var u in Current.units)if(u.alive&&u.t!=null)
            {
                var model=Model(u.type);
                int index=u.id-1;
                int armament=Config.manorHall>0&&(uint)index<Config.red.Count?Config.red[index].armament:0;
                Soldier(model,u.team,new Vector3(u.t[0],u.t[1],u.t[2]),u.angle,u.attack,u.id,u.hp/MaxHp(model.definition,u.id),u.inspired,u.fortified,u.stance,u.moveSpeed,u.moveForward,u.gait,u.hitFlash,u.hitTiltX,u.hitTiltZ,u.charge,u.guard,armament:armament);
            }
            if(jobs)FinishSoldierPoses();
            foreach(var p in Current.props)if(p.alive)Prop(p.type,p.team,p.x,p.z,p.floor,p.active);
            foreach(var s in Current.projectiles){var t=s.t;var mesh=s.type=="stone"?stone:s.type=="bomb"?bomb:s.type=="bolt"?bolt:arrow;var velocity=UnityWorld.Vector(s.v);Add(mesh,Pose(new Vector3(t[0],t[1],t[2]),velocity.sqrMagnitude>.0001f?Quaternion.LookRotation(velocity):Quaternion.identity,Vector3.one),White);}
            foreach(var r in Current.ragdolls)
            {
                float scale=Model(r.type).definition.scale;
                if(r.wreck!=null&&r.wreck.Length==4&&Catalog.Finite(r.wreck[1])){var w=r.wreck;Add(r.type=="cavalry"?wreckHorse:wreckCart,Pose(new Vector3(w[0],w[1],w[2]),Quaternion.Euler(0,w[3]*Mathf.Rad2Deg,0),Vector3.one*scale),Team(r.team));}
                foreach(var part in r.parts){var t=part.t;Add(part.name=="torso"?torso:part.name=="head"?head:limb,Pose(new Vector3(t[0],t[1],t[2]),new Quaternion(t[3],t[4],t[5],t[6]),Vector3.one*scale),part.name=="head"?White:Team(r.team));}
            }
            foreach(var e in Current.effects)Impact(e);
            DrawBattleAccents(Current);
        }
        internal long PerformanceRenderTicks {get;private set;}
        internal bool SupportedDesktopShaders=>material.shader.isSupported&&material.passCount>=2&&
            atmosphere.shader.isSupported&&atmosphere.passCount>0&&sky.shader.isSupported&&sky.passCount>0;
        internal Vector4 GroundFade=>groundingMaterial.GetVector(GroundFadeId);

        void LateUpdate()
        {
            if(!MeasureCasterVertices){DrawFrame();return;}
            long before=System.Diagnostics.Stopwatch.GetTimestamp();
            DrawFrame();
            PerformanceRenderTicks=System.Diagnostics.Stopwatch.GetTimestamp()-before;
        }

        void DrawFrame()
        {
            if(Data==null||Config==null)return;foreach(var b in moving)b.Clear();RenderSubmissions=RenderInstances=RenderHeightSamples=ShadowOnlySubmissions=CulledStaticInstances=0;CasterVertices=MainVertices=0;
            if(cameraWidth!=Screen.width||cameraHeight!=Screen.height){cameraWidth=Screen.width;cameraHeight=Screen.height;if(manor==null){ViewCamera.fieldOfView=Viewport.Compact?65:42;sidebarWeight=HasDesktopDeck?1:0;}else FrameManor();}
            if(manor==null&&CameraMode==2&&Current!=null){UnitFrame best=null;float bestCost=0;foreach(var u in Current.units)if(u.alive){float cost=Model(u.type).definition.cost;if(best==null||cost>bestCost){best=u;bestCost=cost;}}if(best?.t!=null)target=Vector3.Lerp(target,new Vector3(best.t[0],1,best.t[2]),.1f);}
            AdvanceCameraBlend();
            AdvanceFrontierZoom();
            if(seasonLightBlending)AdvanceSeasonLight();
            if(manor==null)
            {
                sidebarWeight=Mathf.MoveTowards(sidebarWeight,HasDesktopDeck?1:0,Time.unscaledDeltaTime*5);
                if(autoArenaDistance)distance=ArenaDistance(sidebarWeight);
#if !UNITY_WEBGL
                if(QualityLevel==2)UpdateShadowDistance();
#endif
            }
            var rotation=Quaternion.Euler(CameraMode==1?87:pitch,yaw,0);cameraPosition=target-rotation*Vector3.forward*distance;
            if(manor!=null)cameraPosition+=rotation*Vector3.up*(manorScreenShift*distance);
            else if(sidebarWeight>0)
            {
                float halfWidth=distance*Mathf.Tan(ViewCamera.fieldOfView*.5f*Mathf.Deg2Rad)*ViewCamera.aspect;
                cameraPosition-=rotation*Vector3.right*(halfWidth*.208f*sidebarWeight);
            }
            ViewCamera.transform.SetPositionAndRotation(cameraPosition,rotation);
            if(manor!=null)
            {
                float farPlane=Mathf.Max(2600,distance*5.5f);
                if(!Mathf.Approximately(ViewCamera.farClipPlane,farPlane))ViewCamera.farClipPlane=farPlane;
            }
            var clip=ViewCamera.projectionMatrix*ViewCamera.worldToCameraMatrix;
            if(clip!=previousClip)
            {
                GeometryUtility.CalculateFrustumPlanes(clip,frustum);poseFrustum=new PoseFrustum(frustum);
                previousClip=clip;visibilityVersion++;
            }
            float sceneryRange=manor==null?42:53,actorRange=sceneryRange;
            if(QualityLevel==2)
            {
                sceneryRange=manor==null?76:Viewport.Compact?75:144;
                actorRange=manor==null?72:Viewport.Compact?72:134;
            }
            // A small hysteresis band keeps fine parts from blinking when zooming around a threshold.
            lowScenery=QualityLevel==0||distance>sceneryRange-(lowScenery?2:0);
            lowActors=QualityLevel==0||distance>actorRange-(lowActors?2:0)||
                (manor==null&&QualityLevel==1&&distance>31&&Config.red.Count+Config.blue.Count>=80);
            if(manor!=null)UpdateGroundFade();
            if(manor!=null){DrawManor();return;}
            if(cachedMap!=Config.map||cachedQuality!=QualityLevel)RebuildMap();
            foreach(var b in still)Draw(b,true);if(Deploying)foreach(var b in grid)Draw(b,true);
            var map=Data.Maps[Config.map];
            if(Current==null)
            {
                DrawDeployment();
                int propIndex=0;
                foreach(var p in map.props)Prop(p.type,p.team,p.x,p.z,FloorAt(ref propFloors[propIndex++],p.x,p.z),false);
                for(int i=0;i<Config.props.Count;i++)
                {
                    var p=Config.props[i];
                    if(p.team==HiddenTeam||HiddenTeam=="both")continue;
                    float floor=FloorAt(ref propFloors[propIndex++],p.x,p.z);
                    Prop(p.type,p.team,p.x,p.z,floor,false);
                    if(i==PropSelection&&Catalog.Finite(floor))Ring(new Vector3(p.x,floor+.057f,p.z),Data.Props[p.type].radius+.20f,Gold);
                }
            }
            else DrawLiveFrame();
            float time=Time.time;
            bool snow=Config.map=="highland",spark=Config.map=="ruins"||Config.map=="island",bridge=Config.map=="bridge";
            foreach(var mote in motes)
            {
                var p=mote.origin;
                p.y=snow?7-Mathf.Repeat(time*mote.speed+mote.phase,7):spark?Mathf.Repeat(time*mote.speed+mote.phase,3):.4f+Mathf.Sin(time*.4f+mote.phase)*.22f;
                p.x+=Mathf.Sin(time*.6f+mote.phase)*.5f;if(bridge){p.x=Mathf.Sin(mote.phase)*2;p.y=-3.8f+Mathf.Repeat(time*mote.speed+mote.phase,1)*.55f;}
                Add(fxMeshes[mote.mesh],Pose(p,Quaternion.Euler(0,mote.phase,0),Vector3.one*mote.size),mote.tint);
            }
            DrawPointer(Gold);foreach(var b in moving)Draw(b,false);
        }
        public void Orbit(Vector2 delta){cameraBlending=false;CameraMode=0;yaw+=delta.x*.22f;pitch=Mathf.Clamp(pitch-delta.y*.15f,25,84);}
        public void Zoom(float amount)
        {
            cameraBlending=false;
            if(manor!=null)
            {
                float origin=zoomTarget>0?zoomTarget:distance;
                zoomTarget=Mathf.Clamp(origin*Mathf.Exp(-amount*.118f),5.8f,MaxFrontierDistance);
                return;
            }
            distance=Mathf.Clamp(distance*Mathf.Exp(-amount*.095f),12,90);
            autoArenaDistance=false;
        }
        public void ResetCamera(){if(manor!=null){FrameFrontier(0);return;}target=Vector3.zero;yaw=0;pitch=51;ViewCamera.fieldOfView=Viewport.Compact?65:42;autoArenaDistance=true;sidebarWeight=HasDesktopDeck?1:0;distance=ArenaDistance(sidebarWeight);CameraMode=0;}
        public bool Ground(Vector2 screen,out Vector3 p)
        {
            var ray=ViewCamera.ScreenPointToRay(screen);p=Vector3.zero;
            if(manor!=null)return PickFrontierGround(ray,out p);
            float nearest=float.PositiveInfinity;foreach(var platform in Data.Maps[Config.map].platforms){var normal=Quaternion.Euler(0,0,platform.angle*Mathf.Rad2Deg)*Vector3.up;var plane=new Plane(normal,new Vector3(platform.x,platform.y,platform.z)+normal*platform.hy);if(plane.Raycast(ray,out float hitDistance)&&hitDistance<nearest){var hit=ray.GetPoint(hitDistance);float h=Data.Height(Config.map,hit.x,hit.z);if(Catalog.Finite(h)&&Mathf.Abs(h-hit.y)<.1f){nearest=hitDistance;p=hit;}}}return nearest<float.PositiveInfinity;
        }
        internal void StopPresentation()
        {
            enabled=false;
            if(ViewCamera)ViewCamera.enabled=false;
            DisposeSoldierPoses();
        }
        void OnDestroy(){StopPresentation();if(RenderSettings.skybox==sky)RenderSettings.skybox=null;if(material)Destroy(material);if(atmosphere)Destroy(atmosphere);if(groundingMaterial)Destroy(groundingMaterial);if(sky)Destroy(sky);if(lakeMaterial)Destroy(lakeMaterial);}
    }
}


