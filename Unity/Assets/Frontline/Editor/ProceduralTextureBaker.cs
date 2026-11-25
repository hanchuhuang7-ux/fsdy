using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Frontline.Editor
{
    // Water motion and shoreline distance follow the deterministic world geometry. Surface art lives in the scanned arrays.
    internal static class ProceduralTextureBaker
    {
        const string Folder = "Assets/Frontline/Resources/Textures";
        const float Tau = Mathf.PI * 2;

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
        internal static void BakeLakeNormals()
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
