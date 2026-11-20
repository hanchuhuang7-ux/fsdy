using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Frontline.UnityGame
{
    public sealed class UnityWorldFactory:IWorldFactory
    {
        readonly bool refined;
        public UnityWorldFactory(bool refined=true){this.refined=refined;}
        public IPhysicsWorld Create(Catalog data,Deployment config)=>new UnityWorld(data,config,refined);
    }
    public sealed class CollisionTag:MonoBehaviour {public int id;}
    public sealed class ProjectileContacts:MonoBehaviour
    {
        public int id;[NonSerialized]public UnityWorld world;[NonSerialized]public UnityBody unit;
        void OnCollisionEnter(Collision c){if(unit!=null)world?.UnitContact(unit,c);else world?.Contact(id,world.ColliderId(c.collider));}
    }
    // Listen at the obstacle, not at every infantry foot/friendly contact.
    public sealed class ObstacleContacts:MonoBehaviour
    {
        [NonSerialized]public UnityWorld world;void OnCollisionEnter(Collision c){world?.ObstacleContact(c);}
    }
    public sealed class UnityBody:IBody,ICombatBody,IWallImpactBody,IFrontierBody
    {
        public int Id {get;private set;}public readonly GameObject gameObject;public readonly Rigidbody rigidbody;readonly Transform pose;public Collider collider;public CollisionTag tag;public ProjectileContacts sink;public bool pooled;public float gravity;public Collider[] colliders;public string type,team;public bool oriented,fast,fixedRotation;public int wallUntil=-1,lastWall=-999;internal UnityWorld world;readonly bool dynamicBody;int positionStep=-1,velocityStep=-1;V3 cachedPosition,cachedVelocity;public float facing,launchAge;public int ownerColliderCount;public readonly List<Collider> ignored=new List<Collider>();
        public UnityBody(int id,GameObject go,Rigidbody rb){Id=id;gameObject=go;rigidbody=rb;pose=go.transform;dynamicBody=!ReferenceEquals(rb,null);}
        public void Facing(float angle,bool highSpeed){if(!dynamicBody)return;if(oriented&&Math.Abs(angle-facing)>.015f){rigidbody.MoveRotation(Quaternion.Euler(0,angle*Mathf.Rad2Deg,0));facing=angle;}if(fast!=highSpeed){fast=highSpeed;rigidbody.collisionDetectionMode=fast?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.Discrete;}}
        public void ArmWallImpact(int steps){wallUntil=world.StepIndex+steps;}
        internal void ResetId(int id){positionStep=-1;velocityStep=-1;Id=id;if(tag)tag.id=id;if(sink)sink.id=id;}
        public V3 Position {get{if(world==null)return UnityWorld.Value(dynamicBody?rigidbody.position:gameObject.transform.position);if(positionStep!=world.StepIndex){cachedPosition=UnityWorld.Value(dynamicBody?rigidbody.position:gameObject.transform.position);positionStep=world.StepIndex;}return cachedPosition;}}
        public V3 Velocity {get{if(!dynamicBody)return new V3();if(world==null)return UnityWorld.Value(rigidbody.linearVelocity);if(velocityStep!=world.StepIndex){cachedVelocity=UnityWorld.Value(rigidbody.linearVelocity);velocityStep=world.StepIndex;}return cachedVelocity;}}
        public void Impulse(V3 v){if(dynamicBody)rigidbody.AddForce(UnityWorld.Vector(v),ForceMode.Impulse);}
        public void StopAtFrontier(float x)
        {
            var p=rigidbody.position; p.x=x;
            rigidbody.position=pose.position=p;
            var v=rigidbody.linearVelocity; v.x=Mathf.Max(0,v.x);
            rigidbody.linearVelocity=v;
            positionStep=velocityStep=-1;
        }
        public float[] Transform(float[] reuse=null)
        {
            var t=reuse!=null&&reuse.Length==7?reuse:new float[7];Vector3 p;Quaternion q;
            if(fixedRotation){p=UnityWorld.Vector(Position);q=Quaternion.identity;}
            else {pose.GetPositionAndRotation(out p,out q);cachedPosition=UnityWorld.Value(p);positionStep=world==null?-1:world.StepIndex;}
            t[0]=p.x;t[1]=p.y;t[2]=p.z;t[3]=q.x;t[4]=q.y;t[5]=q.z;t[6]=q.w;return t;
        }
    }
    public sealed class UnityWorld:IPhysicsWorld,IWallImpactWorld,ICorpseCount
    {
        public int CorpseCount {get{return remains.Count;}}public int StepIndex {get;private set;}public int FilteredGroundContacts,FilteredFriendlyContacts,RecordedChargeContacts,RecordedWallImpacts;
        readonly List<WallImpact> wallImpacts=new List<WallImpact>(24);readonly Dictionary<Collider,UnityBody> collisionBodies=new Dictionary<Collider,UnityBody>();
        public bool MeasurePhysics;public long PhysicsTicks;public string SceneName {get{return scene.name;}}
        const int GroundLayer=8,UnitLayer=9,ShotLayer=10,PropLayer=11,CorpseLayer=12,FrontierRimLayer=13,FrontierFloorLayer=14;
        readonly List<UnityBody> banners=new List<UnityBody>();readonly Scene scene;readonly PhysicsScene physics;readonly GameObject root;readonly Catalog data;readonly Deployment config;
        readonly MapDef map;
        readonly bool refined;
        readonly Collider frontierFence;
        readonly Dictionary<int,UnityBody> bodies=new Dictionary<int,UnityBody>();readonly List<Contact> contacts=new List<Contact>(512);readonly HashSet<ulong> pairs=new HashSet<ulong>(512);
        sealed class Remains {public int id;public string type,team;public float age,settleAt;public bool settling;public bool hasWreck;public readonly float[] wreck=new float[4];public readonly List<UnityBody> parts=new List<UnityBody>(6);public readonly CharacterJoint[] joints=new CharacterJoint[5];}
        readonly List<Remains> remains=new List<Remains>(24);readonly Stack<Remains> corpsePool=new Stack<Remains>(24);readonly Stack<UnityBody> projectilePool=new Stack<UnityBody>();readonly List<UnityBody> activeShots=new List<UnityBody>();readonly Dictionary<Collider,int> colliderIds=new Dictionary<Collider,int>();public int ProjectilePoolMisses {get;private set;}public int CorpsePoolMisses {get;private set;}readonly PhysicsMaterial slippery,rough,bouncy,rolling,ice;bool disposed;static int sequence;static bool initialized;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Vector(V3 v){return new Vector3(v.x,v.y,v.z);}[MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 Value(Vector3 v){return new V3(v.x,v.y,v.z);}
        public UnityWorld(Catalog data,Deployment config,bool refined=true)
        {
            this.data=data;this.config=config;this.refined=refined;map=data.Map(config.map);
            // Seed replay worlds still rely on the historical global solver settings.
            if(!initialized){Physics.reuseCollisionCallbacks=true;Physics.gravity=new Vector3(0,-18,0);Physics.defaultSolverIterations=6;Physics.defaultSolverVelocityIterations=1;Physics.IgnoreLayerCollision(CorpseLayer,UnitLayer);Physics.IgnoreLayerCollision(CorpseLayer,ShotLayer);Physics.IgnoreLayerCollision(CorpseLayer,CorpseLayer);Physics.IgnoreLayerCollision(ShotLayer,ShotLayer);initialized=true;}
            scene=SceneManager.CreateScene("FrontlinePhysics-"+(++sequence),new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();root=new GameObject("Battle physics");SceneManager.MoveGameObjectToScene(root,scene);
            ice=new PhysicsMaterial("Highland ice"){dynamicFriction=.015f,staticFriction=.02f,frictionCombine=PhysicsMaterialCombine.Minimum};rolling=new PhysicsMaterial("Horse and wheel rolling contact"){dynamicFriction=.06f,staticFriction=.08f,frictionCombine=PhysicsMaterialCombine.Minimum};slippery=new PhysicsMaterial("Soldier friction"){dynamicFriction=.2f,staticFriction=.2f};rough=new PhysicsMaterial("Ground friction"){dynamicFriction=.8f,staticFriction=.8f};bouncy=new PhysicsMaterial("Bomb bounce"){dynamicFriction=.35f,staticFriction=.35f,bounciness=.35f};
            foreach(var p in map.platforms){var go=Make("Platform",new V3(p.x,p.y,p.z),config.map==FrontierMap.Id?FrontierFloorLayer:GroundLayer);go.transform.rotation=Quaternion.Euler(0,0,p.angle*Mathf.Rad2Deg);Box(go,new Vector3(p.hx*2,p.hy*2,p.hz*2),config.map=="highland"&&p.y>.95f&&p.angle==0?ice:rough);}
            foreach(var p in map.rocks){var go=Make("Rock",new V3(p.x,p.r*.2f+(config.map==FrontierMap.Id?FrontierMap.SurfaceHeight(p.x,p.z):0),p.z),GroundLayer);go.transform.localScale=Vector3.one*p.r*2;go.transform.rotation=Quaternion.Euler(0,CombatMotion.RockYaw(p.x,p.z),0);var col=go.AddComponent<MeshCollider>();col.sharedMesh=Resources.Load<Mesh>("Models/rock_lod1");col.convex=true;col.sharedMaterial=rough;TrackObstacle(go);}
            foreach(var p in map.walls){var go=Make("Wall",new V3(p.x,p.hy,p.z),GroundLayer);Box(go,new Vector3(p.hx*2,p.hy*2,p.hz*2),rough);TrackObstacle(go);}
            if(config.map==FrontierMap.Id)
            {
                var terrain=Resources.Load<FrontierTerrainAsset>(FrontierTerrainAsset.Resource);
                CoastCollider("Manor ground",terrain.manorFloor,FrontierFloorLayer);
                CoastCollider("Frontier ground",terrain.frontierFloor,FrontierFloorLayer);
                // The inward-facing cliffs support pushed infantry without arresting projectiles.
                Physics.IgnoreLayerCollision(ShotLayer,FrontierRimLayer);
                CoastCollider("Shore boundary",terrain.shoreBarrier,FrontierRimLayer);
                var keep=FrontierMap.CastleFootprint;
                Box(Make("Manor castle",new V3(keep.x,keep.hy,keep.z),GroundLayer),
                    new Vector3(keep.hx*2,keep.hy*2,keep.hz*2),rough);
                var gate=Make("Frontier safety boundary",new V3(FrontierMap.Boundary+.08f,5.5f,0),GroundLayer);
                frontierFence=Box(gate,new Vector3(.16f,13,FrontierMap.BridgeHalfWidth*2+1),rough);
            }
            int capacity=0;foreach(var army in new[]{config.red,config.blue})foreach(var u in army){var d=data.Unit(u.type);if(d.ranged)capacity+=(int)Math.Ceiling(6f*1.15f/d.cooldown)+2;}capacity=Math.Min(1024,capacity);activeShots.Capacity=capacity;
            for(int i=0;i<capacity;i++)projectilePool.Push(CreateProjectile());
            for(int i=0;i<24;i++)corpsePool.Push(CreateRemains());
        }
        void CoastCollider(string name,Mesh mesh,int layer)
        {
            var collider=Make(name,new V3(),layer).AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;
            collider.sharedMaterial=rough;
        }
        internal bool SampleFrontierFloor(float x,float z,out float height)
        {
            bool hit=physics.Raycast(new Vector3(x,FrontierMap.TerrainCeiling+.2f,z),Vector3.down,out var contact,FrontierMap.TerrainCeiling+.35f,1<<FrontierFloorLayer);
            height=contact.point.y;
            return hit;
        }
        void TrackObstacle(GameObject go){var relay=go.AddComponent<ObstacleContacts>();relay.world=this;}
        GameObject Make(string name,V3 p,int layer){var go=new GameObject(name);go.layer=layer;go.transform.SetParent(root.transform,false);go.transform.position=Vector(p);return go;}
        static BoxCollider Box(GameObject go,Vector3 size,PhysicsMaterial material){var c=go.AddComponent<BoxCollider>();c.size=size;c.sharedMaterial=material;return c;}
        UnityBody Tag(int id,GameObject go,Rigidbody rb,bool track=true,Collider collider=null)
        {
            var t=go.AddComponent<CollisionTag>();t.id=id;
            var body=new UnityBody(id,go,rb){collider=collider??go.GetComponent<Collider>(),tag=t};
            body.colliders=body.collider.gameObject==go?go.GetComponents<Collider>():new[]{body.collider};
            // Compound props use their stem as the primary collider and include the plinth in grace.
            body.world=this;
            foreach(var col in body.colliders){colliderIds[col]=id;collisionBodies[col]=body;}
            if(track)bodies.Add(id,body);return body;
        }
        public IBody Unit(int id,UnitPlacement p,string team,UnitDef d)
        {
            var go=Make(d.id,new V3(p.x,data.Height(config.map,p.x,p.z)+.8f*d.scale,p.z),UnitLayer);
            if(d.id=="catapult"){var box=Box(go,Vector(CombatMotion.CartSize)*d.scale,rolling);box.center=new Vector3(0,(refined?ContactShape.CartCenterY:-.37f)*d.scale,0);}
            else {
                var col=go.AddComponent<CapsuleCollider>();col.sharedMaterial=slippery;
                if(d.id=="cavalry"){col.sharedMaterial=rolling;col.direction=2;col.radius=.39f*d.scale;col.height=2.32f*d.scale;col.center=new Vector3(0,(refined?ContactShape.HorseCenterY:-.36f)*d.scale,.08f*d.scale);var rider=go.AddComponent<CapsuleCollider>();rider.radius=(refined?.28f:.25f)*d.scale;rider.height=(refined?1.46f:1.24f)*d.scale;rider.center=Vector3.up*(refined?.66f:.5f)*d.scale;rider.sharedMaterial=rolling;}
                else
                {
                    col.radius=(refined?ContactShape.InfantryRadius(d.id):.27f)*d.scale;
                    col.height=(refined?ContactShape.InfantryHeight:1.24f)*d.scale;
                    if(refined)col.center=Vector3.up*ContactShape.InfantryCenterY*d.scale;
                }
            }
            var rb=go.AddComponent<Rigidbody>();rb.mass=d.mass;rb.linearDamping=.6f;rb.constraints=RigidbodyConstraints.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode.Discrete;
            if(refined)ContactShape.SetSolver(rb,10);
            var body=Tag(id,go,rb);body.type=d.id;body.team=team;body.oriented=d.id=="cavalry"||d.id=="catapult";body.fixedRotation=!body.oriented;body.fast=false;if(d.id=="cavalry"){body.sink=go.AddComponent<ProjectileContacts>();body.sink.world=this;body.sink.id=id;body.sink.unit=body;}body.facing=team=="red"?Mathf.PI/2:-Mathf.PI/2;if(body.oriented)rb.rotation=Quaternion.Euler(0,body.facing*Mathf.Rad2Deg,0);
            if(frontierFence&&team=="red")foreach(var collider in body.colliders)Physics.IgnoreCollision(frontierFence,collider);
            return body;
        }
        public IBody Prop(int id,PropPlacement p,PropDef d)
        {
            float h=p.type=="barrel"?.55f:p.type=="barricade"?.7f:p.type=="banner"?.9f:.08f;var go=Make(p.type,new V3(p.x,data.Height(config.map,p.x,p.z)+h,p.z),PropLayer);Collider c;
            if(refined&&p.type=="barrel")
            {
                // Mesh is rooted at grade, while the prop entity and damage origin stay at mid-height.
                var hull=Make("Barrel hull",new V3(),PropLayer);
                hull.transform.SetParent(go.transform,false);
                hull.transform.localPosition=Vector3.down*h;
                var barrel=hull.AddComponent<MeshCollider>();
                barrel.sharedMesh=Resources.Load<Mesh>("Models/prop_barrel_lod1");barrel.convex=true;c=barrel;
            }
            else if(p.type=="barrel"||p.type=="banner")
            {
                var col=go.AddComponent<CapsuleCollider>();
                col.radius=p.type=="barrel"?.42f:refined?ContactShape.BannerRadius:.13f;
                col.height=refined?ContactShape.BannerHeight:h*2;
                if(refined)
                {
                    col.center=Vector3.up*(ContactShape.BannerHeight*.5f-h);
                    var stand=Box(go,ContactShape.BannerStand,rough);
                    stand.center=Vector3.up*(ContactShape.BannerStand.y*.5f-h);
                }
                c=col;
            }
            else c=Box(go,p.type=="barricade"?(refined?ContactShape.BarricadeSize:new Vector3(.76f,1.4f,2.4f)):new Vector3(1.7f,.16f,1.7f),rough);
            if(p.type=="barricade")TrackObstacle(go);c.isTrigger=p.type=="spikes"||p.type=="spring";c.sharedMaterial=rough;var body=Tag(id,go,null,collider:c);body.type=p.type;body.team=p.team;if(p.type=="banner")banners.Add(body);return body;
        }
        public int ColliderId(Collider collider){return colliderIds.TryGetValue(collider,out var id)?id:0;}
        UnityBody CreateProjectile()
        {
            var go=Make("Pooled projectile",new V3(),ShotLayer);go.SetActive(false);var col=go.AddComponent<SphereCollider>();var rb=go.AddComponent<Rigidbody>();rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.useGravity=false;
            var body=Tag(0,go,rb,false);body.pooled=true;body.sink=go.AddComponent<ProjectileContacts>();body.sink.world=this;body.ignored.Capacity=3+(refined?2:1)*(config.props.Count+map.props.Count);return body;
        }
        public IBody Projectile(int id,string type,V3 p,V3 velocity,float gravityScale,int ownerId)
        {
            // Capacity is the maximum six-second firing rate of this roster, including inspire.
            // The safety path is instrumented; acceptance checks require zero misses.
            UnityBody body;if(projectilePool.Count>0)body=projectilePool.Pop();else{ProjectilePoolMisses++;throw new InvalidOperationException("Projectile pool capacity exceeded");}
            body.ResetId(id);colliderIds[body.collider]=id;var col=(SphereCollider)body.collider;col.radius=type=="bomb"?.18f:type=="stone"?.28f:.09f;col.sharedMaterial=type=="bomb"?bouncy:slippery;
            var rb=body.rigidbody;body.gameObject.transform.SetPositionAndRotation(Vector(p),Quaternion.identity);body.gameObject.SetActive(true);rb.position=Vector(p);rb.rotation=Quaternion.identity;rb.mass=type=="bomb"?1.5f:type=="stone"?4:.15f;rb.linearVelocity=Vector(velocity);rb.angularVelocity=Vector3.zero;body.gravity=gravityScale;rb.useGravity=gravityScale==1;
            rb.WakeUp();bodies.Add(id,body);activeShots.Add(body);
            body.ignored.Clear();body.ownerColliderCount=0;body.launchAge=0;
            if(bodies.TryGetValue(ownerId,out var owner)){
                foreach(var c in owner.colliders){body.ignored.Add(c);Physics.IgnoreCollision(col,c,true);}body.ownerColliderCount=body.ignored.Count;
                foreach(var banner in banners)if(banner.gameObject.activeSelf&&(banner.team==owner.team||banner.team=="neutral")&&(banner.Position-p).Planar<1.15f)
                    foreach(var cover in banner.colliders){body.ignored.Add(cover);Physics.IgnoreCollision(col,cover,true);}
            }return body;
        }
        public void Remove(IBody value)
        {
            var b=(UnityBody)value;bodies.Remove(b.Id);foreach(var col in b.colliders){colliderIds.Remove(col);collisionBodies.Remove(col);}b.gameObject.SetActive(false);
            if(b.pooled){foreach(var c in b.ignored)if(c)Physics.IgnoreCollision(b.collider,c,false);b.ignored.Clear();activeShots.Remove(b);b.rigidbody.linearVelocity=Vector3.zero;b.rigidbody.angularVelocity=Vector3.zero;b.ResetId(0);projectilePool.Push(b);}
            // Units and props remain inactive under the battle root until scene disposal.
        }
        public void UnitContact(UnityBody body,Collision collision)
        {
            if(!body.fast)return;var collider=collision.collider;int layer=collider.gameObject.layer;
            if(layer==GroundLayer||layer==FrontierFloorLayer||layer==FrontierRimLayer){FilteredGroundContacts++;return;}
            if(layer!=UnitLayer)return;
            if(!collisionBodies.TryGetValue(collider,out var other)||other.team==body.team){FilteredFriendlyContacts++;return;}
            Contact(body.Id,other.Id);RecordedChargeContacts++;
        }
        public void ObstacleContact(Collision collision)
        {
            var collider=collision.collider;if(collider.gameObject.layer!=UnitLayer)return;
            if(!collisionBodies.TryGetValue(collider,out var body)||StepIndex>body.wallUntil||StepIndex-body.lastWall<12||collision.contactCount==0)return;
            var point=collision.GetContact(0);var relative=collision.relativeVelocity;float speed=new Vector2(relative.x,relative.z).magnitude;
            if(Mathf.Abs(point.normal.y)>.55f||speed<5)return;body.lastWall=StepIndex;body.wallUntil=-1;
            if(wallImpacts.Count<24){wallImpacts.Add(new WallImpact{id=body.Id,point=Value(point.point),normal=Value(-point.normal),speed=speed});RecordedWallImpacts++;}
        }
        public List<WallImpact> WallImpacts(){if(wallImpacts.Count>1)wallImpacts.Sort((a,b)=>a.id.CompareTo(b.id));return wallImpacts;}
        public void Contact(int a,int b){int low=Math.Min(a,b),high=Math.Max(a,b);ulong pair=((ulong)(uint)low<<32)|(uint)high;if(pairs.Add(pair))contacts.Add(new Contact{a=low,b=high});}
        public void Step(float dt){contacts.Clear();pairs.Clear();wallImpacts.Clear();for(int i=0;i<activeShots.Count;i++){var p=activeShots[i];if(p.gravity!=1&&p.gravity!=0)p.rigidbody.AddForce(Physics.gravity*p.gravity,ForceMode.Acceleration);p.launchAge+=dt;
                if(p.launchAge>=.10f&&p.ignored.Count>p.ownerColliderCount){for(int j=p.ownerColliderCount;j<p.ignored.Count;j++)if(p.ignored[j])Physics.IgnoreCollision(p.collider,p.ignored[j],false);p.ignored.RemoveRange(p.ownerColliderCount,p.ignored.Count-p.ownerColliderCount);}}if(MeasurePhysics){long start=System.Diagnostics.Stopwatch.GetTimestamp();physics.Simulate(dt);PhysicsTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-start;}else physics.Simulate(dt);StepIndex++;}
        public List<Contact> Contacts(){if(contacts.Count>1)contacts.Sort((a,b)=>a.a!=b.a?a.a.CompareTo(b.a):a.b.CompareTo(b.b));return contacts;}
        static readonly string[] Names={"torso","head","armL","armR","legL","legR"};
        static readonly Vector3[] Offsets={new Vector3(0,0,0),new Vector3(0,.53f,0),new Vector3(-.4f,-.02f,0),new Vector3(.4f,-.02f,0),new Vector3(-.16f,-.57f,0),new Vector3(.16f,-.57f,0)};
        static readonly Vector3[] Extents={new Vector3(.23f,.32f,.16f),new Vector3(.2f,.2f,.2f),new Vector3(.11f,.3f,.11f),new Vector3(.11f,.3f,.11f),new Vector3(.12f,.3f,.13f),new Vector3(.12f,.3f,.13f)};
        static readonly Vector3[] Anchors={new Vector3(0,.32f,0),new Vector3(-.25f,.2f,0),new Vector3(.25f,.2f,0),new Vector3(-.16f,-.3f,0),new Vector3(.16f,-.3f,0)};
        static readonly Vector3[] Connected={new Vector3(0,-.2f,0),new Vector3(.12f,.2f,0),new Vector3(-.12f,.2f,0),new Vector3(0,.27f,0),new Vector3(0,.27f,0)};
        Remains CreateRemains()
        {
            var r=new Remains();
            for(int i=0;i<6;i++)
            {
                var go=Make(Names[i],new V3(),CorpseLayer);go.SetActive(false);
                var collider=Box(go,Extents[i]*2,rough);
                var rb=go.AddComponent<Rigidbody>();
                rb.mass=i==0?2:.5f;rb.linearDamping=.25f;rb.angularDamping=.8f;rb.sleepThreshold=.08f;
                if(refined)
                {
                    ContactShape.SetSolver(rb,4.5f);
                    rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                }
                r.parts.Add(new UnityBody(0,go,rb){collider=collider});
                if(i==0)continue;
                var joint=go.AddComponent<CharacterJoint>();joint.connectedBody=r.parts[0].rigidbody;
                joint.autoConfigureConnectedAnchor=false;joint.enableCollision=false;r.joints[i-1]=joint;
                if(refined)
                {
                    joint.lowTwistLimit=new SoftJointLimit{limit=i==1?-28:-42};
                    joint.highTwistLimit=new SoftJointLimit{limit=i==1?28:42};
                    joint.swing1Limit=new SoftJointLimit{limit=i==1?31:58};
                    joint.swing2Limit=new SoftJointLimit{limit=i==1?25:38};
                    joint.enableProjection=true;joint.projectionDistance=.065f;joint.projectionAngle=16;
                }
            }
            return r;
        }
        public void Corpse(int id,string type,string team,UnitDef def,V3 position,V3 velocity,V3 impulse)
        {
            if(remains.Count>=24)Discard(remains[0]);Remains r;if(corpsePool.Count>0)r=corpsePool.Pop();else{CorpsePoolMisses++;throw new InvalidOperationException("Corpse pool capacity exceeded");}r.id=id;r.type=type;r.team=team;r.age=0;r.settleAt=.5f;if(r.settling)Settle(r,false);float floor=data.Height(config.map,position.x,position.z);r.hasWreck=(type=="cavalry"||type=="catapult")&&Catalog.Finite(floor)&&
                (!refined||CanPlaceWreck(position,floor,def.scale));r.wreck[0]=position.x;r.wreck[1]=floor+.08f;r.wreck[2]=position.z;r.wreck[3]=Mathf.Atan2(velocity.x,velocity.z);
            if(refined&&Catalog.Finite(floor)&&position.y>floor-def.scale)
                position.y+=CorpseLift(position,def.scale);
            for(int i=0;i<6;i++)
            {
                var body=r.parts[i];body.ResetId(id);var rb=body.rigidbody;
                var at=Vector(position)+Offsets[i]*def.scale;
                body.gameObject.transform.SetPositionAndRotation(at,Quaternion.identity);
                rb.position=at;rb.rotation=Quaternion.identity;
                ((BoxCollider)body.collider).size=CorpseExtents(i)*2*def.scale;
                rb.linearVelocity=Vector(velocity);rb.angularVelocity=Vector3.zero;
                if(i>0)
                {
                    var anchor=Connected[i-1];
                    r.joints[i-1].anchor=anchor*def.scale;
                    r.joints[i-1].connectedAnchor=(refined?Offsets[i]+anchor:Anchors[i-1])*def.scale;
                }
            }
            for(int i=0;i<6;i++){var body=r.parts[i];body.gameObject.SetActive(true);body.rigidbody.linearVelocity=Vector(velocity);body.rigidbody.angularVelocity=Vector3.zero;body.rigidbody.WakeUp();body.rigidbody.AddForce(Vector(impulse)*.12f,ForceMode.Impulse);}remains.Add(r);
        }
        Vector3 CorpseExtents(int index)=>refined&&index==1?new Vector3(.2f,.22f,.2f):Extents[index];

        float CorpseLift(V3 position,float scale)
        {
            float lift=0;
            for(int i=0;i<Offsets.Length;i++)
            {
                var p=Vector(position)+Offsets[i]*scale;
                var extent=CorpseExtents(i)*scale;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                {
                    float ground=data.Height(config.map,p.x+x*extent.x,p.z+z*extent.z);
                    if(Catalog.Finite(ground))lift=Mathf.Max(lift,ground+.025f-(p.y-extent.y));
                }
            }
            return lift;
        }

        bool CanPlaceWreck(V3 position,float floor,float scale)
        {
            const float wreckRadius=1.52f;
            float r=wreckRadius*scale;
            foreach(var wall in map.walls)
                if(Mathf.Abs(position.x-wall.x)<wall.hx+r&&Mathf.Abs(position.z-wall.z)<wall.hz+r)return false;
            foreach(var rock in map.rocks)
                if(Catalog.Distance(position.x,position.z,rock.x,rock.z)<rock.r+r)return false;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                float h=data.Height(config.map,position.x+x*r,position.z+z*r);
                if(!Catalog.Finite(h)||Mathf.Abs(h-floor)>.27f)return false;
            }
            return true;
        }

        void Discard(Remains r){foreach(var b in r.parts){b.gameObject.SetActive(false);b.rigidbody.linearVelocity=Vector3.zero;b.rigidbody.angularVelocity=Vector3.zero;}remains.Remove(r);corpsePool.Push(r);}
        static void Settle(Remains r,bool settling){r.settling=settling;foreach(var b in r.parts){b.rigidbody.linearDamping=settling?.85f:.25f;b.rigidbody.angularDamping=settling?3f:.8f;}}
        public void PushCorpses(V3 p,float radius)
        {
            foreach(var r in remains){bool strong=false;foreach(var b in r.parts){var delta=b.Position-p;float distance=delta.Length;if(distance<radius&&distance>.001f){float force=(1-distance/radius)*4;b.Impulse((delta.Normalized+new V3(0,.4f,0))*force);if(force>.6f)strong=true;}}
                if(strong){r.settleAt=r.age+.5f;if(r.settling)Settle(r,false);}}
        }
        public void AgeCorpses(float dt){for(int i=remains.Count-1;i>=0;i--){var r=remains[i];r.age+=dt;if(r.age>12)Discard(r);else if(!r.settling&&r.age>=r.settleAt)Settle(r,true);}}
        public List<CorpseFrame> Corpses(List<CorpseFrame> reuse=null)
        {
            var list=reuse??new List<CorpseFrame>(24);while(list.Count<remains.Count)list.Add(new CorpseFrame());
            for(int n=0;n<remains.Count;n++){var r=remains[n];var frame=list[n];frame.id=r.id;frame.type=r.type;frame.team=r.team;frame.age=r.age;if(r.hasWreck){if(frame.wreckStorage==null)frame.wreckStorage=new float[4];frame.wreck=frame.wreckStorage;Array.Copy(r.wreck,frame.wreck,4);}else frame.wreck=null;
                while(frame.parts.Count<6)frame.parts.Add(new CorpsePart());for(int i=0;i<6;i++){var part=frame.parts[i];if(part.name==null)part.name=Names[i];part.t=r.parts[i].Transform(part.t);}}
            if(list.Count>remains.Count)list.RemoveRange(remains.Count,list.Count-remains.Count);return list;
        }
        public void Dispose(){if(disposed)return;disposed=true;root.SetActive(false);SceneManager.UnloadSceneAsync(scene);Object.Destroy(slippery);Object.Destroy(rough);Object.Destroy(bouncy);Object.Destroy(rolling);Object.Destroy(ice);bodies.Clear();activeShots.Clear();projectilePool.Clear();corpsePool.Clear();colliderIds.Clear();collisionBodies.Clear();wallImpacts.Clear();remains.Clear();}
    }
}
