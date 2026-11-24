using System.Collections;
using System.Collections.Generic;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        IEnumerator CheckSurfaceArt()
        {
            var view=app.View;
            string initial=Json.Write(app.Config);
            view.SetQuality(2,false);
            var shell=app.GetComponentInChildren<Canvas>(true);
            if(!shell)shell=Object.FindAnyObjectByType<Canvas>();
            Require(shell&&QualitySettings.antiAliasing==4&&QualitySettings.shadowCascades==4,"Desktop art capture requires the Q2 player");
            var rows=new List<object>();
            foreach(string key in new[]{"manor_hall_tower","cottage","bakery","tree","pine","body_shield","horse","arm_right_spear","arena_ground"})
            {
                var mesh=Resources.Load<Mesh>("Models/"+key);
                Require(mesh&&mesh.HasVertexAttribute(VertexAttribute.TexCoord0)&&
                    mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1)==4,"Missing baked surface channels on "+key);
                var proxy=Resources.Load<Mesh>("Models/"+key+"_lod1");
                Require(proxy,"Missing low mesh "+key);
                rows.Add(new {model=key,main=mesh.vertexCount,proxy=proxy.vertexCount});
            }
            var estate=WideEstateFixture();
            estate.guideDismissed=true;estate.Upgrade(0);estate.Upgrade(0);
            for(int i=0;i<6;i++)estate.RecruitGuard();
            yield return Press("Mode.Manor");
            app.SetManorSpeed(0);app.ImportText(estate.Export());app.SelectManorSlot(-1);
            try
            {
                shell.enabled=false;
                var keep=new Vector3(FrontierMap.ManorX,0,0);
                view.Focus(keep+new Vector3(1,2,0),83,42,-27);
                yield return ManorShot("300-lake-estate");
                view.Focus(keep+new Vector3(-.65f,5.55f,0),25.6f,19,29);
                yield return ManorShot("300-ducal-keep");
                var town=new Vector3(FrontierMap.ManorX+Manor.SlotX(1),1.20f,Manor.SlotZ(1));
                view.Focus(town,10.2f,27,-24);
                yield return ManorShot("300-jettied-houses");
                var forge=new Vector3(FrontierMap.ManorX+Manor.SlotX(5),1.12f,Manor.SlotZ(5));
                view.Focus(forge,9.6f,23,36);
                yield return ManorShot("300-smithy");
                Require(view.LakeShoreReady,"Missing baked shoreline on the shipped lake material");
                view.Focus(BattleView.QuayPoint+new Vector3(1.2f,-.6f,-1.6f),15.5f,27,159);
                yield return ManorShot("300-littoral");
                var tree=view.PortraitTree();
                view.Focus(tree+Vector3.up*2.1f,12.8f,22,-29);
                yield return ManorShot("300-woodland");
                app.SetManorMode(false);
                app.SetMode("sandbox");
                app.ImportText(Json.Write(ActionFormation()));
                view.Focus(new Vector3(-3.35f,1.02f,-8.2f),3.74f,12,-134);
                yield return ManorShot("300-armour-portrait");
                app.RequestStart();app.Paused=true;
                Require(app.Battle?.World is UnityWorld,"Missing live PhysX formation");
                view.CheckParallelPoses=true;
                var actions=new List<object>();
                yield return RecordActionMatch(new[]{
                    new MotionCapture("cavalry","charger",MotionMoment.Gallop),
                    new MotionCapture("spear","lancer",MotionMoment.Strike),
                    new MotionCapture("shield","men-at-arms",MotionMoment.Strike)
                },actions,"300");
                Require(view.CheckedParallelPoses>0,"No parallel pose samples");
                var report=new {version=Application.version,quality=view.QualityLevel,width=Screen.width,height=Screen.height,
                    realPhysX=true,workers=view.ParallelPoseWorkers,checkedParallelPoses=view.CheckedParallelPoses,
                    surfaceMeshes=rows,actions,screenshots=11};
                yield return Save("surface-art.json",Encoding.UTF8.GetBytes(Json.Write(report)),"application/json");
            }
            finally {view.CheckParallelPoses=false;shell.enabled=true;}
            app.ReturnToDeploy();app.ImportText(initial);
            Require(Json.Write(app.Config)==initial,"Art capture changed the arena deployment");
            Debug.Log("FRONTLINE_SURFACE_ART_OK");
        }
    }

    public sealed partial class BattleView
    {
        internal Vector3 PortraitTree()
        {
            var landmark=CampCenter(0)+new Vector3(17,0,23);
            Vector3 best=default;
            float nearest=float.MaxValue;
            foreach(var first in outerVegetation)
            {
                if(first.mesh.high.name!="tree")continue;
                for(var page=first;page!=null;page=page.next)for(int i=0;i<page.count;i++)
                {
                    Vector3 p=page.matrices[i].GetColumn(3);
                    float d=(p-landmark).sqrMagnitude;
                    if(d<nearest){nearest=d;best=p;}
                }
            }
            if(nearest==float.MaxValue)throw new System.InvalidOperationException("No deciduous tree in the mainland batches");
            return best;
        }
    }
}
