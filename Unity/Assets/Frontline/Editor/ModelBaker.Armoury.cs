using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void BowStave(Geometry g,bool steel,int tier=0)
        {
            const float x=.36f;
            g.surface=steel?Surface.Metal:Surface.Oak;
            float span=steel?.39f+.028f*tier:.59f+.045f*tier;
            var center=steel?V(x,.096f,.60f):V(x,.10f,.51f);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 Limb(float s,float bend)=>center+(steel?Vector3.right:Vector3.up)*(side*span*s)+Vector3.forward*bend;
                var tip=Limb(1,steel?-.125f:-.182f);
                g.Loft(new[]{center,Limb(.32f,-.015f),Limb(.66f,-.052f),Limb(.86f,-.131f),tip},
                    new[]{Section(.032f,.039f),Section(.027f,.034f),Section(.022f,.029f),Section(.016f,.020f),Section(.009f,.012f)},
                    7,2,steel?WroughtIron:WoodLight);
                var rest=steel?V(x,.102f,.22f):V(x,.102f,tip.z);
                g.Cylinder(tip,rest,.006f,Linen,4);
                if(tier>0)g.Cylinder(Limb(.77f,-.081f),Limb(.84f,-.115f),.023f,OldGold,6);
            }
            if(steel)
            {
                g.surface=Surface.Oak;
                g.Loft(new[]{V(x,-.012f,-.17f),V(x,.037f,.078f),V(x,.070f,.35f),V(x,.083f,.76f)},
                    new[]{Section(.064f,.084f),Section(.068f,.073f),Section(.054f,.052f),Section(.039f,.038f)},8,2,Walnut);
                g.surface=Surface.Metal;g.Box(V(x,.131f,.39f),V(.037f,.031f,.64f),LightSteel);
                g.Cylinder(V(x-.085f,.039f,.22f),V(x+.085f,.039f,.22f),.041f,WroughtIron,8);
            }
            else
            {
                g.surface=Surface.Hide;
                g.Cylinder(center-Vector3.up*.065f,center+Vector3.up*.075f,.038f,Walnut,8);
                g.Cylinder(V(x,.109f,.20f),V(x,.109f,.86f),.009f,WoodLight,5);
                Fletching(g,V(x,.109f,.24f),Vector3.back,.109f);
            }
            g.surface=Surface.Auto;
        }

        static void Fletching(Geometry g,Vector3 tip,Vector3 shaft,float length)
        {
            var transverse=Vector3.Cross(shaft,Mathf.Abs(shaft.y)>.85f?Vector3.right:Vector3.up).normalized;
            for(int i=0;i<3;i++)
            {
                var vane=Quaternion.AngleAxis(i*120,shaft)*transverse;
                g.Quad(tip,tip+shaft*length,tip+shaft*length+vane*.024f,
                    tip+shaft*(length*.38f)+vane*.021f,UndyedLinen,true);
            }
        }

        static void FieldImplement(Geometry g,string unit,int rank=0)
        {
            const float x=.36f;
            if(unit=="archer"||unit=="crossbow"){BowStave(g,unit=="crossbow",rank);return;}
            if(unit=="giant")
            {
                g.surface=Surface.Oak;g.Cylinder(V(x,-.44f,.36f),V(x,.69f,.36f),.046f,Walnut,10);
                g.surface=Surface.Metal;
                var h=V(x,.62f,.36f);float reach=.29f+.017f*rank;
                g.Loft(new[]{h-Vector3.right*reach,h-Vector3.right*.21f,h,h+Vector3.right*.20f,h+Vector3.right*reach},
                    new[]{Section(.160f,.155f),Section(.136f,.139f),Section(.139f,.135f),Section(.151f,.146f),Section(.17f,.161f)},
                    8,1,WroughtIron);
                foreach(int side in new[]{-1,1})
                {
                    var end=h+Vector3.right*(side*reach);
                    g.Bevel(end,V(.065f,.319f,.303f),.043f,LightSteel);
                    g.Box(h+V(side*.16f,0,.134f),V(.054f,.241f,.023f),rank>=2?OldGold:Steel);
                    for(int rivet=-1;rivet<=1;rivet+=2)
                        g.Ellipsoid(h+V(side*.16f,rivet*.083f,.152f),Vector3.one*.018f,OldGold,7,3);
                }
                if(rank==3)g.Spear(h+V(0,.108f,0),h+V(0,.319f,0),.091f,OldGold);
            }
            else if(unit=="bomber")
            {
                var jar=V(x,.014f,.33f);
                g.surface=Surface.Metal;
                g.Loft(new[]{jar+V(0,-.145f,0),jar+V(0,-.094f,0),jar,jar+V(0,.11f,0),jar+V(0,.16f,0)},
                    new[]{Section(.084f,.081f),Section(.14f,.135f),Section(.153f,.154f),Section(.093f,.096f),Section(.043f,.045f)},
                    11,2,WroughtIron);
                g.Torus(jar+Vector3.up*.007f,.153f,.013f,Copper,12);
                var r=Section(.012f,.013f);
                g.surface=Surface.Cloth;g.Loft(new[]{jar+V(0,.15f,0),jar+V(.006f,.216f,0),jar+V(.050f,.24f,.018f),jar+V(.068f,.235f,.023f)},
                    new[]{r,r,r,r},5,2,UndyedLinen);
                g.Ellipsoid(jar+V(.069f,.235f,.023f),Vector3.one*.021f,Amber,7,3);
            }
            else
            {
                var shaft=V(x,0,.28f);
                g.surface=Surface.Oak;
                g.Loft(new[]{shaft+Vector3.down*.45f,shaft,shaft+V(.008f,.40f,0),shaft+V(0,.68f,0)},
                    new[]{Section(.031f,.033f),Section(.034f,.036f),Section(.030f,.029f),Section(.046f,.043f)},8,2,Walnut);
                g.surface=Surface.Metal;
                g.Box(shaft+Vector3.up*.70f,V(.065f,.29f,.070f),rank>0?OldGold:Copper);
                g.Box(shaft+Vector3.up*.738f,V(.238f,.057f,.070f),rank>0?OldGold:Copper);
                if(rank>=2)g.Ellipsoid(shaft+V(0,.815f,0),V(.077f,.099f,.073f),Glass,10,4);
                if(rank==3)
                {
                    g.Cylinder(shaft+V(-.244f,.67f,0),shaft+V(.245f,.67f,0),.018f,OldGold,7);
                    for(int j=0;j<7;j++)
                    {
                        float a=-.212f+j*.061f,b=a+.061f;
                        Vector3 P(float u,float t)=>shaft+V(u,.653f-t*(.32f-.28f*Mathf.Abs(u)),.033f+t*.041f*Mathf.Cos(17*u));
                        g.surface=Surface.Cloth;
                        g.Quad(P(a,0),P(b,0),P(b,1),P(a,1),j==0||j==6||j==3?OldGold:Indigo,true);
                    }
                }
            }
            g.surface=Surface.Auto;
        }

        static Geometry ForgedArm(string hero,int rank)
        {
            bool iron=hero=="hero_vanguard",ranger=hero=="hero_ranger";
            string tool=iron?"giant":ranger?(rank>0&&rank<3?"archer":"crossbow"):"medic";
            var g=new Geometry();
            ArticulatedSleeve(g,tool,false);FieldImplement(g,tool,rank);
            g.surface=Surface.Metal;
            var shoulder=V(.282f,.288f,.010f);
            g.Frustum(shoulder-V(0,.021f,0),V(.225f,0,.234f),shoulder+V(0,.017f,0),V(.185f,0,.205f),
                rank==3?OldGold:WroughtIron,12);
            return g;
        }

        static Geometry AchievementBadge(string hero,int rank)
        {
            var g=ArmamentCrest(hero,0);
            bool iron=hero=="hero_vanguard",ranger=hero=="hero_ranger";
            g.surface=Surface.Metal;
            for(int side=-1;side<=1;side+=2)
            {
                if(iron)
                {
                    g.Loft(new[]{V(side*.076f,.06f,.215f),V(side*.124f,.15f,.220f),V(side*.21f,.223f,.138f)},
                        new[]{Section(.026f,.015f),Section(.040f,.018f),Section(.018f,.012f)},6,2,OldGold);
                    if(rank==3)
                    {
                        var p=V(side*.24f,.258f,0);
                        g.Ellipsoid(p,V(.126f,.109f,.153f),OldGold,10,4);
                        g.Spear(p+V(0,.066f,.016f),p+V(side*.060f,.159f,-.041f),.048f,WroughtIron);
                    }
                }
                else if(ranger)
                {
                    var q=V(-.101f+side*.036f,.25f,-.228f);
                    g.Cylinder(q,q+V(.044f,.32f,-.053f),.012f,WoodLight,5);
                    Fletching(g,q+V(.034f,.24f,-.042f),Vector3.up,.104f);
                    if(rank==3)g.Spear(V(side*.106f,.665f,-.042f),V(side*.189f,.806f,-.090f),.043f,OldGold);
                }
                else
                {
                    g.Cylinder(V(side*.15f,.286f,.146f),V(side*.064f,.118f,.205f),.015f,OldGold,7);
                    if(rank==3)g.Spear(V(side*.111f,.670f,-.018f),V(side*.126f,.785f,-.022f),.031f,OldGold);
                }
            }
            if(!iron&&!ranger)g.Bevel(V(0,.070f,.215f),V(.115f,.147f,.058f),.020f,rank==3?OldGold:LightSteel);
            return g;
        }

        static void CampaignDress(Geometry g,string unit,string hero)
        {
            if(unit=="archer"||unit=="crossbow")
            {
                g.surface=Surface.Hide;
                g.Loft(new[]{V(-.154f,-.26f,-.208f),V(-.127f,.06f,-.209f),V(-.066f,.31f,-.19f)},
                    new[]{Section(.058f,.065f),Section(.069f,.073f),Section(.086f,.083f)},9,2,Walnut);
                for(int i=0;i<4;i++)Fletching(g,V(-.123f+i*.031f,.371f+(i%2)*.033f,-.227f),Vector3.up,.10f);
                g.surface=Surface.Auto;
            }
            if(unit=="bomber"||unit=="medic")
            {
                for(int j=0;j<3;j++)
                {
                    var phial=V(-.203f+j*.048f,-.135f,.121f);
                    g.surface=unit=="medic"?Surface.Glass:Surface.Metal;
                    g.Ellipsoid(phial,V(.028f,.063f,.029f),unit=="medic"?Glass:WroughtIron,7,3);
                    g.Cylinder(phial+Vector3.up*.054f,phial+Vector3.up*.079f,.018f,Walnut,6);
                }
                g.surface=Surface.Auto;
            }
            if(unit=="militia"||unit=="giant")
            {
                g.surface=unit=="giant"?Surface.Metal:Surface.Hide;
                for(int i=0;i<3;i++)
                    g.Bevel(V(.115f-i*.109f,-.197f,.146f),V(.101f,.113f,.042f),.019f,unit=="giant"?Steel:Walnut);
                g.surface=Surface.Auto;
            }
            if(hero!=null)
            {
                var dye=hero=="hero_ranger"?Moss:hero=="hero_vanguard"?Madder:Indigo;
                for(int i=0;i<5;i++)
                {
                    var foot=V(-.074f+i*.030f,.665f,-.053f);
                    var tip=foot+V(-.071f-.016f*i,.175f+.021f*i,-.092f);
                    g.Quad(foot,Vector3.Lerp(foot,tip,.58f)-V(.035f,0,0),tip,
                        Vector3.Lerp(foot,tip,.56f)+V(.030f,0,0),i%2==0?dye:OldGold,true);
                }
            }
        }
    }
}
