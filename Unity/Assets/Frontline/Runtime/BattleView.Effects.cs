using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        Batch accentRings,accentMist;
        internal int VisibleOrderAccents {get;private set;}
        internal int VisibleImpactAccents {get;private set;}
        internal int AccentInstances => accentRings.count+accentMist.count;
        internal bool AccentCasters => accentRings.mesh.castShadow||accentMist.mesh.castShadow;

        void InitializeAccents()
        {
            var wave=Load("ring",true);
            // Thin translucent bands use the existing inexpensive distant mesh at every quality.
            var band=new MeshSet {
                high=wave.low,low=wave.low,highVertices=wave.lowVertices,lowVertices=wave.lowVertices,
                atmosphere=true,castShadow=false,receiveShadow=false,localBounds=wave.low.bounds
            };
            accentRings=Buffer(band,moving,0);
            accentMist=new Batch{mesh=dust};
            moving.Add(accentMist);
        }

        static void Accent(Batch page,Matrix4x4 matrix,Color tint,float softness=0)
        {
            // One platform-sized reusable page per mesh. Never enter Batch.Add's growth branch.
            if(page.count<Capacity)page.Add(matrix,tint,softness);
        }

        void DrawPulseRing(Vector3 p,float radius,float aspect,float thickness,Color tint,float alpha)
        {
            tint.a=alpha;
            Accent(accentRings,Pose(p,Quaternion.identity,new Vector3(radius,thickness,radius*aspect)),tint);
        }

        void DrawActiveOrder(OrderEntry order,int tick)
        {
            float ground=Data.Height(Config.map,order.x,order.z);
            if(!Catalog.Finite(ground))return;
            var at=new Vector3(order.x,ground+.076f,order.z);
            bool focus=order.type=="focus", shield=order.type=="fortify";
            float radius=focus?4:6;
            if(!InView(at,focus?8:6))return;
            VisibleOrderAccents|=focus?2:shield?4:1;
            var tint=OrderTint(order.type);
            float f=Mathf.Clamp01((tick-order.tick)/38f),fade=1-f;
            DrawPulseRing(at,radius,1,1.5f,tint,.33f+.58f*fade);
            if(focus)
            {
                // The inner reticle is the target zone, while the light outer band indicates the troops reached.
                DrawPulseRing(at,8,1,.9f,tint,.17f+.30f*fade);
                for(int i=0;i<4;i++)
                {
                    float step=i*Mathf.PI*.5f;
                    var mark=at+new Vector3(Mathf.Sin(step),0,Mathf.Cos(step))*(radius-.16f);
                    tint.a=.38f+.27f*fade;
                    Accent(accentMist,Pose(mark,Quaternion.Euler(0,i*90,0),new Vector3(.19f,1,1.12f)),tint,.25f);
                }
            }
            else if(shield)DrawPulseRing(at+Vector3.up*.035f,radius*.92f,1,1.3f,tint,.29f+.38f*fade);

            if(fade<=0)return;
            float spread=Mathf.Lerp(.38f,radius,1-fade*fade);
            DrawPulseRing(at+Vector3.up*.025f,spread,1,2,Color.Lerp(tint,White,.43f),.95f*fade);
            if(QualityLevel>0)
                DrawPulseRing(at+Vector3.up*.075f,Mathf.Max(.22f,spread-.48f),1,1,tint,.44f*fade);
            tint.a=.20f*fade;
            Accent(accentMist,Pose(at,Quaternion.identity,new Vector3(spread*1.85f,1,spread*1.85f)),tint,1);

            // Short columns rise off the centre and the expanding band so nearby bodies cannot hide the pulse.
            tint.a=.57f*fade;
            var column=at+Vector3.up*(.90f+2.10f*f);
            var size=new Vector3(focus?.57f:.72f,1,1.52f+1.95f*f);
            var face=ViewCamera.transform.rotation*Quaternion.Euler(90,0,0);
            Accent(accentMist,Pose(column,face,size),tint,1);
            if(QualityLevel>0)
            {
                var offset=ViewCamera.transform.right*(.48f*radius);
                offset.y=0;
                tint.a*=.76f;
                Accent(accentMist,Pose(column+offset,face,size*.74f),tint,1);
                if(QualityLevel==2)Accent(accentMist,Pose(column-offset,face,size*.74f),tint,1);
            }
        }

        void DrawBattleAccents(Frame frame)
        {
            VisibleOrderAccents=VisibleImpactAccents=0;
            foreach(var order in frame.commander.active)if(order.endTick>frame.tick)DrawActiveOrder(order,frame.tick);
            int remaining=QualityLevel==2?7:QualityLevel==1?4:2;
            // Give rarer blasts the first slots. Recent collisions fill the remaining positions.
            for(int pass=0;pass<2&&remaining>0;pass++)
                for(int i=frame.effects.Count-1;i>=0&&remaining>0;i--)
                {
                    var effect=frame.effects[i];
                    bool blast=effect.type=="explosion";
                    if(pass==0?!blast:effect.type!="slam"&&effect.type!="charge")continue;
                    if(DrawImpactPulse(effect,blast))remaining--;
                }
        }

        bool DrawImpactPulse(EffectFrame effect,bool blast)
        {
            if(effect.life<=0||effect.maxLife<=0)return false;
            var at=new Vector3(effect.x,effect.y,effect.z);
            if(!InView(at,5))return false;
            float ground=Data.Height(Config.map,effect.x,effect.z);
            if(!Catalog.Finite(ground))return false;
            at.y=ground+.087f;
            bool charge=effect.type=="charge";
            VisibleImpactAccents|=blast?1:charge?4:2;
            float f=Mathf.Clamp01(1-effect.life/effect.maxLife),fade=1-f;
            float radius=Mathf.Lerp(.34f,blast?4.5f:charge?2.85f:3.4f,1-fade*fade);
            var tint=blast?new Color(1,.63f,.29f):charge?new Color(.94f,.79f,.50f):new Color(1,.74f,.39f);
            DrawPulseRing(at,radius,1,1.6f,tint,.94f*fade);
            if(QualityLevel>0)
                DrawPulseRing(at+Vector3.up*.042f,Mathf.Max(.19f,radius*.71f),1,1.4f,
                    Color.Lerp(tint,White,.41f),.8f*fade);
            int puffs=QualityLevel==2?3:QualityLevel==1?2:1;
            for(int i=0;i<puffs;i++)
            {
                float a=i*2.39996f+effect.id*1.39f;
                var drift=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*(.29f+.13f*i);
                float width=(blast?1.4f:.9f)+f*1.95f;
                drift.y=.13f+f*(blast?.98f:.35f);
                var ash=i==0&&blast?new Color(.33f,.25f,.19f,.31f*fade):new Color(.77f,.62f,.43f,.27f*fade);
                Accent(accentMist,Pose(at+drift,ViewCamera.transform.rotation*Quaternion.Euler(90,0,0),
                    new Vector3(width,1,width*.63f)),ash,1);
            }
            return true;
        }

    }
}
