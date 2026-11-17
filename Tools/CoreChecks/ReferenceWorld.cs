// Rule-test double outside Unity/Assets. Never shipped; real PhysX is checked in Unity.
using System;using System.Collections.Generic;using Frontline.Core;
sealed class ReferenceFactory:IWorldFactory {public int live,created;public IPhysicsWorld Create(Catalog c,Deployment d){live++;created++;return new ReferenceWorld(this,c,d);}}
sealed class ReferenceWorld:IPhysicsWorld
{
 sealed class Body:IBody {public int Id{get;set;}public V3 p,v;public float mass=1,height=.8f,gravity=1;public bool fixedBody;public V3 Position=>p;public V3 Velocity=>v;public void Impulse(V3 f){if(!fixedBody)v+=f/mass;}public float[] Transform(float[] reuse=null){var t=reuse??new float[7];t[0]=p.x;t[1]=p.y;t[2]=p.z;t[3]=t[4]=t[5]=0;t[6]=1;return t;}}
 readonly ReferenceFactory owner;readonly Catalog data;readonly Deployment config;readonly List<Body> bodies=new List<Body>();bool disposed;
 public ReferenceWorld(ReferenceFactory owner,Catalog data,Deployment config){this.owner=owner;this.data=data;this.config=config;}
 public IBody Unit(int id,UnitPlacement p,string team,UnitDef d){var b=new Body{Id=id,p=new V3(p.x,data.Height(config.map,p.x,p.z)+.8f*d.scale,p.z),mass=d.mass,height=.8f*d.scale};bodies.Add(b);return b;}
 public IBody Prop(int id,PropPlacement p,PropDef d){var b=new Body{Id=id,p=new V3(p.x,data.Height(config.map,p.x,p.z)+.5f,p.z),fixedBody=true};bodies.Add(b);return b;}
 public IBody Projectile(int id,string type,V3 p,V3 velocity,float gravityScale,int ownerId){var b=new Body{Id=id,p=p,v=velocity,height=0,gravity=gravityScale};bodies.Add(b);return b;}
 public void Remove(IBody b){bodies.Remove((Body)b);}public void Corpse(int id,string type,string team,UnitDef def,V3 p,V3 v,V3 impulse){}public void PushCorpses(V3 p,float radius){}public void AgeCorpses(float dt){}public List<CorpseFrame> Corpses(List<CorpseFrame> reuse=null){var result=reuse??new List<CorpseFrame>();result.Clear();return result;}public List<Contact> Contacts(){return new List<Contact>();}
 public void Step(float dt){foreach(var b in bodies)if(!b.fixedBody){b.v+=new V3(0,-18*b.gravity*dt,0);b.p+=b.v*dt;float h=data.Height(config.map,b.p.x,b.p.z)+b.height;if(b.p.y<h){b.p.y=h;b.v.y=0;}b.v*=.99f;}}
 public void Dispose(){if(disposed)return;disposed=true;owner.live--;bodies.Clear();}
}
