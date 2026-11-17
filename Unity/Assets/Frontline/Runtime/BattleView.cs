using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed class BattleView:MonoBehaviour
    {
        public Camera ViewCamera {get;private set;}[NonSerialized]public Catalog Data;public Deployment Config;public Frame Current;public string HiddenTeam;
        public List<int> Selection=new List<int>();[NonSerialized]public Vector3? Marker;public float MarkerRadius=1;public bool Deploying=true;public int CameraMode;
        enum Tint {Red,Blue,Skin,Wood,Steel,Gold,White,Ink,Ground,Edge,Rock,Grass,Mud,Ice,RedLight,BlueLight,Heal,Blast,Water}
        readonly Color[] colors={new Color(.86f,.27f,.23f),new Color(.22f,.51f,.83f),new Color(.92f,.72f,.51f),new Color(.42f,.27f,.17f),new Color(.56f,.64f,.67f),new Color(.98f,.72f,.25f),new Color(.9f,.92f,.86f),new Color(.12f,.18f,.22f),new Color(.54f,.64f,.46f),new Color(.28f,.37f,.34f),new Color(.47f,.52f,.52f),new Color(.41f,.53f,.34f),new Color(.39f,.31f,.24f),new Color(.62f,.81f,.84f),new Color(1,.63f,.44f),new Color(.48f,.76f,1),new Color(.41f,.84f,.57f),new Color(1,.48f,.13f),new Color(.13f,.24f,.32f)};
        sealed class Batch {public Mesh mesh;public Material material;public Matrix4x4[] matrices=new Matrix4x4[1023];public int count;}
        readonly Dictionary<int,Batch> batches=new Dictionary<int,Batch>();Mesh cube,sphere,cylinder;Material[] materials;Vector3 target=Vector3.zero;float yaw=0,distance=40,pitch=51;int cameraWidth,cameraHeight,lastMapColor=-1;
        void Awake()
        {
            ViewCamera=new GameObject("Battle camera").AddComponent<Camera>();ViewCamera.transform.SetParent(transform,false);ViewCamera.backgroundColor=new Color(.075f,.12f,.17f);ViewCamera.clearFlags=CameraClearFlags.SolidColor;ViewCamera.fieldOfView=Viewport.Compact?65:42;ViewCamera.nearClipPlane=.1f;ViewCamera.farClipPlane=180;ViewCamera.gameObject.AddComponent<AudioListener>();
            var light=new GameObject("Warm sun").AddComponent<Light>();light.transform.rotation=Quaternion.Euler(52,-28,0);light.type=LightType.Directional;light.intensity=1;light.shadows=LightShadows.Soft;light.shadowStrength=.4f;light.shadowBias=.045f;RenderSettings.ambientLight=new Color(.48f,.54f,.59f);QualitySettings.shadowDistance=65;QualitySettings.shadows=ShadowQuality.All;
            cube=Resources.Load<Mesh>("Cube");sphere=Resources.Load<Mesh>("Sphere");cylinder=Resources.Load<Mesh>("Cylinder");materials=new Material[colors.Length];var shader=Resources.Load<Shader>("FrontlineInstanced");for(int i=0;i<materials.Length;i++){materials[i]=new Material(shader){enableInstancing=true};materials[i].color=colors[i];}
        }
        static Mesh Primitive(PrimitiveType type){var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;go.SetActive(false);Destroy(go);return mesh;}
        void Add(Mesh mesh,Tint color,Matrix4x4 matrix)
        {
            int key=(mesh==cube?1:mesh==sphere?2:3)*31+(int)color;if(!batches.TryGetValue(key,out var b)){b=new Batch{mesh=mesh,material=materials[(int)color]};batches.Add(key,b);}if(b.count==1023){Draw(b);b.count=0;}b.matrices[b.count++]=matrix;
        }
        void Draw(Batch b){if(b.count>0)Graphics.DrawMeshInstanced(b.mesh,0,b.material,b.matrices,b.count,null,ShadowCastingMode.On,true,0,ViewCamera);}
        void Box(Vector3 p,Vector3 scale,Tint color,Quaternion rotation=default){if(rotation==default)rotation=Quaternion.identity;Add(cube,color,Matrix4x4.TRS(p,rotation,scale));}
        void Ball(Vector3 p,Vector3 scale,Tint color){Add(sphere,color,Matrix4x4.TRS(p,Quaternion.identity,scale));}
        void Rod(Vector3 a,Vector3 b,float width,Tint color){var d=b-a;if(d.sqrMagnitude>.0001f)Add(cylinder,color,Matrix4x4.TRS((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,d.normalized),new Vector3(width,d.magnitude*.5f,width)));}
        void LocalBox(Matrix4x4 root,Vector3 p,Vector3 scale,Tint color,Quaternion rotation=default){if(rotation==default)rotation=Quaternion.identity;Add(cube,color,root*Matrix4x4.TRS(p,rotation,scale));}
        void LocalBall(Matrix4x4 root,Vector3 p,Vector3 scale,Tint color){Add(sphere,color,root*Matrix4x4.TRS(p,Quaternion.identity,scale));}
        void Ring(Vector3 center,float radius,Tint tint){const int count=36;for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Rod(center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius,center+new Vector3(Mathf.Sin(b),0,Mathf.Cos(b))*radius,.045f,tint);}}
        void Soldier(string type,string team,Vector3 p,float angle,float attack,float scale,int id,float hp=1,bool inspired=false,bool fortified=false,string stance="advance")
        {
            Tint color=team=="red"?Tint.Red:Tint.Blue;var q=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);var root=Matrix4x4.TRS(p,q,Vector3.one*scale);float walk=Deploying?0:Mathf.Sin((Current?.time??Time.time)*8+id)*.18f;float kick=Mathf.Sin(Mathf.Clamp01(attack)*Mathf.PI)*.6f;
            LocalBox(root,new Vector3(0,0,0),new Vector3(.48f,.63f,.34f),color);LocalBall(root,new Vector3(0,.54f,0),new Vector3(.4f,.43f,.38f),Tint.Skin);LocalBox(root,new Vector3(0,.7f,0),new Vector3(.44f,.13f,.43f),type=="militia"?Tint.Wood:Tint.Steel);
            LocalBox(root,new Vector3(-.15f,-.53f,walk),new Vector3(.2f,.48f,.23f),Tint.Ink);LocalBox(root,new Vector3(.15f,-.53f,-walk),new Vector3(.2f,.48f,.23f),Tint.Ink);
            LocalBox(root,new Vector3(-.36f,.03f,.06f),new Vector3(.16f,.5f,.18f),Tint.Skin,Quaternion.Euler(-15,0,10));LocalBox(root,new Vector3(.36f,.03f,.15f+kick*.1f),new Vector3(.16f,.5f,.18f),Tint.Skin,Quaternion.Euler(-kick*65,0,-10));
            if(type=="shield"){LocalBox(root,new Vector3(-.38f,0,.33f),new Vector3(.5f,.75f,.12f),color);LocalBox(root,new Vector3(-.38f,0,.405f),new Vector3(.1f,.7f,.02f),Tint.Steel);LocalBox(root,new Vector3(.4f,.15f,.5f),new Vector3(.09f,.08f,.8f),Tint.Steel);}
            else if(type=="spear"){LocalBox(root,new Vector3(.4f,.15f,.62f),new Vector3(.055f,.055f,2.2f),Tint.Wood);LocalBox(root,new Vector3(.4f,.15f,1.8f),new Vector3(.13f,.1f,.35f),Tint.Steel);}
            else if(type=="archer"){LocalBox(root,new Vector3(.35f,.1f,.55f),new Vector3(.06f,.9f,.08f),Tint.Wood,Quaternion.Euler(0,0,12));LocalBox(root,new Vector3(.35f,.1f,.75f),new Vector3(.035f,.035f,.6f),Tint.Gold);}
            else if(type=="crossbow"){LocalBox(root,new Vector3(.3f,.05f,.54f),new Vector3(.75f,.08f,.1f),Tint.Steel);LocalBox(root,new Vector3(.3f,.05f,.37f),new Vector3(.1f,.13f,.65f),Tint.Wood);}
            else if(type=="bomber"){LocalBall(root,new Vector3(.43f,.02f,.5f),Vector3.one*.3f,Tint.Ink);LocalBox(root,new Vector3(.43f,.23f,.5f),new Vector3(.05f,.15f,.05f),Tint.Blast);}
            else if(type=="medic"){LocalBox(root,new Vector3(0,.06f,.185f),new Vector3(.1f,.4f,.035f),Tint.White);LocalBox(root,new Vector3(0,.06f,.19f),new Vector3(.3f,.1f,.04f),Tint.White);LocalBox(root,new Vector3(.4f,.1f,.3f),new Vector3(.12f,1.1f,.12f),Tint.Wood);LocalBall(root,new Vector3(.4f,.7f,.3f),Vector3.one*.22f,Tint.Heal);}
            else if(type=="giant"){LocalBox(root,new Vector3(.43f,.1f,.5f),new Vector3(.18f,1.25f,.18f),Tint.Wood);LocalBox(root,new Vector3(.43f,.72f,.5f),new Vector3(.52f,.35f,.4f),Tint.Rock);}
            else if(type=="cavalry"){LocalBox(root,new Vector3(0,-.5f,0),new Vector3(.65f,.6f,1.1f),Tint.Wood);LocalBox(root,new Vector3(0,-.14f,.65f),new Vector3(.36f,.6f,.4f),Tint.Wood);foreach(float x in new[]{-.24f,.24f})foreach(float z in new[]{-.4f,.4f})LocalBox(root,new Vector3(x,-.95f,z+walk*.2f),new Vector3(.14f,.6f,.15f),Tint.Ink);LocalBox(root,new Vector3(.4f,.16f,.9f),new Vector3(.07f,.07f,2),Tint.Steel);}
            else if(type=="catapult"){LocalBox(root,new Vector3(0,-.42f,0),new Vector3(1.1f,.25f,1.4f),Tint.Wood);foreach(float x in new[]{-.6f,.6f})foreach(float z in new[]{-.45f,.45f})LocalBall(root,new Vector3(x,-.62f,z),new Vector3(.22f,.5f,.5f),Tint.Ink);LocalBox(root,new Vector3(0,.4f,-.1f),new Vector3(.15f,1.7f,.17f),Tint.Wood,Quaternion.Euler(-35+kick*50,0,0));LocalBall(root,new Vector3(0,.95f,-.5f),Vector3.one*.45f,Tint.Rock);}
            else LocalBox(root,new Vector3(.4f,.23f,.38f),new Vector3(.1f,.7f,.1f),Tint.Steel,Quaternion.Euler(kick*80,0,-12));
            float ground=Data.Height(Config.map,p.x,p.z);if(!Catalog.Finite(ground))ground=p.y-.8f*scale;var foot=new Vector3(p.x,ground+.045f,p.z);
            if(Selection.Contains(id))Ring(foot,.7f*scale,Tint.Gold);else if(Deploying&&stance!="advance")Ring(foot,.48f*scale,stance=="hold"?Tint.Steel:Tint.White);
            if(inspired||fortified)Ring(foot,.65f*scale,fortified?Tint.BlueLight:Tint.Gold);
            if(hp<.99f){Box(p+new Vector3(0,1.1f*scale,0),new Vector3(.7f*scale,.06f,.08f),Tint.Ink);Box(p+new Vector3(-.35f*scale*(1-hp),1.11f*scale,0),new Vector3(.7f*scale*Mathf.Clamp01(hp),.07f,.09f),color);}
        }
        void Prop(string type,string team,float x,float z,float ground,bool active)
        {
            var p=new Vector3(x,ground,z);var color=team=="red"?Tint.Red:team=="blue"?Tint.Blue:Tint.Gold;
            if(type=="barrel"){Add(cylinder,active?Tint.Blast:Tint.Wood,Matrix4x4.TRS(p+Vector3.up*.55f,Quaternion.identity,new Vector3(.84f,.55f,.84f)));for(int i=0;i<2;i++)Ring(p+Vector3.up*(.2f+i*.65f),.43f,Tint.Ink);}
            else if(type=="barricade"){Box(p+Vector3.up*.65f,new Vector3(.76f,1.3f,2.4f),Tint.Wood);for(int i=-1;i<=1;i++)Box(p+new Vector3(-.42f,.85f,i*.8f),new Vector3(.2f,.2f,.2f),Tint.Steel);}
            else if(type=="banner"){Rod(p,p+Vector3.up*2.6f,.065f,Tint.Wood);Box(p+new Vector3(.47f,2.12f,0),new Vector3(.95f,.65f,.05f),color);}
            else if(type=="spikes"){Box(p+Vector3.up*.07f,new Vector3(1.7f,.14f,1.7f),Tint.Wood);for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++)Box(p+new Vector3(i*.5f,.3f,j*.5f),new Vector3(.08f,.5f,.08f),Tint.Steel,Quaternion.Euler(0,0,15));}
            else {Box(p+Vector3.up*.12f,new Vector3(1.7f,.24f,1.7f),color);for(int i=0;i<3;i++)Ring(p+Vector3.up*(.3f+i*.1f),.45f,Tint.Steel);}
        }
        void LateUpdate()
        {
            if(Data==null||Config==null)return;foreach(var b in batches.Values)b.count=0;
            if(cameraWidth!=Screen.width||cameraHeight!=Screen.height){cameraWidth=Screen.width;cameraHeight=Screen.height;ViewCamera.fieldOfView=Viewport.Compact?65:42;distance=Viewport.Compact?70:40;}
            if(CameraMode==2&&Current!=null){UnitFrame best=null;foreach(var u in Current.units)if(u.alive&&(best==null||Data.Units[u.type].cost>Data.Units[best.type].cost))best=u;if(best?.t!=null)target=Vector3.Lerp(target,new Vector3(best.t[0],1,best.t[2]),.1f);}
            Quaternion rotation=Quaternion.Euler(CameraMode==1?87:pitch,yaw,0);ViewCamera.transform.position=target-rotation*Vector3.forward*distance;ViewCamera.transform.rotation=rotation;
            var map=Data.Maps[Config.map];if(lastMapColor!=map.color){lastMapColor=map.color;materials[(int)Tint.Ground].color=new Color(((map.color>>16)&255)/255f,((map.color>>8)&255)/255f,(map.color&255)/255f);}Box(new Vector3(0,-3.6f,0),new Vector3(100,.3f,80),Tint.Water);
            foreach(var p in map.platforms)Box(new Vector3(p.x,p.y,p.z),new Vector3(p.hx*2,p.hy*2,p.hz*2),p.bridge?Tint.Wood:Tint.Ground,Quaternion.Euler(0,0,p.angle*Mathf.Rad2Deg));
            foreach(var p in map.rocks)Ball(new Vector3(p.x,p.r*.2f,p.z),Vector3.one*p.r*2,Tint.Rock);
            foreach(var p in map.walls){Box(new Vector3(p.x,p.hy,p.z),new Vector3(p.hx*2,p.hy*2,p.hz*2),Tint.Rock);Box(new Vector3(p.x,p.hy*2+.05f,p.z),new Vector3(p.hx*2+.08f,.12f,p.hz*2+.08f),Tint.Edge);}
            foreach(var p in map.zones){float floor=Data.Height(Config.map,p.x,p.z);if(Catalog.Finite(floor))Box(new Vector3(p.x,floor+.015f,p.z),new Vector3(p.hx*2,.02f,p.hz*2),p.type=="mud"?Tint.Mud:Tint.Ice);}
            if(Deploying){for(int i=-16;i<=16;i+=2)for(int j=-10;j<=10;j+=2){float h=Data.Height(Config.map,i,j);if(Catalog.Finite(h))Box(new Vector3(i,h+.025f,j),new Vector3(.07f,.025f,.07f),i<0?Tint.RedLight:Tint.BlueLight);}for(int j=-11;j<=11;j++){float h=Data.Height(Config.map,0,j);if(Catalog.Finite(h))Box(new Vector3(0,h+.025f,j),new Vector3(.06f,.03f,.4f),Tint.White);}}
            if(Current==null){int id=1;foreach(string team in new[]{"red","blue"})foreach(var u in Config.Army(team)){if(team!=HiddenTeam&&HiddenTeam!="both")Soldier(u.type,team,new Vector3(u.x,Data.Height(Config.map,u.x,u.z)+.8f*Data.Units[u.type].scale,u.z),team=="red"?Mathf.PI/2:-Mathf.PI/2,0,Data.Units[u.type].scale,id,stance:u.stance);id++;}foreach(var p in map.props)Prop(p.type,p.team,p.x,p.z,Data.Height(Config.map,p.x,p.z),false);foreach(var p in Config.props)if(p.team!=HiddenTeam&&HiddenTeam!="both")Prop(p.type,p.team,p.x,p.z,Data.Height(Config.map,p.x,p.z),false);}
            else {
                foreach(var u in Current.units)if(u.alive&&u.t!=null)Soldier(u.type,u.team,new Vector3(u.t[0],u.t[1],u.t[2]),u.angle,u.attack,Data.Units[u.type].scale,u.id,u.hp/Data.Units[u.type].hp,u.inspired,u.fortified,u.stance);
                foreach(var p in Current.props)if(p.alive)Prop(p.type,p.team,p.x,p.z,p.floor,p.active);
                foreach(var shot in Current.projectiles){var t=shot.t;var p=new Vector3(t[0],t[1],t[2]);if(shot.type=="bomb"||shot.type=="stone")Ball(p,Vector3.one*(shot.type=="stone"?.56f:.36f),shot.type=="stone"?Tint.Rock:Tint.Ink);else Rod(p,p-UnityWorld.Vector(shot.v).normalized*.7f,.035f,shot.type=="bolt"?Tint.Steel:Tint.Wood);}
                foreach(var r in Current.ragdolls){if(r.wreck!=null&&r.wreck.Length==4&&Catalog.Finite(r.wreck[1])){var w=r.wreck;var q=Quaternion.Euler(0,w[3]*Mathf.Rad2Deg,70);Box(new Vector3(w[0],w[1]+.15f,w[2]),r.type=="cavalry"?new Vector3(.65f,.5f,1.3f):new Vector3(1.2f,.25f,1.5f),Tint.Wood,q);for(int j=-1;j<=1;j+=2)Ball(new Vector3(w[0]+j*.6f,w[1]+.18f,w[2]),new Vector3(.5f,.2f,.5f),Tint.Ink);}}foreach(var r in Current.ragdolls)foreach(var part in r.parts){var t=part.t;var size=part.name=="torso"?new Vector3(.46f,.64f,.32f):part.name=="head"?new Vector3(.4f,.4f,.4f):new Vector3(.22f,.6f,.24f);Box(new Vector3(t[0],t[1],t[2]),size*Data.Units[r.type].scale,part.name=="head"?Tint.Skin:r.team=="red"?Tint.Red:Tint.Blue,new Quaternion(t[3],t[4],t[5],t[6]));}
                foreach(var e in Current.effects){float f=1-e.life/e.maxLife;Ring(new Vector3(e.x,e.y+.12f,e.z),.3f+f*(e.type=="heal"?1:2.5f),e.type=="heal"?Tint.Heal:Tint.Blast);}
                foreach(var o in Current.commander.active)if(o.endTick>Current.tick)Ring(new Vector3(o.x,Data.Height(Config.map,o.x,o.z)+.08f,o.z),o.type=="focus"?4:6,o.team=="red"?Tint.RedLight:Tint.BlueLight);
            }
            if(Marker.HasValue)Ring(Marker.Value+Vector3.up*.06f,MarkerRadius,Tint.Gold);foreach(var b in batches.Values)Draw(b);
        }
        public void Orbit(Vector2 delta){CameraMode=0;yaw+=delta.x*.22f;pitch=Mathf.Clamp(pitch-delta.y*.15f,25,84);}
        public void Zoom(float amount){distance=Mathf.Clamp(distance-amount*3,18,90);}
        public void ResetCamera(){target=Vector3.zero;yaw=0;pitch=51;distance=Viewport.Compact?70:40;CameraMode=0;}
        public bool Ground(Vector2 screen,out Vector3 p)
        {
            var ray=ViewCamera.ScreenPointToRay(screen);p=Vector3.zero;float nearest=float.PositiveInfinity;foreach(var platform in Data.Maps[Config.map].platforms){var normal=Quaternion.Euler(0,0,platform.angle*Mathf.Rad2Deg)*Vector3.up;var plane=new Plane(normal,new Vector3(platform.x,platform.y,platform.z)+normal*platform.hy);if(plane.Raycast(ray,out float distance)&&distance<nearest){var hit=ray.GetPoint(distance);float h=Data.Height(Config.map,hit.x,hit.z);if(Catalog.Finite(h)&&Mathf.Abs(h-hit.y)<.1f){nearest=distance;p=hit;}}}return nearest<float.PositiveInfinity;
        }
        void OnDestroy(){if(materials!=null)foreach(var m in materials)Destroy(m);}
    }
}




