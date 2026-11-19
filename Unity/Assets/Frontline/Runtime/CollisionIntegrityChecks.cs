using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontline.UnityGame
{
    static class CollisionIntegrityChecks
    {
        static void Require(bool passes,string failure)
        {
            if(!passes)throw new InvalidOperationException(failure);
        }

        internal static object Run(Catalog source)
        {
            var failures=new List<Exception>();
            var hull=Measure(()=>CheckSilhouettes(source),failures);
            var data=new Catalog(Json.Write(source.Data));
            foreach(var map in data.Maps.Values)map.props.Clear();
            var crowd=Measure(()=>CheckCrowd(data),failures);
            var bridge=Measure(()=>CheckBridge(data),failures);
            var remains=Measure(()=>CheckRemains(data),failures);
            var plots=Measure(CheckPlotFits,failures);
            if(failures.Count>0)throw new AggregateException("Collision fixtures failed",failures);
            return new {hull,crowd,bridge,remains,plots};
        }

        static object Measure(Func<object> run,List<Exception> failures)
        {
            try{return run();}
            catch(Exception failure){failures.Add(failure);return null;}
        }

        static object CheckSilhouettes(Catalog data)
        {
            var config=new Deployment {map="meadow"};
            using(var world=new UnityWorld(data,config))
            {
                float rockError=0,barrelError=0;
                int rocks=0;
                var high=Resources.Load<Mesh>("Models/rock");
                var vertices=high.vertices;
                Physics.SyncTransforms();
                foreach(var root in SceneManager.GetSceneByName(world.SceneName).GetRootGameObjects())
                    foreach(var hull in root.GetComponentsInChildren<MeshCollider>())
                    {
                        rocks++;
                        Require(hull.sharedMesh.name=="rock_lod1"&&hull.convex,"Rock used a different physics hull");
                        rockError=Mathf.Max(rockError,OutsideHull(hull,vertices));
                        Require((hull.sharedMesh.bounds.size-high.bounds.size).sqrMagnitude<1e-8f,"Rock LOD silhouette differs in bounds");
                    }
                Require(rocks==data.Map(config.map).rocks.Count&&rocks>0&&rockError<.028f,"Visible rock protrudes through its hull: "+rockError);
                var types=new[]{"shield","giant","cavalry","catapult","hero_vanguard","hero_ranger","hero_commander"};
                for(int i=0;i<types.Length;i++)
                {
                    var definition=data.Unit(types[i]);
                    var body=(UnityBody)world.Unit(10+i,new UnitPlacement(types[i],-12+i*2,9.9f),"red",definition);
                    Require(body.rigidbody.solverIterations==8&&body.rigidbody.solverVelocityIterations==2&&
                        body.rigidbody.maxDepenetrationVelocity==10,"Solver profile differs for "+types[i]);
                    if(types[i]=="catapult")
                    {
                        var cart=(BoxCollider)body.collider;
                        var mesh=Resources.Load<Mesh>("Models/catapult");
                        Require(Mathf.Abs(mesh.bounds.min.y-(cart.center.y-cart.size.y*.5f)/definition.scale)<.035f,
                            "Cart wheels sit below the support box");
                    }
                    else
                    {
                        var capsule=(CapsuleCollider)body.collider;
                        float bottom=(capsule.center.y-(capsule.direction==2?capsule.radius:capsule.height*.5f))/definition.scale;
                        Require(Mathf.Abs(bottom+.79f)<.025f,"Support at wrong height for "+types[i]);
                    }
                }
                var barrel=(UnityBody)world.Prop(30,new PropPlacement{type="barrel",x=-8,z=7,team="red"},data.Props["barrel"]);
                var barricade=(UnityBody)world.Prop(31,new PropPlacement{type="barricade",x=-6,z=7,team="red"},data.Props["barricade"]);
                var banner=(UnityBody)world.Prop(32,new PropPlacement{type="banner",x=-4,z=7,team="red"},data.Props["banner"]);
                Physics.SyncTransforms();
                barrelError=OutsideHull(barrel.collider,Resources.Load<Mesh>("Models/prop_barrel").vertices);
                Require(barrel.collider is MeshCollider convex&&convex.convex&&barrelError<.048f,"Barrel body protrudes beyond the collision hull");
                var stakes=Resources.Load<Mesh>("Models/prop_barricade").bounds;
                Require((stakes.size-((BoxCollider)barricade.collider).size).magnitude<.08f,"Barricade bounds mismatch");
                Require(banner.colliders.Length==2&&Mathf.Abs(banner.collider.bounds.max.y-ContactShape.BannerHeight)<.03f,
                    "Banner support does not match the mast");
                var wall=Resources.Load<Mesh>("Models/wall").bounds;
                Require((wall.size-Vector3.one).sqrMagnitude<.0004f,"Stone wall extends beyond its solid profile");
                Debug.Log("FRONTLINE_COLLISION_ALIGNMENT_OK rockHull="+rockError+" barrelHull="+barrelError+" actors="+types.Length+" rocks="+rocks);
                return new {rocks,rockMaxOutsideMetres=rockError,barrelMaxOutsideMetres=barrelError,checkedActors=types.Length,checkedProps=3};
            }
        }

        static float OutsideHull(Collider collider,Vector3[] points)
        {
            float max=0;
            foreach(var local in points)
            {
                var point=collider.transform.TransformPoint(local);
                max=Mathf.Max(max,(point-collider.ClosestPoint(point)).magnitude);
            }
            return max;
        }

        static object CheckCrowd(Catalog data)
        {
            var config=new Deployment{seed=167,budget=10000};
            string[] types={"militia","shield","spear","giant"};
            for(int i=0;i<60;i++)
            {
                float x=3.35f+i/10*1.72f,z=-7.06f+i%10*1.57f;
                config.red.Add(new UnitPlacement(types[i%4],-x,z));
                config.blue.Add(new UnitPlacement(types[(i+1)%4],x,z+.06f));
            }
            using(var battle=new Battle(data,config,new UnityWorldFactory()))
            {
                const int roster=120;
                var samples=new Vector3[roster];
                var radii=new float[roster];
                var overlapRuns=new byte[roster,roster];
                var approached=new bool[roster];
                foreach(var u in battle.Units)radii[u.entity.id-1]=((CapsuleCollider)((UnityBody)u.entity.body).collider).radius;
                float maxOverlap=0;
                int worstRun=0,compared=0,reached=0;
                for(int step=0;step<552&&battle.Result==null;step++)
                {
                    battle.Step();
                    if(battle.Tick<36||battle.Tick%6!=0)continue;
                    for(int i=0;i<roster;i++)
                    {
                        var u=battle.Units[i];
                        if(!u.entity.alive)continue;
                        samples[i]=UnityWorld.Vector(u.entity.body.Position);
                        if(!approached[i]&&Mathf.Abs(samples[i].x)<3.05f){approached[i]=true;reached++;}
                    }
                    for(int i=0;i<roster;i++)
                    {
                        if(!battle.Units[i].entity.alive)continue;
                        for(int j=i+1;j<roster;j++)
                        {
                            if(!battle.Units[j].entity.alive)continue;
                            var delta=samples[i]-samples[j];
                            float reach=radii[i]+radii[j];
                            if(delta.x*delta.x+delta.z*delta.z>=reach*reach||Mathf.Abs(delta.y)>.7f)
                            {overlapRuns[i,j]=0;continue;}
                            var a=(UnityBody)battle.Units[i].entity.body;
                            var b=(UnityBody)battle.Units[j].entity.body;
                            if(!Physics.ComputePenetration(a.collider,samples[i],Quaternion.identity,b.collider,samples[j],
                                Quaternion.identity,out _,out var depth))
                            {overlapRuns[i,j]=0;continue;}
                            compared++;maxOverlap=Mathf.Max(maxOverlap,depth);
                            if(depth>.20f)worstRun=Math.Max(worstRun,++overlapRuns[i,j]);else overlapRuns[i,j]=0;
                        }
                    }
                }
                Require(reached>=48&&compared>0,"Crowd test did not reach dense contact: "+reached);
                Require(worstRun<3,"Deep living-body overlap persisted for three 6-tick samples");
                Debug.Log("FRONTLINE_DENSE_CONTACT_OK approached="+reached+" contacts="+compared+" maxOverlap="+maxOverlap+" deepRun="+worstRun);
                return new {formation="60v60",ticks=battle.Tick,reached,compared,maxOverlapMetres=maxOverlap,consecutiveDeepSamples=worstRun};
            }
        }

        static object CheckBridge(Catalog data)
        {
            var config=new Deployment{map="bridge",budget=10000};
            const int size=20;
            for(int i=0;i<size;i++)config.red.Add(new UnitPlacement("militia",-4.85f-i/2*1.27f,i%2==0?-.76f:.76f));
            config.blue.Add(new UnitPlacement("shield",16,0,"hold"));
            using(var battle=new Battle(data,config,new UnityWorldFactory()))
            {
                foreach(var unit in battle.Units)unit.cooldown=999;
                var crossed=new HashSet<int>();
                float depth=0;
                for(int i=0;i<1000&&crossed.Count<size&&battle.Result==null;i++)
                {
                    battle.Step();
                    foreach(var unit in battle.Units)
                    {
                        if(unit.entity.team!="red"||!unit.entity.alive)continue;
                        var p=unit.entity.body.Position;
                        if(p.x>3.36f)crossed.Add(unit.entity.id);
                        if(Mathf.Abs(p.x)<3.17f&&Mathf.Abs(p.z)<1.76f)depth=Mathf.Max(depth,.79f*unit.definition.scale-p.y);
                    }
                }
                Require(crossed.Count==size&&depth<.14f,"Bridge crowd crossed "+crossed.Count+"/"+size+" with depth "+depth);
                Debug.Log("FRONTLINE_BRIDGE_CLEARANCE_OK crossed="+crossed.Count+"/"+size+" ticks="+battle.Tick+" depth="+depth);
                return new {party=size,crossed=crossed.Count,ticks=battle.Tick,maxFootDepthMetres=depth};
            }
        }

        static List<object> CheckRemains(Catalog data)
        {
            var rows=new List<object>();
            foreach(string map in new[]{"bridge","ruins","highland"})
            {
                var config=new Deployment{map=map};
                float x=map=="bridge"?-3.04f:map=="ruins"?-.90f:-5.45f;
                float z=map=="ruins"?5.5f:.14f;
                float ground=data.Height(map,x,z);
                using(var world=new UnityWorld(data,config))
                {
                    world.Corpse(500,"militia","red",data.Unit("militia"),
                        new V3(x,ground+.53f,z),new V3(map=="ruins"?3.5f:.08f,-2.8f,0),new V3(.13f,-.7f,.04f));
                    for(int step=0;step<245;step++){world.Step(Catalog.Step);world.AgeCorpses(Catalog.Step);}
                    var scene=SceneManager.GetSceneByName(world.SceneName);
                    float depth=0,socketGap=0,wallDepth=0;
                    int parts=0,sleeping=0;
                    var roots=scene.GetRootGameObjects();
                    foreach(var root in roots)
                    {
                        var boxes=root.GetComponentsInChildren<BoxCollider>();
                        foreach(var box in boxes)
                        {
                            if(box.gameObject.layer!=12)continue;
                            parts++;
                            var rb=box.attachedRigidbody;
                            if(rb.IsSleeping())sleeping++;
                            var extent=box.size*.5f;
                            for(int bx=-1;bx<=1;bx+=2)for(int by=-1;by<=1;by+=2)for(int bz=-1;bz<=1;bz+=2)
                            {
                                var point=box.transform.TransformPoint(Vector3.Scale(extent,new Vector3(bx,by,bz)));
                                float floor=data.Height(map,point.x,point.z);
                                if(Catalog.Finite(floor))depth=Mathf.Max(depth,floor-point.y);
                            }
                            var joint=box.GetComponent<CharacterJoint>();
                            if(joint)socketGap=Mathf.Max(socketGap,Vector3.Distance(box.transform.TransformPoint(joint.anchor),
                                joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
                            foreach(var other in boxes)
                            {
                                if(other.name!="Wall")continue;
                                if(Physics.ComputePenetration(box,box.transform.position,box.transform.rotation,
                                    other,other.transform.position,other.transform.rotation,out _,out var overlap))
                                    wallDepth=Mathf.Max(wallDepth,overlap);
                            }
                        }
                    }
                    Require(parts==6&&depth<.12f&&socketGap<.13f&&wallDepth<.07f,
                        "Remains outside tolerance on "+map+": ground="+depth+" joint="+socketGap+" wall="+wallDepth+" parts="+parts);
                    Debug.Log("FRONTLINE_REMAINS_CLEARANCE_OK "+map+" ground="+depth+" joint="+socketGap+" wall="+wallDepth);
                    rows.Add(new{map,parts,sleeping,groundDepthMetres=depth,socketGapMetres=socketGap,wallDepthMetres=wallDepth});
                }
            }
            return rows;
        }

        static object CheckPlotFits()
        {
            var types=new List<string>{"manor_hall"};
            types.AddRange(Manor.Buildings);
            int checkedMeshes=0;
            foreach(string kind in types)
                foreach(string level in new[]{"","_wing","_tower"})
                    foreach(string lod in new[]{"","_lod1"})
                    {
                        var mesh=Resources.Load<Mesh>("Models/"+kind+level+lod);
                        Require(mesh,"Missing manor mesh: "+kind+level+lod);
                        var bounds=mesh.bounds;
                        bool hall=kind=="manor_hall";
                        Require(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x))<=ManorPlotArt.HalfWidth(hall)+.001f&&
                            Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))<=ManorPlotArt.HalfDepth(hall)+.001f,
                            "Building overlaps a route: "+kind+level+lod);
                        checkedMeshes++;
                    }
            var walkClearance=BattleView.CheckEstateRoutes();
            Debug.Log("FRONTLINE_PLOT_BOUNDS_OK plotCount="+Manor.PlotCount+" tierLodMeshes="+checkedMeshes+" "+Json.Write(walkClearance));
            return new {plotCount=Manor.PlotCount,tierLodMeshes=checkedMeshes,walkClearance};
        }
    }
}
