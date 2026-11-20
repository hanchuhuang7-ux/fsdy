using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    internal enum HudTone { Secondary, Primary, Selected, Danger }

    public sealed partial class NativeHud
    {
        enum Slice { Frame, Inset, Shadow, Secondary, Primary, Selected, Danger, Disabled, Badge, Rule, Rail, Sheet, Count }

        const int TileSize=48, TileStride=TileSize+2, TileColumns=6;
        readonly Sprite[] slices=new Sprite[(int)Slice.Count];
        readonly Sprite[] glyphs=new Sprite[(int)Glyph.Count];
        Texture2D themeTexture;
        static readonly Color titleGold=new Color(.97f,.86f,.66f);
        static readonly Color buttonInk=new Color(.16f,.127f,.087f);
        static readonly Color mutedButtonText=new Color(.64f,.66f,.63f);
        static readonly Color quietBadge=new Color(.32f,.39f,.38f);
        static readonly Color warmBadge=new Color(.53f,.40f,.22f);
        static readonly Color goodBadge=new Color(.29f,.48f,.39f);
        static readonly Color blockedBadge=new Color(.52f,.31f,.28f);

        void InitializeTheme()
        {
            const int count=(int)Slice.Count+(int)Glyph.Count;
            const int width=TileColumns*TileStride,height=(count+TileColumns-1)/TileColumns*TileStride;
            var pixels=new Color32[width*height];
            Paint(Slice.Frame,0x3b4c48,0x172524,0xd2e0d1,15,199,214,68);
            Paint(Slice.Inset,0x65746a,0x3a4842,0xe4dbc1,12,110,130,41);
            Paint(Slice.Secondary,0x647773,0x3a4a48,0xc6d6cb,17,65,110,40);
            Paint(Slice.Primary,0xf6e6bb,0xd1b182,0xfff6da,17,245,242,210);
            Paint(Slice.Selected,0x9a8870,0x5a4b37,0xefd7aa,17,215,218,143);
            Paint(Slice.Danger,0xa06b62,0x593734,0xffc4ad,17,149,170,78);
            Paint(Slice.Disabled,0x54615d,0x414a47,0x999e92,17,60,86,35);
            Paint(Slice.Badge,0xf4eee3,0xd8d9ce,0xf5e8cc,18,224,218,205);
            Paint(Slice.Rail,0x405654,0x152625,0xd4eddf,20,180,204,89);
            Paint(Slice.Sheet,0x384944,0x192625,0xf2e1be,17,234,246,108);
            PaintShadow();
            var rule=TileOrigin((int)Slice.Rule);
            for(int x=0;x<TileSize;x++)
                pixels[rule.x+x+rule.y*width]=new Color32(222,196,154,(byte)Mathf.Min(118,16+14*Mathf.Min(x,TileSize-1-x)));
            for(int i=0;i<glyphs.Length;i++)
            {
                var origin=TileOrigin(slices.Length+i);
                for(int y=0;y<TileSize;y++)
                    for(int x=0;x<TileSize;x++)
                    {
                        var point=new Vector2((x+.5f)*24/TileSize,(y+.5f)*24/TileSize);
                        float coverage=Mathf.Clamp01(.5f-GlyphDistance((Glyph)i,point)*2);
                        pixels[origin.x+x+(origin.y+y)*width]=new Color32(255,255,255,Opacity(coverage));
                    }
            }

            themeTexture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            {
                name="Frontline HUD atlas",
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp
            };
            themeTexture.SetPixels32(pixels);
            themeTexture.Apply(false,true);
            for(int i=0;i<slices.Length;i++)
            {
                var kind=(Slice)i;
                bool thin=kind==Slice.Rule;
                float edge=kind is Slice.Frame or Slice.Inset?17:22;
                var border=thin?new Vector4(6,0,6,0):new Vector4(edge,edge,edge,edge);
                slices[i]=MakeSprite(i,thin?1:TileSize,border,"HUD "+kind);
            }
            for(int i=0;i<glyphs.Length;i++)
                glyphs[i]=MakeSprite(slices.Length+i,TileSize,Vector4.zero,"HUD glyph "+(Glyph)i);

            Sprite MakeSprite(int index,int heightPixels,Vector4 border,string name)
            {
                var origin=TileOrigin(index);
                var sprite=Sprite.Create(themeTexture,new Rect(origin.x,origin.y,TileSize,heightPixels),
                    new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
                sprite.name=name;
                return sprite;
            }

            void Paint(Slice kind,int top,int bottom,int rim,float radius,byte highAlpha,byte lowAlpha,byte rimAlpha)
            {
                var origin=TileOrigin((int)kind);
                Color32 upper=Rgb(top,highAlpha),lower=Rgb(bottom,lowAlpha),outline=Rgb(rim,rimAlpha);
                for(int y=0;y<TileSize;y++)
                    for(int x=0;x<TileSize;x++)
                    {
                        float distance=RoundedDistance(x,y,radius,0);
                        float brightness=(float)y/(TileSize-1);
                        Color32 fill=Color32.Lerp(lower,upper,brightness);
                        if(distance> -1.45f)
                            fill=Color32.Lerp(fill,outline,Mathf.Clamp01((distance+1.45f)*.86f)*(.48f+.52f*brightness));
                        fill.a=(byte)Mathf.RoundToInt(fill.a*Mathf.Clamp01(.5f-distance));
                        pixels[origin.x+x+(origin.y+y)*width]=fill;
                    }
            }

            void PaintShadow()
            {
                var shadow=TileOrigin((int)Slice.Shadow);
                for(int y=0;y<TileSize;y++)
                    for(int x=0;x<TileSize;x++)
                    {
                        float distance=RoundedDistance(x,y,15,10);
                        byte alpha=Opacity(.44f*Mathf.SmoothStep(0,1,Mathf.Clamp01((9f-distance)/14)));
                        pixels[shadow.x+x+(shadow.y+y)*width]=new Color32(0,0,0,alpha);
                    }
            }
        }

        static Vector2Int TileOrigin(int index) => new Vector2Int(index%TileColumns*TileStride+1,index/TileColumns*TileStride+1);

        static byte Opacity(float coverage) => (byte)Mathf.RoundToInt(255*coverage);
        static Color32 Rgb(int rgb,byte a) => new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,a);

        static float RoundedDistance(int x,int y,float radius,float inset)
        {
            float center=(TileSize-1)*.5f, core=TileSize*.5f-radius-inset;
            float dx=Mathf.Abs(x-center)-core, dy=Mathf.Abs(y-center)-core;
            float px=Mathf.Max(0,dx),py=Mathf.Max(0,dy);
            return Mathf.Sqrt(px*px+py*py)+Mathf.Min(0,Mathf.Max(dx,dy))-radius;
        }

        void ReleaseTheme()
        {
            foreach(var sprite in slices)if(sprite)Destroy(sprite);
            foreach(var sprite in glyphs)if(sprite)Destroy(sprite);
            if(themeTexture)Destroy(themeTexture);
        }

        RectTransform Float(Transform parent,string name,float x,float y,float w,float h,Slice slice=Slice.Frame)
        {
            var shadow=Panel(parent,name+" shadow",x-7,y-4,w+14,h+15,Color.white,Slice.Shadow);
            shadow.GetComponent<Image>().raycastTarget=false;
            return Panel(parent,name,x,y,w,h,Color.white,slice);
        }

        void Rule(Transform parent,float x,float y,float width)
        {
            var rect=Panel(parent,"Gold rule",x,y,width,1,Color.white,Slice.Rule);
            rect.GetComponent<Image>().raycastTarget=false;
        }

        Text Badge(Transform parent,string text,float x,float y,float w,float h,int size=11)
        {
            var rect=Panel(parent,"State badge",x,y,w,h,quietBadge,Slice.Badge);
            rect.GetComponent<Image>().raycastTarget=false;
            return Label(rect,text,4,0,w-8,h,size,pale,TextAnchor.MiddleCenter);
        }

        void BadgeLook(Text text,Image background,string caption,bool good=false,bool active=false,bool blocked=false)
        {
            if(text.text!=caption)text.text=caption;
            text.color=good?thriving:active?gold:blocked?danger:pale;
            background.color=good?goodBadge:active?warmBadge:blocked?blockedBadge:quietBadge;
        }

        void ApplyButtonLook(NativeHudButton button,Text caption,HudTone tone)
        {
            button.SetTheme(button.image,caption,slices[(int)Slice.Secondary+(int)tone],slices[(int)Slice.Disabled],
                tone==HudTone.Primary?buttonInk:tone==HudTone.Selected?titleGold:white,mutedButtonText,tone);
        }

        void SetButtonTone(Button button,HudTone tone)
        {
            var themed=(NativeHudButton)button;
            if(themed.Tone!=tone)ApplyButtonLook(themed,themed.Caption,tone);
        }
    }
}
