using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontline.UnityGame
{
    public static class PoolChecks
    {
        static void Require(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        public static Result RunLegacy(Catalog data)
        {
            var config=data.Default();config.commander.enabled=true;
            using(var battle=new Frontline.Core.History07.Battle(data,config,new LegacyUnityWorldFactory()))
            {
                battle.IssueOrder("red",new Order("inspire",-6,0),out _);while(battle.Result==null)battle.Step();
                Require(battle.Tick==1847&&battle.Result.winner=="red","Frozen v0.6 seed outcome changed");
                Debug.Log("FRONTLINE_LEGACY_SEED_OK ticks="+battle.Tick+" winner="+battle.Result.winner);return battle.Result;
            }
        }
        public static void Run(Catalog data,IWorldFactory factory)
        {
            var config=new Deployment{red=new List<UnitPlacement>{new UnitPlacement("archer",-6,0)},blue=new List<UnitPlacement>{new UnitPlacement("archer",6,0)}};
            using(var battle=new Battle(data,config,factory))
            {
                var world=(UnityWorld)battle.World;IBody prior=null;
                for(int i=0;i<256;i++){var body=world.Projectile(1000+i,"arrow",new V3(0,4,0),new V3(0,0,1),1,1);Require(V3.Distance(body.Position,new V3(0,4,0))<.001f&&body.Velocity.z>.99f,"Pooled projectile lost spawn pose or launch velocity");if(prior!=null)Require(ReferenceEquals(prior,body),"Projectile body was not reused");Require(body.Id==1000+i,"Pooled projectile kept its old id");world.Remove(body);prior=body;}
                var first=battle.PrepareSnapshot();var live=battle.PrepareSnapshot();
                world.Corpse(1,"cavalry","red",data.Units["cavalry"],new V3(0,3,0),new V3(),new V3());battle.Snapshot(first);string saved=Json.Write(first);
                for(int i=0;i<32;i++){world.Corpse(10+i,"militia","blue",data.Units["militia"],new V3(i%4,3,i%6),new V3(),new V3());world.Step(Catalog.Step);world.AgeCorpses(Catalog.Step);battle.Snapshot(live);}
                VerifyPoseParity(world);
                Require(saved==Json.Write(first),"Pooled corpse mutated an archived frame");Require(live.ragdolls.Count==24&&live.ragdolls[0].parts.Count==6,"Corpse pool bound or six-body contract changed");
                Require(world.ProjectilePoolMisses==0&&world.CorpsePoolMisses==0,"A prewarmed physics pool was exhausted");
                for(int i=0;i<8;i++)battle.Snapshot(live);long start=AllocationMeter.Read();for(int i=0;i<100;i++)battle.Snapshot(live);long bytes=AllocationMeter.Available?AllocationMeter.Read()-start:-1;
                Require(!AllocationMeter.Available||bytes==0,"Prepared native corpse snapshots allocated "+bytes+" bytes");
                Debug.Log("FRONTLINE_POOL_OK projectiles=256 corpseReuses=32 parts=6 snapshotAllocatedBytes="+bytes+" misses=0");
            }
        }
        static void VerifyPoseParity(UnityWorld world)
        {
            int count=0;foreach(var root in SceneManager.GetSceneByName(world.SceneName).GetRootGameObjects())foreach(var rb in root.GetComponentsInChildren<Rigidbody>()){
                rb.transform.GetPositionAndRotation(out var position,out var rotation);
                Require((position-rb.position).sqrMagnitude<.00000001f&&Mathf.Abs(Quaternion.Dot(rotation,rb.rotation))>.999999f,"Combined pose read lagged physics");count++;
            }
            Require(count>=6,"Pose parity check missed ragdolls");Debug.Log("FRONTLINE_POSE_OK combinedReads="+count);
        }
    }
}
