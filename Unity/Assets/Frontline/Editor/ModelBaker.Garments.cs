using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static readonly Color UndyedLinen=new Color(.88f,.848f,.746f,0),
            Madder=new Color(.54f,.206f,.165f,0), Walnut=new Color(.266f,.163f,.106f,0),
            WroughtIron=new Color(.382f,.443f,.455f,0), OldGold=new Color(.80f,.592f,.299f,0),
            DyedWool=new Color(.88f,.855f,.770f,1);
        static readonly Vector3[] TunicSections={
            V(.47f,-.49f,.335f),V(.435f,-.365f,.322f),V(.357f,-.143f,.283f),
            V(.46f,.148f,.326f),V(.50f,.229f,.347f),V(.245f,.337f,.244f),V(.155f,.405f,.151f)
        };

        static Color Pigment(Color dye,float light) => new Color(dye.r*light,dye.g*light,dye.b*light,dye.a);

        static void TailoredTunic(Geometry g,Color dye,bool split)
        {
            const int sides=14;
            Vector3 Point(int row,int i)
            {
                var ring=TunicSections[row];
                float a=(i+.5f)*2*Mathf.PI/sides;
                float gather=row<=1?1+(i%2==0?.043f:-.048f):1;
                return V(Mathf.Sin(a)*ring.x*.5f*gather,
                    ring.y+(row==0&&split&&(i%sides==0||i%sides==7)?.102f:0),
                    Mathf.Cos(a)*ring.z*.5f*gather);
            }
            for(int row=0;row<TunicSections.Length-1;row++)
                for(int side=0;side<sides;side++)
                {
                    var a=Point(row,side);var b=Point(row,side+1);
                    var c=Point(row+1,side+1);var d=Point(row+1,side);
                    float crease=side%2==0?.91f:1.02f;
                    var hem=Pigment(dye,crease*(row==0?.81f:.91f));
                    var shoulder=Pigment(dye,crease*(row>=3?1.10f:1));
                    g.Gradient(a,b,c,hem,hem,shoulder);
                    g.Gradient(a,c,d,hem,shoulder,shoulder);
                }
        }

        static void Portrait(Geometry g,int style,bool helmet)
        {
            // The bare head is 0.27 m tall in model space, about one sixth of
            // the nearly 1.6 m silhouette. Hat peaks and feathers sit above it.
            FaceBust(g,V(0,.561f,.025f),V(.116f,.137f,.119f),style,helmet,false);
            g.Frustum(V(0,.374f,.014f),V(.137f,0,.135f),V(0,.475f,.031f),V(.113f,0,.117f),Skin*.88f,6);
        }

        static void WoolHood(Geometry g,Color wool,bool tail)
        {
            g.Ellipsoid(V(0,.588f,-.029f),V(.151f,.148f,.141f),wool,8,3);
            g.Frustum(V(0,.224f,-.026f),V(.448f,0,.358f),V(0,.465f,-.026f),V(.25f,0,.226f),wool,8);
            g.Quad(V(-.055f,.419f,.107f),V(.055f,.419f,.107f),V(.137f,.280f,.139f),V(-.137f,.280f,.139f),
                Pigment(wool,.91f),true);
            if(tail)
            {
                g.Triangle(V(-.063f,.656f,-.112f),V(.071f,.651f,-.135f),V(.126f,.314f,-.275f),Pigment(wool,.87f));
                g.Triangle(V(-.063f,.656f,-.112f),V(.126f,.314f,-.275f),V(-.04f,.36f,-.221f),wool);
            }
        }

        static void KettleHat(Geometry g,Color metal)
        {
            g.Frustum(V(0,.573f,.015f),V(.385f,0,.346f),V(0,.626f,0),V(.255f,0,.258f),Pigment(metal,.84f),10);
            g.Frustum(V(0,.623f,0),V(.252f,0,.256f),V(0,.755f,-.015f),V(.123f,0,.148f),metal,10);
            g.Frustum(V(0,.565f,.015f),V(.385f,0,.346f),V(0,.579f,.015f),V(.385f,0,.346f),LightSteel,10);
        }

        static void Bascinet(Geometry g,Color metal,Color fringe)
        {
            g.surface=Surface.Metal;
            g.Loft(new[]{V(0,.493f,-.026f),V(0,.568f,-.020f),V(0,.655f,-.004f),V(0,.716f,-.022f),V(0,.793f,-.054f)},
                new[]{Section(.134f,.122f),Section(.149f,.141f),Section(.143f,.158f),Section(.102f,.111f),Section(.012f,.017f)},
                14,2,metal);
            g.Frustum(V(0,.37f,-.018f),V(.30f,0,.245f),V(0,.525f,-.022f),V(.25f,0,.215f),Pigment(fringe,.78f),12);
            // A continuous visor band overlaps the shell on both hinges. The ocular slit is recessed
            // between its raised lower plate and the brow rather than cut all the way through the head.
            Vector3 Face(float t,int row)
            {
                float a=t*1.43f,front=Mathf.Cos(a);
                float y=row==0?.492f:row==1?.572f:row==2?.590f:.636f;
                float nose=row==0?.152f:row==1?.207f:row==2?.190f:.182f;
                return V(.150f*Mathf.Sin(a),y+(1-front)*.024f,.008f+front*nose);
            }
            for(int row=0;row<3;row++)for(int j=0;j<12;j++)
            {
                float a=j/6f-1,b=(j+1)/6f-1;
                var dye=row==1?Dark:row==2?LightSteel:metal;
                g.Quad(Face(a,row),Face(b,row),Face(b,row+1),Face(a,row+1),dye,true);
            }
            for(int s=-1;s<=1;s+=2)
            {
                g.Ellipsoid(V(s*.151f,.585f,.042f),V(.012f,.018f,.019f),OldGold,8,3);
                for(int i=0;i<3;i++)
                {
                    float t=s*(.22f+i*.11f);
                    var p=Vector3.Lerp(Face(t,0),Face(t,1),.56f)+V(0,0,.002f);
                    g.Quad(p+V(-.003f,0,0),p+V(.003f,0,0),p+V(.004f,.020f,0),p+V(-.002f,.020f,0),Dark,true);
                }
            }
            g.surface=Surface.Auto;
        }

        static void ShoulderMantle(Geometry g,Color dye,float length)
        {
            // A lofted strip of wool follows the clavicle, shoulder blades and flared hem.
            Vector3 OnFold(float u,float t)
            {
                var collar=V(u*.41f,.305f,-.129f-.046f*Mathf.Cos(u*2.8f));
                var blade=V(u*.51f,.158f,-.192f-.026f*Mathf.Cos(u*3.2f));
                var waist=V(u*.54f,-.061f+.18f*length,-.22f-.038f*Mathf.Cos(u*13));
                var hem=V(u*.608f,length+.077f*Mathf.Abs(u)+.014f*Mathf.Sin(u*18),
                    -.282f-.058f*Mathf.Cos(u*19+.56f));
                if(t<.34f)return Geometry.Catmull(collar,collar,blade,waist,t/.34f);
                if(t<.67f)return Geometry.Catmull(collar,blade,waist,hem,(t-.34f)/.33f);
                return Geometry.Catmull(blade,waist,hem,hem,(t-.67f)/.33f);
            }
            g.surface=Surface.Cloth;
            for(int i=0;i<12;i++)for(int j=0;j<6;j++)
            {
                float u=i/12f-.5f,v=(i+1)/12f-.5f,t=j/6f,s=(j+1)/6f;
                g.Quad(OnFold(v,t),OnFold(u,t),OnFold(u,s),OnFold(v,s),Pigment(dye,.96f+.04f*Mathf.Cos(u*19)),true);
            }
            g.surface=Surface.Auto;
        }


        static void ArticulatedSleeve(Geometry g,string unit,bool left)
        {
            float side=left?-1:1;
            bool plate=!left&&(unit=="spear"||unit=="shield"||unit=="giant"||unit=="cavalry");
            var joint=V(side*.282f,.251f,.009f);
            var elbow=V(side*.354f,.040f,-.043f);
            var grip=left?V(-.36f,-.14f,.078f):unit switch {
                "archer"=>V(.36f,.10f,.51f), "crossbow"=>V(.36f,.035f,.22f),
                "medic"=>V(.36f,-.115f,.28f),"giant"=>V(.36f,-.12f,.36f),
                "bomber"=>V(.36f,-.043f,.29f),"spear"=>V(.36f,.041f,.085f),
                "cavalry"=>V(.36f,.041f,.085f),_=>V(.36f,.10f,-.071f)
            };
            // The longest ranged item has a forward grip; turn the elbow toward it.
            elbow.z+=Mathf.Max(0,grip.z-.10f)*.53f;
            if(!left&&grip.z<.10f)elbow+=V(side*.028f,-.127f,-.046f);
            var mid=Vector3.Lerp(joint,elbow,.55f)+V(side*.022f,0,0);
            g.surface=Surface.Cloth;
            g.Loft(new[]{joint,mid,elbow},new[]{Section(.093f,.102f),Section(.086f,.09f),Section(.071f,.072f)},9,2,DyedWool);
            g.surface=plate?Surface.Metal:Surface.Cloth;
            g.Ellipsoid(joint, V(.107f,.103f,.110f),plate?WroughtIron:DyedWool,10,4);
            if(plate)
            {
                g.Frustum(joint+V(0,-.058f,0),V(.217f,0,.227f),joint+V(0,-.012f,0),V(.227f,0,.232f),LightSteel,10);
                g.Ellipsoid(elbow,V(.084f,.082f,.092f),WroughtIron,9,4);
            }
            var wrist=Vector3.Lerp(elbow,grip,.78f);
            g.surface=plate?Surface.Metal:Surface.Hide;
            g.Loft(new[]{elbow,Vector3.Lerp(elbow,wrist,.40f),wrist,grip},
                new[]{Section(.078f,.079f),Section(.076f,.074f),Section(.051f,.057f),Section(.05f,.053f)},
                9,2,plate?WroughtIron:Walnut);
            g.surface=Surface.Hide;
            g.Ellipsoid(grip,V(.067f,.065f,.075f),Walnut,9,4);
            if(plate)
            {
                g.surface=Surface.Metal;
                g.Ellipsoid(grip+V(side*.038f,.01f,0),V(.033f,.051f,.060f),Steel,8,3);
            }
            g.surface=Surface.Auto;
        }

        static Geometry FieldSoldier(string unit,string hero=null)
        {
            var g=new Geometry();
            bool armored=unit=="shield"||unit=="spear"||unit=="cavalry"||unit=="giant";
            var wool=hero=="hero_ranger"?Moss:hero=="hero_commander"?UndyedLinen:DyedWool;
            TailoredTunic(g,wool,armored);
            for(int side=-1;side<=1;side+=2)
            {
                g.surface=armored?Surface.Metal:Surface.Cloth;
                var shoulder=V(side*.229f,.243f,-.002f);
                g.Ellipsoid(shoulder,V(.113f,.103f,.143f),armored?WroughtIron:wool,10,4);
                if(armored)
                {
                    g.Frustum(shoulder+V(0,-.052f,0),V(.230f,0,.291f),shoulder+V(0,.013f,0),
                        V(.230f,0,.273f),LightSteel,10);
                    g.Ellipsoid(shoulder+V(side*.082f,-.021f,.081f),V(.019f,.020f,.023f),OldGold,6,3);
                }
                g.surface=Surface.Cloth;
                g.Ellipsoid(V(side*.114f,-.351f,-.01f),V(.125f,.165f,.148f),Pigment(wool,.85f),8,4);
            }
            g.surface=Surface.Auto;
            Portrait(g,(hero!=null?11:0)+System.Array.IndexOf(Types,unit),armored);
            if(unit=="shield"||unit=="cavalry"||unit=="giant")
                Bascinet(g,WroughtIron,hero!=null?OldGold:Steel);
            else if(unit=="archer"||unit=="medic")
                WoolHood(g,hero=="hero_commander"?Indigo:unit=="archer"?Moss:UndyedLinen,true);
            else
                KettleHat(g,unit=="militia"?Walnut:WroughtIron);

            if(armored)
            {
                g.surface=Surface.Metal;
                g.Loft(new[]{V(0,-.157f,.012f),V(0,-.058f,.032f),V(0,.091f,.054f),V(0,.201f,.034f),V(0,.293f,.009f)},
                    new[]{Section(.186f,.143f),Section(.207f,.165f),Section(.244f,.181f),Section(.247f,.170f),Section(.145f,.115f)},
                    14,2,WroughtIron);
                for(int i=0;i<3;i++)
                {
                    float y=-.137f-i*.044f,w=.396f+i*.014f;
                    g.Frustum(V(0,y-.022f,.026f),V(w,0,.33f),V(0,y+.016f,.026f),V(w-.019f,0,.302f),
                        Pigment(WroughtIron,1.03f-i*.069f),12);
                }
                g.surface=Surface.Auto;
                // Linen falls over the iron breast and fauld rather than forming
                // thick boxes at the shoulders. Its edges follow the waist and hip.
                g.Frustum(V(0,-.145f,.014f),V(.38f,0,.31f),V(0,.218f,.016f),V(.502f,0,.345f),WroughtIron,8);
                for(int side=-1;side<=1;side+=2)
                {
                    g.Quad(V(side*.071f,.289f,.126f),V(side*.22f,.226f,.118f),V(side*.166f,-.19f,.167f),
                        V(side*.041f,-.37f,.179f),wool,true);
                    g.Quad(V(side*.187f,.245f,-.02f),V(side*.270f,.182f,.054f),V(side*.274f,.11f,.088f),
                        V(side*.211f,.154f,.15f),LightSteel,true);
                }
                HeraldicKite(g,0,.083f,.186f,.064f,.107f,hero!=null?OldGold:UndyedLinen);
                if(unit!="spear"&&hero==null)ShoulderMantle(g,wool,-.53f);
            }
            else if(unit=="archer"||unit=="crossbow")
            {
                g.Cylinder(V(-.158f,-.21f,-.198f),V(-.082f,.322f,-.18f),.074f,Walnut,7);
                for(int i=0;i<3;i++)
                {
                    float x=-.131f+i*.038f;
                    g.Cylinder(V(x,.281f,-.199f),V(x+.023f,.475f,-.232f),.008f,WoodLight,4);
                    g.Triangle(V(x,.377f,-.207f),V(x+.021f,.469f,-.235f),V(x-.026f,.454f,-.269f),UndyedLinen);
                }
            }
            else if(unit=="bomber"||unit=="medic")
                g.Bevel(V(-.201f,-.19f,-.106f),V(.211f,.266f,.186f),.04f,unit=="medic"?UndyedLinen:Madder);

            g.Frustum(V(0,-.176f,.014f),V(.377f,0,.306f),V(0,-.108f,.011f),V(.389f,0,.312f),Walnut,8);
            g.Box(V(0,-.143f,.179f),V(.076f,.065f,.021f),OldGold);
            g.Bevel(V(.172f,-.222f,.077f),V(.108f,.144f,.129f),.025f,WoodLight);
            if(!armored)g.Quad(V(-.172f,.24f,.141f),V(-.138f,.263f,.145f),V(.17f,-.17f,.164f),
                V(.133f,-.174f,.171f),Walnut,true);

            if(hero=="hero_vanguard")
            {
                ShoulderMantle(g,Madder,-.63f);
                g.Spear(V(-.073f,.748f,-.042f),V(-.257f,.972f,-.122f),.063f,OldGold);
            }
            else if(hero=="hero_ranger")
            {
                ShoulderMantle(g,Moss,-.58f);
                g.Triangle(V(.112f,.652f,-.066f),V(.356f,.924f,-.209f),V(.176f,.687f,-.223f),UndyedLinen);
            }
            else if(hero=="hero_commander")
            {
                ShoulderMantle(g,Indigo,-.61f);
                HeraldicKite(g,0,.062f,.19f,.085f,.134f,OldGold);
                Pennant(g,V(-.171f,-.28f,-.192f),1.58f,.51f,Indigo,0);
            }
            return g;
        }

        static Geometry CurvedShield(float x,float z,Color rim,Color field)
        {
            var g=new Geometry();
            var edge=new[] {V(-.212f,.31f,0),V(-.235f,.155f,0),V(-.164f,-.197f,0),
                V(0,-.473f,0),V(.164f,-.197f,0),V(.235f,.155f,0),V(.212f,.31f,0),V(0,.338f,.021f)};
            var center=V(x,.025f,z+.088f);
            var origin=V(x,0,z);
            for(int i=0;i<edge.Length;i++)
            {
                var a=edge[i];var b=edge[(i+1)%edge.Length];
                var c=a*.855f+Vector3.forward*.019f;var d=b*.855f+Vector3.forward*.019f;
                g.Quad(origin+a,origin+b,origin+d,origin+c,Pigment(rim,i%2==0?1:.79f),true);
                g.Gradient(origin+c,origin+d,center,Pigment(field,.81f),Pigment(field,.91f),field);
            }
            g.Ellipsoid(center+Vector3.forward*.025f,V(.066f,.079f,.044f),rim,7,2);
            HeraldicKite(g,x,-.158f,z+.069f,.065f,.103f,UndyedLinen);
            g.Box(V(x,-.06f,z-.041f),V(.237f,.085f,.086f),Walnut);
            return g;
        }
    }
}
