using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    internal enum HudTone { Secondary, Primary, Selected, Danger }

    public sealed partial class NativeHud
    {
        enum Slice { Frame, Inset, Shadow, Secondary, Primary, Selected, Danger, Disabled, Badge, Rule }

        const int TileSize=24, TileStride=TileSize+2, TileColumns=5;
        readonly Sprite[] slices=new Sprite[10];
        Texture2D themeTexture;
        static readonly Color titleGold=new Color(.96f,.77f,.45f);
        static readonly Color buttonInk=new Color(.15f,.106f,.063f);
        static readonly Color mutedButtonText=new Color(.58f,.56f,.51f);
        static readonly Color quietBadge=new Color(.25f,.26f,.25f);
        static readonly Color warmBadge=new Color(.43f,.32f,.17f);
        static readonly Color goodBadge=new Color(.23f,.37f,.29f);
        static readonly Color blockedBadge=new Color(.39f,.23f,.19f);

        void InitializeTheme()
        {
            const int width=TileColumns*TileStride, height=2*TileStride;
            var pixels=new Color32[width*height];
            Paint(Slice.Frame,0xcac4b0,0xa4a49a,0xffdd9b,0xece9dd,6);
            Paint(Slice.Inset,0xc4ba9f,0xaba693,0xe8cc94,0xdfd7bc,5);
            Paint(Slice.Secondary,0x414846,0x292e2d,0x756b57,0x535a53,5);
            Paint(Slice.Primary,0xf3ce83,0xc69752,0xffdfa0,0xffdfa5,5);
            Paint(Slice.Selected,0x725e3b,0x493b28,0xddae61,0x9d8150,5);
            Paint(Slice.Danger,0x743e36,0x502925,0xad6859,0x925246,5);
            Paint(Slice.Disabled,0x3c3c38,0x30302d,0x56544b,0x494840,5);
            Paint(Slice.Badge,0xddd9c8,0xc2c0b2,0xece6d2,0xeee7d3,10);
            PaintShadow();
            var rule=TileOrigin(Slice.Rule);
            for(int x=0;x<TileSize;x++)
                pixels[rule.x+x+rule.y*width]=new Color32(222,177,100,(byte)Mathf.Min(235,48+37*Mathf.Min(x,TileSize-1-x)));

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
                var origin=TileOrigin(kind);
                bool thin=kind==Slice.Rule;
                float edge=kind==Slice.Badge||kind==Slice.Shadow?11:7;
                var border=thin?new Vector4(6,0,6,0):new Vector4(edge,edge,edge,edge);
                var sprite=Sprite.Create(themeTexture,new Rect(origin.x,origin.y,TileSize,thin?1:TileSize),
                    new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);
                sprite.name="HUD "+kind;
                slices[i]=sprite;
            }

            void Paint(Slice kind,int top,int bottom,int rim,int glint,float radius)
            {
                Vector2Int origin=TileOrigin(kind);
                Color32 upper=Rgb(top), lower=Rgb(bottom), outline=Rgb(rim), light=Rgb(glint);
                for(int y=0;y<TileSize;y++)
                    for(int x=0;x<TileSize;x++)
                    {
                        float distance=RoundedDistance(x,y,radius,0);
                        float brightness=Mathf.Clamp01((y-2f)/(TileSize-5));
                        Color32 fill=distance> -1.25f?outline:y==TileSize-3?light:Color32.Lerp(lower,upper,brightness);
                        fill.a=(byte)Mathf.RoundToInt(255*Mathf.Clamp01(.5f-distance));
                        pixels[origin.x+x+(origin.y+y)*width]=fill;
                    }
            }

            void PaintShadow()
            {
                var shadow=TileOrigin(Slice.Shadow);
                for(int y=0;y<TileSize;y++)
                    for(int x=0;x<TileSize;x++)
                    {
                        float distance=RoundedDistance(x,y,6,4);
                        byte alpha=(byte)Mathf.RoundToInt(165*Mathf.SmoothStep(0,1,Mathf.Clamp01((4-distance)/7)));
                        pixels[shadow.x+x+(shadow.y+y)*width]=new Color32(0,0,0,alpha);
                    }
            }
        }

        static Vector2Int TileOrigin(Slice kind) =>
            new Vector2Int((int)kind%TileColumns*TileStride+1,(int)kind/TileColumns*TileStride+1);

        static Color32 Rgb(int rgb) => new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);

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
            if(themeTexture)Destroy(themeTexture);
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
