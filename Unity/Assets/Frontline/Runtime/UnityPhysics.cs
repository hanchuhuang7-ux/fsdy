using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Frontline.UnityGame
{
    public sealed class UnityWorldFactory:IWorldFactory {public IPhysicsWorld Create(Catalog data,Deployment config){return new UnityWorld(data,config);}}
    public sealed class CollisionTag:MonoBehaviour {public int id;}
    public sealed class ProjectileContacts:MonoBehaviour
    {
        public int id;[NonSerialized]public UnityWorld world;
        void OnCollisionEnter(Collision c){var other=c.collider.GetComponent<CollisionTag>();world?.Contact(id,other==null?0:other.id);}
    }
    public sealed class UnityBody:IBody
    {
        public int Id {get;private set;}public readonly GameObject gameObject;public readonly Rigidbody rigidbody;
        public UnityBody(int id,GameObject go,Rigidbody rb){Id=id;gameObject=go;rigidbody=rb;}
        public V3 Position {get{return UnityWorld.Value(rigidbody?rigidbody.position:gameObject.transform.position);}}
        public V3 Velocity {get{return rigidbody?UnityWorld.Value(rigidbody.linearVelocity):new V3();}}
        public void Impulse(V3 v){if(rigidbody)rigidbody.AddForce(UnityWorld.Vector(v),ForceMode.Impulse);}
        public float[] Transform(float[] reuse=null){var t=reuse!=null&&reuse.Length==7?reuse:new float[7];var p=rigidbody?rigidbody.position:gameObject.transform.position;var q=rigidbody?rigidbody.rotation:gameObject.transform.rotation;t[0]=p.x;t[1]=p.y;t[2]=p.z;t[3]=q.x;t[4]=q.y;t[5]=q.z;t[6]=q.w;return t;}
    }
    public sealed class UnityWorld:IPhysicsWorld
    {
        const int GroundLayer=8,UnitLayer=9,ShotLayer=10,PropLayer=11,CorpseLayer=12;
        readonly Scene scene;readonly PhysicsScene physics;readonly GameObject root;readonly Catalog data;readonly Deployment config;
        readonly Dictionary<int,UnityBody> bodies=new Dictionary<int,UnityBody>();readonly List<Contact> contacts=new List<Contact>();readonly HashSet<ulong> pairs=new HashSet<ulong>();
        sealed class Remains {public int id;public string type,team;public float age;public float[] wreck;public readonly List<UnityBody> parts=new List<UnityBody>();}
        readonly List<Remains> remains=new List<Remains>();readonly PhysicsMaterial slippery,rough,bouncy;bool disposed;static int sequence;static bool initialized;
        public static Vector3 Vector(V3 v){return new Vector3(v.x,v.y,v.z);}public static V3 Value(Vector3 v){return new V3(v.x,v.y,v.z);}
        public UnityWorld(Catalog data,Deployment config)
        {
            this.data=data;this.config=config;
            if(!initialized){Physics.gravity=new Vector3(0,-18,0);Physics.defaultSolverIterations=6;Physics.defaultSolverVelocityIterations=1;Physics.IgnoreLayerCollision(CorpseLayer,UnitLayer);Physics.IgnoreLayerCollision(CorpseLayer,ShotLayer);Physics.IgnoreLayerCollision(CorpseLayer,CorpseLayer);Physics.IgnoreLayerCollision(ShotLayer,ShotLayer);initialized=true;}
            scene=SceneManager.CreateScene("FrontlinePhysics-"+(++sequence),new CreateSceneParameters(LocalPhysicsMode.Physics3D));physics=scene.GetPhysicsScene();root=new GameObject("Battle physics");SceneManager.MoveGameObjectToScene(root,scene);
            slippery=new PhysicsMaterial("Soldier friction"){dynamicFriction=.2f,staticFriction=.2f};rough=new PhysicsMaterial("Ground friction"){dynamicFriction=.8f,staticFriction=.8f};bouncy=new PhysicsMaterial("Bomb bounce"){dynamicFriction=.35f,staticFriction=.35f,bounciness=.35f};
            foreach(var p in data.Maps[config.map].platforms){var go=Make("Platform",new V3(p.x,p.y,p.z),GroundLayer);go.transform.rotation=Quaternion.Euler(0,0,p.angle*Mathf.Rad2Deg);Box(go,new Vector3(p.hx*2,p.hy*2,p.hz*2),rough);}
            foreach(var p in data.Maps[config.map].rocks){var go=Make("Rock",new V3(p.x,p.r*.2f,p.z),GroundLayer);var col=go.AddComponent<SphereCollider>();col.radius=p.r;col.sharedMaterial=rough;}
            foreach(var p in data.Maps[config.map].walls){var go=Make("Wall",new V3(p.x,p.hy,p.z),GroundLayer);Box(go,new Vector3(p.hx*2,p.hy*2,p.hz*2),rough);}
        }
        GameObject Make(string name,V3 p,int layer){var go=new GameObject(name);go.layer=layer;go.transform.SetParent(root.transform,false);go.transform.position=Vector(p);return go;}
        static BoxCollider Box(GameObject go,Vector3 size,PhysicsMaterial material){var c=go.AddComponent<BoxCollider>();c.size=size;c.sharedMaterial=material;return c;}
        UnityBody Tag(int id,GameObject go,Rigidbody rb,bool track=true){var t=go.AddComponent<CollisionTag>();t.id=id;var body=new UnityBody(id,go,rb);if(track)bodies.Add(id,body);return body;}
        public IBody Unit(int id,UnitPlacement p,string team,UnitDef d)
        {
            var go=Make(d.id,new V3(p.x,data.Height(config.map,p.x,p.z)+.8f*d.scale,p.z),UnitLayer);var collider=go.AddComponent<CapsuleCollider>();collider.radius=.27f*d.scale;collider.height=1.24f*d.scale;collider.sharedMaterial=slippery;
            var rb=go.AddComponent<Rigidbody>();rb.mass=d.mass;rb.linearDamping=.6f;rb.constraints=RigidbodyConstraints.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode.Discrete;return Tag(id,go,rb);
        }
        public IBody Prop(int id,PropPlacement p,PropDef d)
        {
            float h=p.type=="barrel"?.55f:p.type=="barricade"?.7f:p.type=="banner"?.9f:.08f;var go=Make(p.type,new V3(p.x,data.Height(config.map,p.x,p.z)+h,p.z),PropLayer);Collider c;
            if(p.type=="barrel"||p.type=="banner"){var col=go.AddComponent<CapsuleCollider>();col.radius=p.type=="barrel"?.42f:.13f;col.height=h*2;c=col;}
            else c=Box(go,p.type=="barricade"?new Vector3(.76f,1.4f,2.4f):new Vector3(1.7f,.16f,1.7f),rough);
            c.isTrigger=p.type=="spikes"||p.type=="spring";c.sharedMaterial=rough;return Tag(id,go,null);
        }
        public IBody Projectile(int id,string type,V3 p,V3 velocity,float gravityScale,int ownerId)
        {
            var go=Make(type,p,ShotLayer);var col=go.AddComponent<SphereCollider>();col.radius=type=="bomb"?.18f:type=="stone"?.28f:.09f;col.sharedMaterial=type=="bomb"?bouncy:slippery;
            var rb=go.AddComponent<Rigidbody>();rb.mass=type=="bomb"?1.5f:type=="stone"?4:.15f;rb.linearVelocity=Vector(velocity);rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.useGravity=false;
            var body=Tag(id,go,rb);var sink=go.AddComponent<ProjectileContacts>();sink.id=id;sink.world=this;gravities.Add(body,gravityScale);if(bodies.TryGetValue(ownerId,out var owner))foreach(var ownCol in owner.gameObject.GetComponents<Collider>())Physics.IgnoreCollision(col,ownCol);return body;
        }
        readonly Dictionary<UnityBody,float> gravities=new Dictionary<UnityBody,float>();
        public void Remove(IBody value){var b=(UnityBody)value;bodies.Remove(b.Id);gravities.Remove(b);if(b.gameObject){b.gameObject.SetActive(false);Object.Destroy(b.gameObject);}}
        public void Contact(int a,int b){int low=Math.Min(a,b),high=Math.Max(a,b);ulong pair=((ulong)(uint)low<<32)|(uint)high;if(pairs.Add(pair))contacts.Add(new Contact{a=low,b=high});}
        public void Step(float dt){contacts.Clear();pairs.Clear();foreach(var p in gravities)if(p.Key.rigidbody)p.Key.rigidbody.AddForce(Physics.gravity*p.Value,ForceMode.Acceleration);physics.Simulate(dt);}
        public List<Contact> Contacts(){contacts.Sort((a,b)=>a.a!=b.a?a.a.CompareTo(b.a):a.b.CompareTo(b.b));return contacts;}
        static readonly string[] Names={"torso","head","armL","armR","legL","legR"};
        static readonly Vector3[] Offsets={new Vector3(0,0,0),new Vector3(0,.53f,0),new Vector3(-.4f,-.02f,0),new Vector3(.4f,-.02f,0),new Vector3(-.16f,-.57f,0),new Vector3(.16f,-.57f,0)};
        static readonly Vector3[] Extents={new Vector3(.23f,.32f,.16f),new Vector3(.2f,.2f,.2f),new Vector3(.11f,.3f,.11f),new Vector3(.11f,.3f,.11f),new Vector3(.12f,.3f,.13f),new Vector3(.12f,.3f,.13f)};
        static readonly Vector3[] Anchors={new Vector3(0,.32f,0),new Vector3(-.25f,.2f,0),new Vector3(.25f,.2f,0),new Vector3(-.16f,-.3f,0),new Vector3(.16f,-.3f,0)};
        static readonly Vector3[] Connected={new Vector3(0,-.2f,0),new Vector3(.12f,.2f,0),new Vector3(-.12f,.2f,0),new Vector3(0,.27f,0),new Vector3(0,.27f,0)};
        public void Corpse(int id,string type,string team,UnitDef def,V3 position,V3 velocity,V3 impulse)
        {
            if(remains.Count>=24)Discard(remains[0]);float wreckFloor=data.Height(config.map,position.x,position.z);var r=new Remains{id=id,type=type,team=team,wreck=(type=="cavalry"||type=="catapult")&&Catalog.Finite(wreckFloor)?new[]{position.x,wreckFloor+.08f,position.z,Mathf.Atan2(velocity.x,velocity.z)}:null};remains.Add(r);
            for(int i=0;i<6;i++){var go=Make(Names[i],position+Value(Offsets[i]*def.scale),CorpseLayer);Box(go,Extents[i]*2*def.scale,rough);var rb=go.AddComponent<Rigidbody>();rb.mass=i==0?2:.5f;rb.linearDamping=.25f;rb.angularDamping=.8f;rb.linearVelocity=Vector(velocity);rb.AddForce(Vector(impulse)*.12f,ForceMode.Impulse);r.parts.Add(new UnityBody(id,go,rb));if(i>0){var joint=go.AddComponent<CharacterJoint>();joint.connectedBody=r.parts[0].rigidbody;joint.autoConfigureConnectedAnchor=false;joint.anchor=Connected[i-1]*def.scale;joint.connectedAnchor=Anchors[i-1]*def.scale;joint.enableCollision=false;}}
        }
        void Discard(Remains r){foreach(var b in r.parts)if(b.gameObject){b.gameObject.SetActive(false);Object.Destroy(b.gameObject);}remains.Remove(r);}
        public void PushCorpses(V3 p,float radius){foreach(var r in remains)foreach(var b in r.parts){var delta=b.Position-p;float distance=delta.Length;if(distance<radius&&distance>.001f)b.Impulse((delta.Normalized+new V3(0,.4f,0))*((1-distance/radius)*4));}}
        public void AgeCorpses(float dt){for(int i=remains.Count-1;i>=0;i--){remains[i].age+=dt;if(remains[i].age>12)Discard(remains[i]);}}
        public List<CorpseFrame> Corpses(){var list=new List<CorpseFrame>();foreach(var r in remains){var f=new CorpseFrame{id=r.id,type=r.type,team=r.team,age=r.age,wreck=r.wreck};for(int i=0;i<r.parts.Count;i++)f.parts.Add(new CorpsePart{name=Names[i],t=r.parts[i].Transform()});list.Add(f);}return list;}
        public void Dispose(){if(disposed)return;disposed=true;root.SetActive(false);SceneManager.UnloadSceneAsync(scene);Object.Destroy(slippery);Object.Destroy(rough);Object.Destroy(bouncy);bodies.Clear();gravities.Clear();remains.Clear();}
    }
}




