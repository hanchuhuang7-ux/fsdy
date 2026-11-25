using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        // Positive IDs are the shader ABI; zero is reserved for meshes authored before 3.0.
        enum Surface : byte { Auto, Stone, Oak, Plaster, Roof, Metal, Cloth, Terrain, Leaf, Earth, Hide, Thatch, Glass, Outcrop, WindowLight, Ember, Shore }
        static Dictionary<Color,Surface> pigments;
        static readonly Dictionary<Color,Surface> matched=new Dictionary<Color,Surface>();
        static Surface PigmentSurface(Color c)
        {
            if(pigments==null)
            {
                pigments=new Dictionary<Color,Surface>();
                void Paint(Surface s,params Color[] dyes){foreach(var dye in dyes)pigments[dye]=s;}
                Paint(Surface.Stone,Rock,Stonework,KeepStone,KeepEdge,CliffStone,SeaStone);
                Paint(Surface.Oak,Wood,WoodLight,Walnut);
                Paint(Surface.Plaster,Plaster,Limewash);
                Paint(Surface.Roof,FiredClay,Tile,Slate,KeepRoof);
                Paint(Surface.Metal,Steel,LightSteel,WroughtIron,OldGold,Gold,Copper);
                Paint(Surface.Cloth,Cloth,DyedWool,Linen,UndyedLinen,Indigo,KeepRed,Madder);
                Paint(Surface.Leaf,Leaf,Moss,Green);
                Paint(Surface.Earth,Earth,FallowSoil,FallowRidge,Sand);
                Paint(Surface.Thatch,Straw,StrawLight);
                Paint(Surface.Hide,Skin,Dark);
                Paint(Surface.Glass,Glass,Amber);
            }
            if(c.a>.8f)return Surface.Cloth;
            if(pigments.TryGetValue(c,out var s)||matched.TryGetValue(c,out s))return s;
            var nearest=Surface.Stone;
            float best=float.MaxValue;
            foreach(var entry in pigments)
            {
                Color d=c-entry.Key;
                float delta=d.r*d.r+d.g*d.g+d.b*d.b;
                if(delta<best){best=delta;nearest=entry.Value;}
            }
            matched[c]=nearest;
            return nearest;
        }

        sealed partial class Geometry
        {
            internal Surface surface;
            readonly Dictionary<int,Vector2> flexibility=new Dictionary<int,Vector2>();
            public int Mark => vertices.Count;

            // Channel x is free cloth/tail length. Channel y articulates the charger's poll.
            public void WeightSince(int start,Vector3 fixedPoint,Vector3 freePoint,bool poll=false,bool stringNock=false)
            {
                var axis=freePoint-fixedPoint;
                float inverse=1f/axis.sqrMagnitude;
                for(int i=start;i<vertices.Count;i++)
                {
                    float w=Mathf.Clamp01(Vector3.Dot(vertices[i]-fixedPoint,axis)*inverse);
                    flexibility.TryGetValue(i,out var weights);
                    if(stringNock)weights.y=-w;else if(poll)weights.y=w*w*(3-2*w);else weights.x=w*w;
                    flexibility[i]=weights;
                }
            }
            readonly List<Surface> faceSurfaces=new List<Surface>();
            readonly Dictionary<int,Vector2> loftUV=new Dictionary<int,Vector2>();
            // Tool shafts are outside the head envelope. Keep the gorget on the torso.
            public Geometry PortraitPart(bool head)
            {
                var result=new Geometry();
                for(int i=0;i<vertices.Count;i+=3)
                {
                    var center=(vertices[i]+vertices[i+1]+vertices[i+2])/3f;
                    bool crown=center.y>.395f&&Mathf.Abs(center.x)<.32f&&Mathf.Abs(center.z)<.31f;
                    if(crown!=head)continue;
                    int dest=result.Mark;
                    result.surface=faceSurfaces[i/3];
                    result.SmoothFace(vertices[i],vertices[i+1],vertices[i+2],colors[i],colors[i+1],colors[i+2],
                        normals[i],normals[i+1],normals[i+2]);
                    for(int k=0;k<3;k++)
                    {
                        if(loftUV.TryGetValue(i+k,out var uv))result.loftUV[dest+k]=uv;
                        if(flexibility.TryGetValue(i+k,out var w))result.flexibility[dest+k]=w;
                    }
                }
                return result;
            }

            static Vector3Int Cell(Vector3 p,float scale)=>Vector3Int.FloorToInt(p*scale);

            public Mesh SurfaceMesh(string name)
            {
                var mesh=new Mesh{name=name};
                if(vertices.Count==0)return mesh;
                var groups=new Dictionary<(Vector3Int,Surface),List<Vector3>>();
                var occupied=new HashSet<Vector3Int>();
                var bounds=new Bounds(vertices[0],Vector3.zero);
                foreach(var point in vertices)bounds.Encapsulate(point);
                float cell=Mathf.Clamp(bounds.size.magnitude*.019f,.047f,.52f);
                float inv=1/cell;
                var tags=new Surface[faceSurfaces.Count];
                for(int face=0;face<tags.Length;face++)
                {
                    int i=face*3;
                    var tag=faceSurfaces[face];
                    tags[face]=tag=tag==Surface.Auto?PigmentSurface(colors[i]):tag;
                    for(int j=0;j<3;j++)
                    {
                        var key=(Vector3Int.RoundToInt(vertices[i+j]*8192),tag);
                        if(!groups.TryGetValue(key,out var adjacent))groups.Add(key,adjacent=new List<Vector3>(4));
                        if(!adjacent.Contains(normals[i+j]))adjacent.Add(normals[i+j]);
                    }
                    var a=vertices[i];var ab=vertices[i+1]-a;var ac=vertices[i+2]-a;
                    int samples=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(ab.magnitude,ac.magnitude)*inv),1,12);
                    for(int b=0;b<=samples;b++)for(int c=0;c<=samples-b;c++)
                        occupied.Add(Cell(a+(b*ab+c*ac)/samples,inv));
                }
                var points=new List<Vector3>();var shading=new List<Vector3>();var tint=new List<Color32>();
                var uv=new List<Vector2>();var properties=new List<Vector4>();
                bool pelvis=name.StartsWith("body_",StringComparison.Ordinal)&&name!="body_catapult"||name.EndsWith("_clothes",StringComparison.Ordinal)||name.StartsWith("specialist_",StringComparison.Ordinal);
                bool ankle=name=="shin"||name=="shin_plate";
                var flex=flexibility.Count==0&&!pelvis&&!ankle?null:new List<Vector4>();
                var corners=new Dictionary<(Vector3,Vector3,Color32,Vector2,Vector4,Vector2),int>();
                var faces=new int[indices.Count];
                for(int i=0;i<indices.Count;i++)
                {
                    int source=indices[i];
                    var p=vertices[source];var n=normals[source];var s=tags[source/3];
                    Vector3 rounded=Vector3.zero;
                    foreach(var adjacent in groups[(Vector3Int.RoundToInt(p*8192),s)])
                        if(Vector3.Dot(n,adjacent)>.743145f)rounded+=adjacent; // 42 degree crease
                    rounded=rounded.sqrMagnitude>.01f?rounded.normalized:n;
                    int tri=source-source%3;
                    var faceNormal=Vector3.Cross(vertices[tri+1]-vertices[tri],vertices[tri+2]-vertices[tri]).normalized;
                    if(faceNormal==Vector3.zero)faceNormal=n;
                    var tex=loftUV.TryGetValue(source,out var projected)?projected:Project(p,faceNormal,s);
                    float rough=s==Surface.Metal?.36f:s==Surface.Glass?.23f:s==Surface.Hide?.67f:s==Surface.Roof?.69f:.84f;
                    float metal=s==Surface.Metal?.91f:0;
                    var channels=new Vector4((int)s,metal,rough,Occlusion(p,rounded));
                    Color32 dye=colors[source];
                    flexibility.TryGetValue(source,out var weights);
                    var key=(p,rounded,dye,tex,channels,weights);
                    if(!corners.TryGetValue(key,out int dest))
                    {
                        corners.Add(key,dest=points.Count);points.Add(p);shading.Add(rounded);tint.Add(dye);
                        uv.Add(tex);properties.Add(channels);
                        flex?.Add(new Vector4(weights.x,weights.y,pelvis?Mathf.SmoothStep(0,1,(-.04f-p.y)/.29f):0,
                            ankle?Mathf.SmoothStep(0,1,(-.20f-p.y)/.058f):0));
                    }
                    faces[i]=dest;
                }
                mesh.indexFormat=points.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.SetVertices(points);mesh.SetNormals(shading);mesh.SetColors(tint);
                mesh.SetUVs(0,uv);mesh.SetUVs(1,properties);if(flex!=null)mesh.SetUVs(2,flex);mesh.SetTriangles(faces,0);mesh.RecalculateBounds();
                return mesh;

                float Occlusion(Vector3 p,Vector3 n)
                {
                    var right=Vector3.Cross(Mathf.Abs(n.y)<.82f?Vector3.up:Vector3.right,n).normalized;
                    var forward=Vector3.Cross(n,right);
                    var sample=p+n*cell*1.55f;
                    int hits=0;
                    for(int k=0;k<4;k++)
                    {
                        var axis=k<2?right:forward;
                        if((k&1)==0)axis=-axis;
                        if(occupied.Contains(Cell(sample+axis*cell*1.6f,inv)))hits+=2;
                        if(occupied.Contains(Cell(sample+n*cell+axis*cell*3,inv)))hits++;
                    }
                    float contact=1-Mathf.Clamp01((p.y-bounds.min.y)/Mathf.Max(.04f,bounds.size.y*.19f));
                    return Mathf.Clamp01(1-.026f*hits-.12f*contact-.07f*Mathf.Max(0,-n.y));
                }
            }

            static Vector2 Project(Vector3 p,Vector3 n,Surface s)
            {
                var abs=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
                bool horizontal=abs.y>=abs.x&&abs.y>=abs.z;
                var uv=horizontal?new Vector2(p.x,p.z):abs.x>abs.z?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                float scale=s==Surface.Metal||s==Surface.Cloth?2.35f:s==Surface.Stone?.79f:s==Surface.Roof?1.45f:1.15f;
                return uv*scale;
            }

            // Cross sections follow a parallel-transport frame. UVs are unwrapped along the
            // arc (including its seam), so grain runs continuously around a bent branch or neck.
            public void Loft(Vector3[] axis,Vector2[] radius,int sides,int steps,Color dye)
            {
                if(axis.Length<2||axis.Length!=radius.Length)throw new ArgumentException("Invalid loft sections");
                int last=(axis.Length-1)*steps;
                var previous=new Vector3[sides];var current=new Vector3[sides];
                Vector3 tangent=(axis[1]-axis[0]).normalized;
                Vector3 across=Vector3.ProjectOnPlane(Mathf.Abs(tangent.x)>.85f?Vector3.forward:Vector3.right,tangent).normalized;
                Vector3 priorCenter=axis[0];float distance=0;
                float circumference=Mathf.Max(.08f,Mathf.PI*(radius[axis.Length/2].x+radius[axis.Length/2].y));
                for(int ring=0;ring<=last;ring++)
                {
                    int k=Math.Min(ring/steps,axis.Length-2);float t=ring/(float)steps-k;
                    int a=Math.Max(0,k-1),d=Math.Min(axis.Length-1,k+2);
                    var center=Catmull(axis[a],axis[k],axis[k+1],axis[d],t);
                    Vector3 derivative=.5f*((axis[k+1]-axis[a])+2*t*(2*axis[a]-5*axis[k]+4*axis[k+1]-axis[d])+
                        3*t*t*(-axis[a]+3*axis[k]-3*axis[k+1]+axis[d]));
                    if(derivative.sqrMagnitude>1e-9f)tangent=derivative.normalized;
                    across=Vector3.ProjectOnPlane(across,tangent).normalized;
                    var side=Vector3.Cross(tangent,across);
                    Vector2 extent=Catmull(radius[a],radius[k],radius[k+1],radius[d],t);
                    float nextDistance=distance+(center-priorCenter).magnitude;
                    for(int j=0;j<sides;j++)
                    {
                        float angle=j*2*Mathf.PI/sides;
                        current[j]=center+across*(Mathf.Cos(angle)*Mathf.Max(.001f,extent.x))+
                            side*(Mathf.Sin(angle)*Mathf.Max(.001f,extent.y));
                    }
                    for(int j=0;j<sides;j++)
                    {
                        int h=(j+1)%sides;
                        if(ring==0){Triangle(center,current[h],current[j],dye);continue;}
                        var ta=new Vector2(j/(float)sides*circumference,distance);
                        var tb=new Vector2((j+1)/(float)sides*circumference,distance);
                        var tc=new Vector2(tb.x,nextDistance);var td=new Vector2(ta.x,nextDistance);
                        Panel(previous[j],previous[h],current[h],ta,tb,tc);
                        Panel(previous[j],current[h],current[j],ta,tc,td);
                        if(ring==last)Triangle(center,current[j],current[h],dye);
                    }
                    var swap=previous;previous=current;current=swap;priorCenter=center;distance=nextDistance;
                }

                void Panel(Vector3 a,Vector3 b,Vector3 c,Vector2 u,Vector2 v,Vector2 w)
                {
                    int i=vertices.Count;Triangle(a,b,c,dye);
                    if(vertices.Count==i)return;
                    loftUV[i]=u;loftUV[i+1]=v;loftUV[i+2]=w;
                }
            }

            internal static Vector3 Catmull(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t) =>
                .5f*(2*b+(c-a)*t+(2*a-5*b+4*c-d)*(t*t)+(-a+3*b-3*c+d)*(t*t*t));
        }
    }
}
