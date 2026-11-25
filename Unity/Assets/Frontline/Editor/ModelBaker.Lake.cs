using System;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void LakeRoads(bool fine,Action<Vector3,Vector3,Vector3,Vector3,Color> quad)
        {
            int pieces=fine?720:360;
            double step=Math.PI*2/pieces;
            for(int kind=0;kind<2;kind++)
                for(int i=0;i<pieces;i++)
                {
                    var p=Waymark(i,kind==1);
                    var q=Waymark(i+1,kind==1);
                    var startNormal=Across(q-Waymark(i-1,kind==1));
                    var endNormal=Across(Waymark(i+2,kind==1)-p);
                    Lane(p,q,startNormal,endNormal,kind==0?1.76f:1.36f,i+kind*9);
                }
            for(int sector=-1;sector<Manor.Sectors.Count;sector++)
            {
                var site=sector<0?new V3(FrontierMap.Boundary+.15f,0,0):FrontierMap.CampCenter(sector);
                var start=UnityWorld.Vector(site);
                var end=UnityWorld.Vector(FrontierMap.RoadJunction(site));
                int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(start,end)/2.3f));
                var across=Across(end-start);
                for(int i=0;i<steps;i++)Lane(Vector3.Lerp(start,end,i/(float)steps),
                    Vector3.Lerp(start,end,(i+1)/(float)steps),across,across,sector<0?2.23f:1.48f,i);
            }

            Vector3 Waymark(int i,bool ridge) => UnityWorld.Vector(FrontierMap.SurveyRoad((i+pieces)%pieces*step,ridge));
            Vector3 Across(Vector3 direction) => new Vector3(-direction.z,0,direction.x).normalized;

            void Lane(Vector3 start,Vector3 end,Vector3 begin,Vector3 finish,float radius,int stripe)
            {
                float wear=.5f+.5f*Mathf.Sin(stripe*1.23f);
                float lift=fine?.12f:.22f;
                var earth=Color.Lerp(new Color(.46f,.392f,.273f,.15f),new Color(.53f,.466f,.344f,.15f),wear);
                var worn=Color.Lerp(new Color(.58f,.509f,.366f,.17f),new Color(.623f,.546f,.413f,.17f),wear);
                var a=GroundAt(start-begin*radius,lift);var b=GroundAt(start+begin*radius,lift);
                var c=GroundAt(end+finish*radius,lift);var d=GroundAt(end-finish*radius,lift);
                quad(a,b,c,d,earth);
                float tread=radius*.64f;
                quad(GroundAt(start-begin*tread,lift+.036f),GroundAt(start+begin*tread,lift+.036f),
                    GroundAt(end+finish*tread,lift+.036f),GroundAt(end-finish*tread,lift+.036f),worn);
            }
        }

        static Vector3 GroundAt(Vector3 at,float offset)
        {
            float y=FrontierMap.SurfaceHeight(at.x,at.z);
            at.y=(Catalog.Finite(y)?y:0)+offset;
            return at;
        }

        static void FarForests(Action<Vector3,Vector3,Vector3,Color> face)
        {
            for(int i=0;i<FrontierMap.ShoreStations;i++)
                for(int tree=0;tree<24;tree++)
                {
                    float seed=i*35.7f+tree*17.9f;
                    double bearing=(i+Fract(Mathf.Sin(seed)*3671.24f))*Math.PI*2/FrontierMap.ShoreStations;
                    float depth=.06f+.88f*Fract(Mathf.Cos(seed*.591f)*2374.24f);
                    var p=UnityWorld.Vector(FrontierMap.RingRoad(bearing,depth));
                    if(FrontierMap.BesideRingRoad(p.x,p.z,depth,.039f))continue;
                    if(Mathf.Sin(p.x*.08f+p.z*.044f)+Mathf.Cos(p.z*.097f-p.x*.033f)<-.25f)continue;
                    bool camp=false;
                    for(int j=0;j<Manor.Sectors.Count;j++)
                        camp|=(p-UnityWorld.Vector(FrontierMap.CampCenter(j))).sqrMagnitude<17*17;
                    if(camp||p.x<FrontierMap.Boundary+22&&p.x>FrontierMap.Boundary-8&&Mathf.Abs(p.z)<12)continue;
                    p=GroundAt(p,.08f);
                    float var=Fract(Mathf.Cos(seed*1.31f)*1321.08f);
                    float r=1.52f+var*1.18f,h=4.2f+var*3.4f;
                    var baseColor=Color.Lerp(new Color(.241f,.365f,.252f,.70f),new Color(.385f,.49f,.283f,.70f),var);
                    var top=p+V(var*.75f,h,0);
                    for(int side=0;side<4;side++)
                    {
                        float a=side*Mathf.PI*.5f+seed,b=a+Mathf.PI*.5f;
                        var a0=p+V(Mathf.Sin(a)*r,.5f,Mathf.Cos(a)*r);
                        var b0=p+V(Mathf.Sin(b)*r,.5f,Mathf.Cos(b)*r);
                        face(a0,b0,top,baseColor*(.83f+.08f*side));
                    }
                }
        }

        static float Fract(float value) => value-Mathf.Floor(value);

        static Geometry DistantRelief()
        {
            var mesh=new Geometry();
            const int stations=FrontierMap.ShoreStations*4,bands=14;
            var points=new Vector3[stations,bands+1];
            var normals=new Vector3[stations,bands+1];
            var tints=new Color[stations,bands+1];
            for(int i=0;i<stations;i++)
                for(int band=0;band<=bands;band++)
                {
                    points[i,band]=ReliefPoint(i,band);
                    tints[i,band]=ReliefTint(points[i,band],band);
                    tints[i,band].a=band/(float)bands;
                }
            for(int i=0;i<stations;i++)
                for(int band=0;band<=bands;band++)
                {
                    var tangent=points[(i+1)%stations,band]-points[(i+stations-1)%stations,band];
                    var outward=points[i,Mathf.Min(band+1,bands)]-points[i,Mathf.Max(0,band-1)];
                    normals[i,band]=band==0?Vector3.up:Vector3.Cross(tangent,outward).normalized;
                }
            for(int i=0;i<stations;i++)
                for(int band=0;band<bands;band++)
                {
                    int j=(i+1)%stations,k=band+1;
                    mesh.SmoothFace(points[i,band],points[j,band],points[j,k],tints[i,band],tints[j,band],tints[j,k],
                        normals[i,band],normals[j,band],normals[j,k]);
                    mesh.SmoothFace(points[i,band],points[j,k],points[i,k],tints[i,band],tints[j,k],tints[i,k],
                        normals[i,band],normals[j,k],normals[i,k]);
                }
            return mesh;

            Vector3 ReliefPoint(int i,int band)
            {
                int j=i/4;
                var bank=UnityWorld.Vector(FrontierMap.OuterBank[j]+(FrontierMap.OuterBank[(j+1)%FrontierMap.ShoreStations]-FrontierMap.OuterBank[j])*((i%4)*.25f));
                if(band==0)return bank;
                float a=i/(float)stations*2*Mathf.PI;
                float spur=Mathf.Sin(a*11+band*.58f)+.48f*Mathf.Cos(a*23-band*.39f)+.22f*Mathf.Sin(a*39+band*.71f);
                float range=.54f+.24f*Mathf.Cos(a*3+band*.40f)+.15f*Mathf.Sin(a*9-band*.49f)+.07f*spur;
                float extension=11.5f*band+6.8f*band*band+range*(3.5f*band);
                float crest=.5f+.5f*Mathf.Cos(band*1.64f+a*2+.69f*Mathf.Sin(a*6-.53f*band));
                // Outside the surveyed mainland only the very last horizon skirt descends to water.
                float altitude=band==bands?-2:Mathf.Max(1.5f,(5+15*band)*(.25f+.72f*crest)*range+3.5f*spur);
                float radius=(bank-V(FrontierMap.ManorX,0,0)).magnitude+extension;
                var direction=(bank-V(FrontierMap.ManorX,0,0)).normalized;
                return V(FrontierMap.ManorX+direction.x*radius,altitude,direction.z*radius);
            }

            Color ReliefTint(Vector3 p,int band)
            {
                if(band==0)return Ground(p);
                float pockets=.5f+.29f*Mathf.Sin(p.x*.067f+p.z*.031f)+.19f*Mathf.Cos(p.z*.091f-p.x*.036f);
                var forest=Color.Lerp(new Color(.292f,.429f,.323f,.59f),new Color(.54f,.623f,.472f,.59f),pockets);
                var rock=Color.Lerp(new Color(.467f,.505f,.422f,.32f),new Color(.654f,.693f,.59f,.31f),pockets);
                var near=Color.Lerp(forest,rock,Mathf.Clamp01((p.y-15)/103+band*.051f));
                return Color.Lerp(near,new Color(.435f,.525f,.624f,near.a),Mathf.Clamp01((band-1)*.096f));
            }
        }
    }
}
