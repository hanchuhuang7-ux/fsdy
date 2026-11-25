using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Frontline.Editor
{
    // The 16 texel wrap around every swatch is a continuation of the same periodic pattern.
    // RGB stores relative albedo, A stores roughness. Normals are explicitly packed XYZ.
    internal static class ProceduralTextureBaker
    {
        const string Folder = "Assets/Frontline/Resources/Textures";
        const int Tile = 256, Border = 16, Period = Tile - 2 * Border;
        const float Tau = Mathf.PI * 2;
        struct Swatch { public Color pigment; public float height, rough; }

        static int Wrap(int x,int period) => (x % period + period) % period;
        static float Hash(int x,int y,int seed)
        {
            unchecked {
                uint h=(uint)(x*374761393+y*668265263+seed*144269503);
                h=(h^(h>>13))*1274126177u;
                return ((h^(h>>16))&65535)/65535f;
            }
        }
        static float Noise(float u,float v,int nx,int ny,int seed)
        {
            float x=u*nx,y=v*ny;
            int a=Mathf.FloorToInt(x),b=Mathf.FloorToInt(y);
            float s=Mathf.SmoothStep(0,1,x-a),t=Mathf.SmoothStep(0,1,y-b);
            float Corner(int i,int j)=>Hash(Wrap(i,nx),Wrap(j,ny),seed);
            return Mathf.Lerp(Mathf.Lerp(Corner(a,b),Corner(a+1,b),s),Mathf.Lerp(Corner(a,b+1),Corner(a+1,b+1),s),t);
        }
        static Swatch Paint(int kind,float u,float v)
        {
            float grain=Noise(u,v,89,89,17),mid=Noise(u,v,19,19,39),wash=Noise(u,v,5,5,11);
            float value=.81f+.12f*grain,relief=grain*.12f,rough=.78f;
            Color tint=Color.white;
            switch(kind)
            {
                case 0: // Alternating courses, fractured limestone and damp pockets along the mortar.
                    float course=v*6+.051f*(mid-.5f);
                    int row=Mathf.FloorToInt(course);
                    float stone=u*3+(row&1)*.5f+.08f*(wash-.5f);
                    float sx=Mathf.Repeat(stone,1),sy=Mathf.Repeat(course,1);
                    float bed=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(Mathf.Min(sx,1-sx)*23,Mathf.Min(sy,1-sy)*14)));
                    float block=Hash(Wrap(Mathf.FloorToInt(stone),3),Wrap(row,6),22);
                    value=.68f+bed*(.13f+.19f*block)+.17f*(grain-.5f)+.10f*(mid-.5f);
                    relief=bed*.53f+mid*.16f+grain*.12f;
                    tint=Color.Lerp(Color.white,new Color(.78f,.85f,.61f),Mathf.Clamp01((wash-.48f)*2)*(1-bed*.72f));
                    break;
                case 1:
                    float drift=Noise(u,v,5,3,28);
                    float fibre=.5f+.5f*Mathf.Sin(Tau*(u*18+drift*1.3f+Noise(u,v,6,11,37)*.65f));
                    float saw=Noise(u,v,57,4,6);
                    float knot=Mathf.Cos(Tau*(u*4+drift*.63f))*.5f+.5f;
                    value=.74f+.09f*fibre+.14f*saw+.054f*knot;
                    relief=.19f*fibre+.14f*saw+.075f*knot;rough=.66f+.22f*saw;
                    tint=new Color(1,.965f,.91f);break;
                case 2:
                    value=.88f+.08f*grain+.10f*mid;
                    relief=.28f*mid+.21f*grain+.12f*wash;
                    tint=Color.Lerp(Color.white,new Color(1,.93f,.80f),wash*.51f);
                    rough=.89f;break;
                case 3:
                    float tileRow=v*8;
                    float courseY=Mathf.Repeat(tileRow,1),courseX=Mathf.Repeat(u*7+(.5f*(Mathf.FloorToInt(tileRow)&1)),1);
                    float lap=Mathf.SmoothStep(0,1,Mathf.Clamp01(courseY*15));
                    float seam=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(courseX,1-courseX)*14));
                    relief=.39f*lap*(.45f+.55f*courseY)+.17f*seam+.11f*mid;
                    value=.64f+.19f*lap+.10f*seam+.13f*(wash-.4f)+.09f*mid;
                    rough=.66f+.2f*mid;break;
                case 4:
                    float brushed=Noise(u,v,7,98,25);
                    float pit=Mathf.Clamp01((grain-.66f)*4);
                    relief=.09f*brushed-.10f*pit+.08f*mid;
                    value=.86f+.12f*brushed-.12f*pit;rough=.25f+.32f*brushed+.15f*pit;break;
                case 5:
                    float warp=Mathf.Cos(Tau*32*u),weft=Mathf.Cos(Tau*32*v);
                    float yarn=warp*weft*.34f+warp*.09f+weft*.09f;
                    relief=.25f+yarn*.36f+.1f*grain;value=.86f+yarn*.20f+.12f*(grain-.5f);rough=.94f;break;
                case 6:
                    float blades=Noise(u,v,64,23,13),gravel=Mathf.Clamp01((mid-.62f)*3);
                    value=.67f+.16f*blades+.14f*grain+.13f*wash+gravel*.09f;
                    tint=Color.Lerp(new Color(.91f,1,.85f),new Color(1,.91f,.77f),wash);
                    relief=grain*.21f+blades*.22f+gravel*.37f;rough=.82f+.15f*grain;break;
                case 7:
                    float leaf=Noise(u,v,23,33,98);
                    float canopy=0,vein=0;
                    for(int layer=0;layer<2;layer++)
                    {
                        int cells=layer==0?11:16;
                        float xx=u*cells+layer*.37f,yy=v*cells+.45f*Mathf.Sin(Tau*u);
                        int col=Mathf.FloorToInt(xx),band=Mathf.FloorToInt(yy);
                        float orientation=Hash(Wrap(col,cells),Wrap(band,cells),72+layer)*Tau;
                        float lx=Mathf.Repeat(xx,1)-.5f,ly=Mathf.Repeat(yy,1)-.5f;
                        float along=lx*Mathf.Cos(orientation)+ly*Mathf.Sin(orientation);
                        float across=-lx*Mathf.Sin(orientation)+ly*Mathf.Cos(orientation);
                        float blade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(across)*3.5f+Mathf.Abs(along)*1.94f-.71f)*4));
                        canopy=Mathf.Max(canopy,blade);
                        vein+=blade*(1-Mathf.Clamp01(Mathf.Abs(across)*28f))*.5f;
                    }
                    value=.66f+.12f*leaf+.10f*mid+.13f*canopy+.034f*vein;
                    tint=Color.Lerp(new Color(.87f,.97f,.75f),new Color(1,1,.93f),leaf);
                    relief=leaf*.11f+.075f*grain+.16f*canopy;rough=.75f+.18f*mid;break;
            }
            return new Swatch {pigment=tint*value,height=relief,rough=rough};
        }

        public static void BakeAtlas()
        {
            var albedo=new Color32[Tile*4*Tile*2];
            var normals=new Color32[albedo.Length];
            var source=new Swatch[Period*Period];
            for(int kind=0;kind<8;kind++)
            {
                for(int y=0;y<Period;y++)for(int x=0;x<Period;x++)
                    source[y*Period+x]=Paint(kind,(x+.5f)/Period,(y+.5f)/Period);
                Swatch At(int x,int y)=>source[Wrap(y,Period)*Period+Wrap(x,Period)];
                for(int y=0;y<Tile;y++)for(int x=0;x<Tile;x++)
                {
                    int u=x-Border,v=y-Border,index=((kind/4)*Tile+y)*Tile*4+(kind%4)*Tile+x;
                    var p=At(u,v);p.pigment.a=p.rough;albedo[index]=p.pigment;
                    var n=new Vector3((At(u-1,v).height-At(u+1,v).height)*1.9f,
                        (At(u,v-1).height-At(u,v+1).height)*1.9f,1).normalized;
                    normals[index]=new Color(.5f+.5f*n.x,.5f+.5f*n.y,.5f+.5f*n.z,1);
                }
            }
            Store("SurfaceAlbedoRoughness",Tile*4,Tile*2,albedo,true);
            Store("SurfaceNormals",Tile*4,Tile*2,normals,false);
            BakeLakeNormals();
            BakeLaneDetail();
        }

        static void BakeLaneDetail()
        {
            const int size=256;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float baseSoil=Noise(u,v,13,17,22),grain=Noise(u,v,97,83,48),moss=Noise(u,v,6,9,71);
                float gravel=Mathf.SmoothStep(0,1,Mathf.Clamp01((Noise(u,v,38,47,94)-.65f)*4.4f));
                float course=v*6+.047f*(baseSoil-.5f);
                int row=Mathf.FloorToInt(course);
                float col=u*3.0f+.5f*(row&1)+.14f*(Hash(0,Wrap(row,6),11)-.5f)+.035f*(moss-.5f);
                float along=Mathf.Repeat(col,1),across=Mathf.Repeat(course,1);
                float edge=Mathf.Min(Mathf.Min(along,1-along)*37,Mathf.Min(across,1-across)*21);
                float shade=.75f+.22f*Hash(Wrap(Mathf.FloorToInt(col),3),Wrap(row,6),44);
                pixels[y*size+x]=new Color(.46f*baseSoil+.40f*grain+.14f*moss,gravel,
                    Mathf.SmoothStep(0,1,Mathf.Clamp01(edge))*shade,moss*.64f+baseSoil*.36f);
            }
            Store("LaneDetail",size,size,pixels,true,false,true);
        }

        static void BakeLakeNormals()
        {
            const int size=256;
            var water=new Color32[size*size];
            var height=new float[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float warp=Noise(u,v,6,8,94),gust=Noise(u,v,9,5,51);
                height[y*size+x]=.24f*Mathf.Sin(Tau*(9*u+16*v+.94f*warp))+
                    .21f*Mathf.Sin(Tau*(-15*u+7*v+1.14f*gust))+
                    .16f*Mathf.Sin(Tau*(17*u+11*v+1.65f*warp))+.21f*Noise(u,v,36,43,12);
            }
            float At(int x,int y)=>height[Wrap(y,size)*size+Wrap(x,size)];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                water[y*size+x]=new Color(.5f+.66f*(At(x-1,y)-At(x+1,y)),.5f+.66f*(At(x,y-1)-At(x,y+1)),1,1);
            Store("LakeNormals",size,size,water,false,false,true);
        }

        // Bake distance to the real waterline from the same cliff profiles that form the land mesh.
        public static void BakeShore(List<Vector3>[] loops,Vector4 rect)
        {
            const int side=1024;
            const float range=16;
            var field=new float[side*side];
            for(int i=0;i<field.Length;i++)field[i]=range*range;
            float dx=rect.z/side,dz=rect.w/side;
            foreach(var loop in loops)for(int i=0;i<loop.Count;i++)
            {
                var a=loop[i];var b=loop[(i+1)%loop.Count];float x=b.x-a.x,z=b.z-a.z;
                float inv=1/Mathf.Max(.00001f,x*x+z*z);
                int x0=Mathf.Max(0,Mathf.FloorToInt((Mathf.Min(a.x,b.x)-range-rect.x)/dx));
                int x1=Mathf.Min(side-1,Mathf.CeilToInt((Mathf.Max(a.x,b.x)+range-rect.x)/dx));
                int z0=Mathf.Max(0,Mathf.FloorToInt((Mathf.Min(a.z,b.z)-range-rect.y)/dz));
                int z1=Mathf.Min(side-1,Mathf.CeilToInt((Mathf.Max(a.z,b.z)+range-rect.y)/dz));
                for(int j=z0;j<=z1;j++)for(int k=x0;k<=x1;k++)
                {
                    float px=rect.x+(k+.5f)*dx-a.x,pz=rect.y+(j+.5f)*dz-a.z;
                    float t=Mathf.Clamp01((x*px+z*pz)*inv);
                    px-=t*x;pz-=t*z;
                    int index=j*side+k;field[index]=Mathf.Min(field[index],px*px+pz*pz);
                }
            }
            var pixels=new Color32[field.Length];
            for(int i=0;i<pixels.Length;i++)
            {
                byte d=(byte)Mathf.RoundToInt(255*Mathf.Sqrt(field[i])/range);
                pixels[i]=new Color32(d,d,d,255);
            }
            Store("ShoreDistance",side,side,pixels,false,true);
        }

        static void Store(string name,int width,int height,Color32[] pixels,bool alpha,bool distance=false,bool repeat=false)
        {
            Directory.CreateDirectory(Folder);
            string path=Folder+"/"+name+".png";
            var image=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            image.SetPixels32(pixels);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false; // Linear data; pigments are relative to the vertex albedo in the Gamma project.
            importer.alphaSource=alpha?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=false;importer.mipmapEnabled=true;importer.anisoLevel=distance?2:4;
            importer.wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.isReadable=false;importer.maxTextureSize=1024;
            void Platform(string target,TextureImporterFormat format)
            {
                var p=importer.GetPlatformTextureSettings(target);p.name=target;p.overridden=true;
                p.maxTextureSize=1024;p.format=format;p.compressionQuality=80;importer.SetPlatformTextureSettings(p);
            }
            Platform("Standalone",distance?TextureImporterFormat.R8:alpha?TextureImporterFormat.DXT5:TextureImporterFormat.BC5);
            Platform("WebGL",distance?TextureImporterFormat.R8:alpha?TextureImporterFormat.ETC2_RGBA8:TextureImporterFormat.ETC2_RGB4);
            importer.SaveAndReimport();
        }
    }
}
