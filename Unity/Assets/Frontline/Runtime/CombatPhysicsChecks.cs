using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontline.UnityGame
{
    public static class CombatPhysicsChecks
    {
        static void Require(bool b,string message){if(!b)throw new InvalidOperationException(message);}
        public static void Run(Catalog data)
        {
            var c=new Deployment{red=new List<UnitPlacement>{new UnitPlacement("cavalry",-8,-3),new UnitPlacement("catapult",-8,3)},blue=new List<UnitPlacement>{new UnitPlacement("shield",8,0)}};
            using(var b=new Battle(data,c,new UnityWorldFactory()))
            {
                var horse=(UnityBody)b.Units[0].entity.body;var cart=(UnityBody)b.Units[1].entity.body;
                Require(horse.colliders.Length==2&&horse.colliders[0] is CapsuleCollider capsule&&capsule.direction==2,"Horse must have longitudinal body + rider capsules");
                Require(cart.collider is BoxCollider box&&box.size.z>2&&box.size.x>1.6f,"Cart base dimensions do not match mesh");
                horse.Facing(Mathf.PI/2,true);cart.Facing(0,false);Physics.SyncTransforms();
                Require(horse.collider.bounds.extents.x>1.3f&&horse.collider.bounds.extents.z<.6f,"Horse collision does not rotate with heading");
                Require(horse.rigidbody.collisionDetectionMode==CollisionDetectionMode.ContinuousSpeculative,"Charge lacks speculative CCD");
                cart.Facing(0,true);Require(cart.rigidbody.collisionDetectionMode==CollisionDetectionMode.ContinuousSpeculative,"Airborne fast body lacks CCD");
                var obstacle=new GameObject("Collision acceptance wall");obstacle.layer=8;SceneManager.MoveGameObjectToScene(obstacle,horse.gameObject.scene);obstacle.transform.position=new Vector3(-3,1,-3);var wall=obstacle.AddComponent<BoxCollider>();wall.size=new Vector3(.4f,4,8);
                horse.rigidbody.linearVelocity=new Vector3(24,0,0);for(int i=0;i<90;i++)b.World.Step(Catalog.Step);
                Require(horse.Position.x< -4.05f,"High-speed horse tunneled through wall/head collision");
                var cartWall=new GameObject("Cart acceptance wall");cartWall.layer=8;SceneManager.MoveGameObjectToScene(cartWall,horse.gameObject.scene);cartWall.transform.position=new Vector3(-3,1,3);cartWall.AddComponent<BoxCollider>().size=new Vector3(.4f,4,3);
                cart.Facing(Mathf.PI/2,true);cart.rigidbody.linearVelocity=new Vector3(16,0,0);for(int i=0;i<90;i++)b.World.Step(Catalog.Step);Require(cart.Position.x< -4.15f,"Cart frame tunneled through wall");
                var ownerShot=(UnityBody)b.World.Projectile(999,"arrow",horse.Position+new V3(0,3,0),new V3(),0,horse.Id);foreach(var col in horse.colliders)Require(Physics.GetIgnoreCollision(ownerShot.collider,col),"Compound owner ignore incomplete");b.World.Remove(ownerShot);
                Debug.Log("FRONTLINE_SHAPE_OK horseColliders=2 cart=box CCD=speculative chargeWallX="+horse.Position.x+" cartWallX="+cart.Position.x);
            }
            c=new Deployment{red=new List<UnitPlacement>{new UnitPlacement("archer",-6,0)},blue=new List<UnitPlacement>{new UnitPlacement("archer",6,0)}};c.props.Add(new PropPlacement{type="banner",team="red",x=-4.7f,z=0});
            using(var world=new UnityWorld(data,c))
            {
                var owner=(UnityBody)world.Unit(1,c.red[0],"red",data.Units["archer"]);var banner=(UnityBody)world.Prop(3,c.props[0],data.Props["banner"]);
                var shield=(UnityBody)world.Unit(2,new UnitPlacement("shield",-3.8f,0),"red",data.Units["shield"]);
                for(int i=0;i<30;i++)world.Step(Catalog.Step);
                var p=CombatMotion.Launch("archer",owner.Position,new V3(1,0,0),1);var shot=(UnityBody)world.Projectile(100,"arrow",p,new V3(15,1,0),0,1);
                Require(Physics.GetIgnoreCollision(shot.collider,owner.collider)&&Physics.GetIgnoreCollision(shot.collider,banner.collider),"Launch owner/banner grace missing");
                Require(!Physics.GetIgnoreCollision(shot.collider,shield.collider),"Friendly shield incorrectly ignored");
                bool blocked=false;for(int i=0;i<14;i++){world.Step(Catalog.Step);foreach(var hit in world.Contacts())if(hit.a==2||hit.b==2)blocked=true;}
                Require(!blocked,"Shoulder-height arrow struck ordinary friendly shield at launch");
                Require(!Physics.GetIgnoreCollision(shot.collider,banner.collider),"Banner grace never expired");world.Remove(shot);
                Require(!Physics.GetIgnoreCollision(shot.collider,owner.collider),"Pooled shot leaked ignored collision");
                shot=(UnityBody)world.Projectile(101,"arrow",new V3(-4.3f,owner.Position.y+.2f,0),new V3(15,0,0),0,1);
                blocked=false;for(int i=0;i<12;i++){world.Step(Catalog.Step);foreach(var hit in world.Contacts())if(hit.a==2||hit.b==2)blocked=true;}
                Require(blocked,"Low friendly projectile no longer physically blocked: shot="+Json.Write(shot.Position)+" shield="+Json.Write(shield.Position)+" ignore="+Physics.GetIgnoreCollision(shot.collider,shield.collider)+" shieldColliderId="+world.ColliderId(shield.collider));world.Remove(shot);
                Debug.Log("FRONTLINE_LAUNCH_OK shoulderClear=true lowFriendlyBlocked=true bannerGraceExpired=true poolReset=true");
            }
            using(var world=new UnityWorld(data,data.Default("meadow")))
            {
                int rocks=0;foreach(var root in SceneManager.GetSceneByName(world.SceneName).GetRootGameObjects())foreach(var rock in root.GetComponentsInChildren<MeshCollider>())
                {
                    rocks++;Require(rock.convex&&rock.sharedMesh.triangles.Length/3<=64,"Rock hull is not low-face convex");
                    float radius=rock.transform.localScale.x*.5f;var q=rock.transform.position+Vector3.right*(radius*.99f);
                    Require((rock.ClosestPoint(q)-q).sqrMagnitude>radius*radius*.0001f,"Rock retained spherical air wall");
                }
                Require(rocks==data.Maps["meadow"].rocks.Count&&rocks>0,"Rock hull count changed");Debug.Log("FRONTLINE_ROCK_OK hulls="+rocks+" maxTriangles=64");
            }
        }
    }
}
