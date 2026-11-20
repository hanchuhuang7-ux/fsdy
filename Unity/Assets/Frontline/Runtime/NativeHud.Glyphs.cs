using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        enum Glyph { Coin,Grain,Timber,Arms,People,Heart,Crown,Council,Codex,Settings,Workshop,Guide,Play,Pause,Speed,Plot,Count }

        Image Mark(Transform parent,Glyph glyph,float x,float y,float size,Color color)
        {
            var rect=Node(parent,"Glyph "+glyph);
            Rect(rect,x,y,size,size);
            var image=rect.gameObject.AddComponent<Image>();
            image.sprite=glyphs[(int)glyph];
            image.color=color;
            image.raycastTarget=false;
            return image;
        }

        void MarkButton(Button button,Glyph glyph,float size=16,float leading=10,int fontSize=0)
        {
            var themed=(NativeHudButton)button;
            var rect=(RectTransform)button.transform;
            float w=rect.sizeDelta.x,h=rect.sizeDelta.y;
            var caption=themed.Caption;
            bool onlyGlyph=caption.text.Length==0;
            var icon=Mark(rect,glyph,onlyGlyph?(w-size)/2:leading,(h-size)/2,size,caption.color);
            themed.MarkImage=icon;
            if(onlyGlyph)return;
            float textX=leading+size+4;
            Rect(caption.rectTransform,textX,0,w-textX-3,h);
            caption.alignment=TextAnchor.MiddleLeft;
            if(fontSize>0)caption.fontSize=fontSize;
        }

        static float GlyphDistance(Glyph kind,Vector2 p)
        {
            const float width=.93f;
            float Line(float x,float y,float tx,float ty,float radius=width)
            {
                var start=new Vector2(x,y);
                var edge=new Vector2(tx-x,ty-y);
                return (p-start-edge*Mathf.Clamp01(Vector2.Dot(p-start,edge)/edge.sqrMagnitude)).magnitude-radius;
            }
            float Disc(float x,float y,float r) => (p-new Vector2(x,y)).magnitude-r;
            float Ring(float x,float y,float r,float stroke=width) => Mathf.Abs(Disc(x,y,r))-stroke;
            float d;
            switch(kind)
            {
                case Glyph.Coin:
                    return Mathf.Min(Ring(12,12,8.1f),Mathf.Min(Line(12,7.5f,12,16.5f),Line(9.5f,12,14.5f,12,.75f)));
                case Glyph.Grain:
                    d=Line(12,3.6f,12,20f);
                    for(int i=0;i<3;i++)
                    {
                        float y=6.5f+i*4;
                        d=Mathf.Min(d,Mathf.Min(Line(7,y+3,12,y),Line(17,y+3,12,y)));
                    }
                    return d;
                case Glyph.Timber:
                    d=Mathf.Min(Line(5,8,5,16),Line(19,8,19,16));
                    d=Mathf.Min(d,Mathf.Min(Line(5,16,12,20),Line(12,20,19,16)));
                    d=Mathf.Min(d,Mathf.Min(Line(5,16,12,12),Line(12,12,19,16)));
                    return Mathf.Min(d,Mathf.Min(Line(12,12,12,4),Mathf.Min(Line(5,8,12,4),Line(12,4,19,8))));
                case Glyph.Arms:
                    d=Mathf.Min(Line(7,6.5f,19,18.5f),Line(8,5,5,8));
                    d=Mathf.Min(d,Mathf.Min(Line(11,8.5f,7.5f,12),Line(16,19,20.5f,20.5f)));
                    return Mathf.Min(d,Line(20.5f,20.5f,19,16f));
                case Glyph.People:
                    d=Mathf.Min(Ring(10,16,3.05f),Line(4.6f,5.2f,5.7f,8));
                    d=Mathf.Min(d,Mathf.Min(Line(5.7f,8,10,9.4f),Line(10,9.4f,14.5f,8)));
                    d=Mathf.Min(d,Line(14.5f,8,16,5.2f));
                    return Mathf.Min(d,Mathf.Min(Ring(17.3f,15.1f,2,.67f),Line(18,9.6f,20,6.8f,.73f)));
                case Glyph.Heart:
                    d=Mathf.Min(Line(4.1f,14,5.5f,17.3f),Line(5.5f,17.3f,9.1f,18.4f));
                    d=Mathf.Min(d,Mathf.Min(Line(9.1f,18.4f,12,15.6f),Line(12,15.6f,15,18.4f)));
                    d=Mathf.Min(d,Mathf.Min(Line(15,18.4f,18.5f,17.3f),Line(18.5f,17.3f,19.9f,14)));
                    return Mathf.Min(d,Mathf.Min(Line(4.1f,14,12,5),Line(19.9f,14,12,5)));
                case Glyph.Crown:
                    d=Line(5,7,19,7);
                    d=Mathf.Min(d,Mathf.Min(Line(5,7,3,17),Line(19,7,21,17)));
                    d=Mathf.Min(d,Mathf.Min(Line(3,17,8,13),Line(8,13,12,19)));
                    return Mathf.Min(d,Mathf.Min(Line(12,19,16,13),Line(16,13,21,17)));
                case Glyph.Council:
                    d=Mathf.Min(Line(4,16,12,20),Line(12,20,20,16));
                    d=Mathf.Min(d,Mathf.Min(Line(4,15,20,15),Line(4,4.5f,20,4.5f)));
                    for(int i=0;i<3;i++)d=Mathf.Min(d,Line(7+5*i,7,7+5*i,12.5f));
                    return d;
                case Glyph.Codex:
                    d=Mathf.Min(Line(12,6,12,16),Line(12,16,8,18));
                    d=Mathf.Min(d,Mathf.Min(Line(8,18,4,17.4f),Line(4,17.4f,4,7.2f)));
                    d=Mathf.Min(d,Mathf.Min(Line(4,7.2f,8,8),Line(8,8,12,6)));
                    d=Mathf.Min(d,Mathf.Min(Line(12,6,16,8),Line(16,8,20,7.2f)));
                    return Mathf.Min(d,Mathf.Min(Line(20,7.2f,20,17.4f),Mathf.Min(Line(20,17.4f,16,18),Line(16,18,12,16))));
                case Glyph.Settings:
                    d=Ring(12,12,6.6f);
                    d=Mathf.Min(d,Ring(12,12,2.5f,.75f));
                    for(int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*.25f,s=Mathf.Sin(a),c=Mathf.Cos(a);
                        d=Mathf.Min(d,Line(12+s*6.3f,12+c*6.3f,12+s*9.1f,12+c*9.1f,1.05f));
                    }
                    return d;
                case Glyph.Workshop:
                    d=Mathf.Min(Line(8,5,15,14,1.35f),Line(11,17,17,12,2));
                    return Mathf.Min(d,Line(9,17.8f,12.5f,20,1.15f));
                case Glyph.Guide:
                    d=Mathf.Min(Ring(12,12,8.2f),Line(9.6f,14,11,15.9f));
                    d=Mathf.Min(d,Mathf.Min(Line(11,15.9f,14.2f,15.2f),Line(14.2f,15.2f,14.2f,13.2f)));
                    d=Mathf.Min(d,Line(14.2f,13.2f,12,11));
                    return Mathf.Min(d,Disc(12,8,1.05f));
                case Glyph.Play:
                    return Mathf.Min(Line(8.1f,5.8f,8.1f,18.2f,1.32f),Mathf.Min(Line(8.1f,18.2f,18.3f,12,1.23f),
                        Line(18.3f,12,8.1f,5.8f,1.23f)));
                case Glyph.Pause:
                    return Mathf.Min(Line(9,6.7f,9,17.3f,1.42f),Line(15,6.7f,15,17.3f,1.42f));
                case Glyph.Speed:
                    return Mathf.Min(Mathf.Min(Line(5,17,10.5f,12),Line(10.5f,12,5,7)),
                        Mathf.Min(Line(13,17,18.5f,12),Line(18.5f,12,13,7)));
                default:
                    d=Mathf.Min(Line(5,6,8,6),Line(5,6,5,9));
                    d=Mathf.Min(d,Mathf.Min(Line(5,18,8,18),Line(5,18,5,15)));
                    d=Mathf.Min(d,Mathf.Min(Line(19,6,16,6),Line(19,6,19,9)));
                    d=Mathf.Min(d,Mathf.Min(Line(19,18,16,18),Line(19,18,19,15)));
                    return Mathf.Min(d,Mathf.Min(Line(9,12,15,12,.79f),Line(12,9,12,15,.79f)));
            }
        }
    }
}
