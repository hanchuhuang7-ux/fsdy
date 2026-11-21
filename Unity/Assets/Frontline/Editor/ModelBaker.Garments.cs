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
            const int sides=8;
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
            g.Frustum(V(0,.504f,-.014f),V(.253f,0,.253f),V(0,.65f,-.018f),V(.289f,0,.284f),metal,8);
            g.Frustum(V(0,.65f,-.018f),V(.29f,0,.283f),V(0,.793f,-.041f),V(.074f,0,.088f),LightSteel,8);
            g.Frustum(V(0,.372f,-.025f),V(.278f,0,.211f),V(0,.514f,-.021f),V(.249f,0,.203f),fringe,7);
            for(int side=-1;side<=1;side+=2)
            {
                void Shell(Vector3 a,Vector3 b,Vector3 c,Color color)
                {
                    if(side<0)g.Triangle(a,c,b,color);else g.Triangle(a,b,c,color);
                }
                var pin=V(side*.117f,.605f,.059f);
                Shell(pin,V(0,.632f,.164f),V(0,.546f,.201f),LightSteel);
                Shell(pin,V(0,.546f,.201f),V(side*.095f,.504f,.101f),metal);
                Shell(V(0,.598f,.183f),V(side*.104f,.603f,.084f),V(side*.045f,.584f,.158f),Dark);
                Shell(V(side*.080f,.550f,.136f),V(side*.064f,.534f,.153f),V(side*.083f,.534f,.129f),Dark);
            }
        }

        static void ShoulderMantle(Geometry g,Color dye,float length)
        {
            const int folds=5;
            for(int i=0;i<folds;i++)
            {
                float a=i/(float)folds-.5f,b=(i+1)/(float)folds-.5f;
                var left=V(a*.412f,.31f,-.10f-Mathf.Cos(a*2.9f)*.059f);
                var right=V(b*.412f,.31f,-.10f-Mathf.Cos(b*2.9f)*.059f);
                var hemA=V(a*.587f,length+.067f*Mathf.Abs(a),-.285f-i%2*.044f);
                var hemB=V(b*.587f,length+.067f*Mathf.Abs(b),-.285f-(i+1)%2*.044f);
                g.Quad(right,left,hemA,hemB,Pigment(dye,i%2==0?.83f:1.09f),true);
            }
        }

        static Geometry FieldSoldier(string unit,string hero=null)
        {
            var g=new Geometry();
            bool armored=unit=="shield"||unit=="spear"||unit=="cavalry"||unit=="giant";
            var wool=hero=="hero_ranger"?Moss:hero=="hero_commander"?UndyedLinen:DyedWool;
            TailoredTunic(g,wool,armored);
            Portrait(g,(hero!=null?11:0)+System.Array.IndexOf(Types,unit),armored);
            if(unit=="shield"||unit=="cavalry"||unit=="giant")
                Bascinet(g,WroughtIron,hero!=null?OldGold:Steel);
            else if(unit=="archer"||unit=="medic")
                WoolHood(g,hero=="hero_commander"?Indigo:unit=="archer"?Moss:UndyedLinen,true);
            else
                KettleHat(g,unit=="militia"?Walnut:WroughtIron);

            if(armored)
            {
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
