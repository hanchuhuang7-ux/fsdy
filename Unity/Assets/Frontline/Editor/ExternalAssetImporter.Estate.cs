using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ExternalAssetImporter
    {
        internal static readonly List<string> Exported=new List<string>();
        static Part P(string name,float w,float h,float d,float x=0,float y=0,float z=0,float yaw=0) =>
            new Part(name,w,h,d,x,y,z,yaw);
        static Part Exact(string name,float w,float h,float d,float x=0,float y=0,float z=0,float yaw=0) =>
            new Part(name,w,h,d,x,y,z,yaw,exact:true);

        static void WritePartPair(string key,IEnumerable<Part> entries,bool plot=false)
        {
            var plan=entries.ToArray();
            for(int level=0;level<2;level++)
            {
                bool low=level==1;
                string name=key+(low?"_lod1":"");
                if(low&&(key=="rock"||key=="prop_barrel"))
                {
                    if(!AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/"+name+".asset"))throw new Exception("Missing physics proxy "+name);
                    Exported.Add("Models/"+name);continue;
                }
                var result=new ArtData();
                foreach(var part in plan)
                {
                    if(low&&part.detailOnly)continue;
                    var high=ReadPart(part.name,false,part.filter);var src=low?ReadPart(part.name,true,part.filter):high;
                    result.Add(src,Fit(high.Bounds(),part),project:true);
                }
                if(plot)
                {
                    bool hall=key.StartsWith("manor_hall",StringComparison.Ordinal);
                    var b=result.Bounds();
                    float fit=Mathf.Min(1,Mathf.Min(ManorPlotArt.HalfWidth(hall)/(b.extents.x+Mathf.Abs(b.center.x)),
                        ManorPlotArt.HalfDepth(hall)/(b.extents.z+Mathf.Abs(b.center.z))));
                    for(int i=0;i<result.p.Count;i++)
                    {
                        var p=result.p[i];result.p[i]=new Vector3(p.x*fit,p.y-b.min.y,p.z*fit);
                    }
                }
                if(key=="market_cart"||key=="verge_wall"||key=="verge_flowers")
                {
                    var bb=result.Bounds();float bottom=bb.min.y;
                    for(int i=0;i<result.p.Count;i++)result.p[i]-=Vector3.up*bottom;
                }
                if(key.StartsWith("backyard_",StringComparison.Ordinal))
                {
                    var box=result.Bounds();float k=Mathf.Min(1,Mathf.Min(ManorPlotArt.BackyardHeight/box.size.y,Mathf.Min(ManorPlotArt.BackyardHalfWidth/(box.extents.x+Mathf.Abs(box.center.x)),ManorPlotArt.BackyardHalfDepth/(ManorPlotArt.BackyardDepthScale*(box.extents.z+Mathf.Abs(box.center.z))))));
                    for(int i=0;i<result.p.Count;i++){var p=result.p[i];result.p[i]=new Vector3(p.x,p.y-box.min.y,p.z)*k;}
                }
                if(key=="bridge_fittings")
                {
                    for(int i=0;i<result.p.Count;i++)
                    {
                        var p=result.p[i];
                        if(Mathf.Abs(p.z)<FrontierMap.BridgeHalfWidth-.13f&&p.y>-.09f&&p.y<.11f)
                        {p.y=0;result.p[i]=p;}
                    }
                    CheckBridgeDeck(result,low);
                }
                if(key=="rock"||key=="prop_barrel")ConformToHull(result,AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/"+key+"_lod1.asset"));
                var mesh=result.Mesh(name);
                if(key=="rock")mesh.bounds=AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/rock_lod1.asset").bounds;
                WriteMesh(name,mesh);Exported.Add("Models/"+name);
            }
        }

        static IEnumerable<Part> Estate(string kind,int level,int crop=0)
        {
            if(kind=="manor_hall")
            {
                float keepWidth=level==1?4.25f:level==2?5.3f:6.6f;
                yield return Exact("building_castle_red",keepWidth,level==1?5.2f:level==2?7.25f:9.20f,keepWidth*.81f,-.44f,0,.40f,180);
                yield return Exact("Path_Square",level==1?4.95f:8.45f,.21f,level==1?4.52f:6.85f,0,-.14f,0);
                if(level>=2)
                {
                    yield return P("LargeTower",2.3f,6.6f,2.25f,3.07f,0,2.22f);
                    yield return Exact("TallWallBricks",6.99f,2.25f,.48f,0,0,3.16f);
                    yield return Exact("TallWallEntrance",3.15f,2.63f,.59f,0,0,-3.12f,180);
                    for(int i=-1;i<=1;i+=2)
                    {
                        yield return Exact("TallWallBricks",.58f,2.2f,5.9f,i*3.51f,0,0,90);
                        yield return Exact("TallWallBricks",2.00f,2.25f,.51f,i*2.57f,0,-3.13f);
                        yield return Exact(i<0?"LargeSquareTowerBricks":"WatchTowerWRoof",1.5f,level==3?4.91f:3.75f,1.56f,i*3.5f,0,-2.85f);
                    }
                }
                if(level==3)
                {
                    yield return Exact("LargeSquareTowerBricks",1.68f,5.6f,1.73f,-3.5f,0,2.79f);
                    yield return Exact("Banner",.64f,1.42f,.28f,-3.48f,3.49f,-2.89f,-17);
                    yield return Exact("Banner",.54f,1.24f,.27f,3.44f,3.68f,-2.86f,13);
                    yield return P("House_3",2.1f,3.65f,2.1f,2.52f,0,-.22f,180);
                }
                yield return P("wooden_lantern_01",.27f,.57f,.25f,-.98f,1.16f,-3.52f);
                yield return P("wooden_lantern_01",.27f,.57f,.25f,.98f,1.16f,-3.52f);
                yield break;
            }
            float size=.77f+.105f*level;
            switch(kind)
            {
                case "cottage":
                    yield return Exact(level==2?"House_2":"House_1",2.23f*size,2.10f*size,2.09f*size,-.56f,0,.21f,180);
                    if(level>=2)yield return P("Bags",.66f,.4f,.42f,.73f,0,.74f);
                    if(level==3)yield return P("Fence",1.22f,.47f,.22f,.88f,0,1.12f);
                    break;
                case "farm": case "irrigated_field":
                    yield return Exact("building_dirt",2.31f,.065f,2.42f,.57f,0,0);
                    if(crop!=1)
                    {
                        var wheat=Exact("Farm_FirstAge_Level"+level+"_Wheat",2.13f,crop==2?.67f:.48f,2.23f,.54f,.052f,0);
                        yield return wheat;
                    }
                    if(kind=="farm")yield return MillBuilding(ManorPlotArt.MillHub);
                    else
                    {
                        yield return P("House_1",.93f,1.04f,1.20f,-1.23f,0,.56f,180);
                        yield return Exact("building_bridge_A",.40f,.24f,2.21f,-.85f,0,0,90);
                    }
                    if(level>1)yield return P("Hay",.63f,.54f,.62f,-1.22f,0,-.86f);
                    if(level==3)yield return P("Fence",1.12f,.4f,.19f,.83f,0,1.24f);
                    break;
                case "bakery":
                    yield return MillBuilding(ManorPlotArt.BakeryHub);
                    yield return P("Blacksmith",1.81f,2.18f*size,2.23f,-.78f,0,.13f,180);
                    yield return P("Bonfire_Lit",.35f,.28f,.27f,-.85f,.11f,-.85f);
                    if(level>=2)yield return P("Bags",.69f,.50f,.48f,-1.18f,0,-1.03f);
                    if(level==3)yield return P("MarketStand_2",1.18f,1.03f,.63f,-.28f,0,-.98f,180);
                    break;
                case "timber_camp":
                    yield return Exact("Sawmill",2.1f*size,1.9f*size,2.11f,-.66f,0,.06f,180);
                    yield return P("Mine",1.23f,1.48f,1.31f,1.10f,0,.43f);
                    yield return P("Logs",1.23f,.59f,.63f,.92f,0,-.72f);
                    break;
                case "weaver":
                    yield return Exact("House_2",2.27f*size,2.18f*size,2.07f,-.45f,0,.21f,180);
                    yield return P("MarketStand_2",1.19f,1.39f,1.45f,.97f,0,.07f,180);
                    yield return P("Cauldron",.53f,.54f,.56f,.99f,0,-.98f);
                    break;
                case "pasture": case "stables":
                    yield return Exact("Stable",2.8f*size,2.06f*size,2.0f*size,0,0,.14f,180);
                    yield return P("Hay",.69f,.56f,.63f,1.25f,0,-.79f);
                    yield return P("Fence",1.38f,.56f,.16f,-.74f,0,-1.17f);
                    break;
                case "smithy":
                    yield return Exact("Blacksmith",3.04f*size,2.63f*size,2.38f*size,0,0,.13f,180);
                    yield return P("Bonfire_Lit",.56f,.50f,.46f,.85f,0,-1.04f);
                    break;
                case "tavern":yield return Exact("Inn",3.11f*size,2.69f*size,2.34f*size,0,0,.13f,180);break;
                case "chapel":
                    yield return Exact("building_church_red",2.58f*size,3.47f*size,2.42f*size,-.15f,0,.03f,180);
                    if(level>1)yield return Exact("Bell_Tower",.81f,2.8f+.35f*level,.85f,1.31f,0,.71f);
                    break;
                case "barracks":case "drill_yard":case "granary":
                    string trade=kind=="barracks"?"Barracks":kind=="drill_yard"?"Archery":"Storage";
                    yield return Exact(trade+"_SecondAge_Level"+level,3.18f*size,2.07f*size,2.41f*size,0,0,.08f,180);
                    break;
                default:throw new ArgumentException(kind);
            }
            foreach(var prop in Courtyard(kind,level)){prop.detailOnly=true;yield return prop;}
            if(kind=="farm"||kind=="irrigated_field"||kind=="cottage"||kind=="bakery")yield break;
            if(level>1)yield return P(kind=="tavern"?"wine_barrel_01":"wooden_crate_01",.52f,.57f,.45f,-1.40f,0,-.97f);
            if(level==3)yield return P(kind=="barracks"||kind=="drill_yard"?"weaponrack":kind=="tavern"?"Bench_1":"wheelbarrow",.82f,.58f,.50f,.33f,0,-1.10f,180);
        }
        static IEnumerable<Part> Courtyard(string kind,int level)
        {
            bool masonry=kind=="chapel"||kind=="smithy"||kind=="tavern";
            yield return Exact(masonry?"fence_stone_straight":"Fence",.82f,.32f,.16f,kind=="cottage"?-1.24f:1.26f,0,-1.24f);
            yield return Exact("Path_Square",.64f,.036f,.36f,-.08f,.004f,-1.15f);
            string cargo=kind switch {"smithy" or "timber_camp"=>"Logs","barracks" or "drill_yard"=>"weaponrack",
                "chapel"=>"Flower_4","tavern"=>"wine_barrel_01","farm" or "pasture" or "irrigated_field"=>"Hay",
                "weaver" or "cottage"=>"bucket_water","bakery"=>"Bags",_=>"wooden_crate_01"};
            yield return P(cargo,.53f,.49f,.41f,1.43f,0,kind=="cottage"?.35f:-.83f,17);
            if(level>=2)yield return P("Flower_4",.42f,.41f,.36f,1.46f,0,1.06f,-19);
            if(level>=3)yield return Exact("Fence",.66f,.33f,.14f,1.64f,0,.32f,90);
        }
        static Part MillBuilding(Vector3 hub)
        {
            var b=ReadPart("Mill",false,"Mill").Bounds();
            var blades=ReadPart("Mill",false,"Mill_Blades").Bounds();
            float s=hub.y/(blades.center.y-b.min.y);
            return new Part("Mill",b.size.x*s,b.size.y*s,b.size.z*s,
                hub.x+(blades.center.x-b.center.x)*s,0,hub.z+(blades.center.z-b.center.z)*s,180,"Mill",true);
        }
        static IEnumerable<Part> Bridge()
        {
            float length=FrontierMap.Boundary-FrontierMap.BridgeStart;
            // Artist-authored timber spans meet on piers; the path stays at the surveyed deck plane.
            const int spans=12;float segment=length/spans;
            for(int i=0;i<spans;i++)
            {
                float x=-length*.5f+(i+.5f)*segment;
                yield return Exact(i%3==0?"bridge-straight-pillar":"bridge-straight",segment+.025f,2.7f,9.30f,x,-2.389f,0);
                for(int side=-1;side<=1;side+=2)
                {
                    float z=side*(FrontierMap.BridgeHalfWidth+.26f);
                    yield return Exact("Fence",segment,.85f,.21f,x,0,z);
                    if(i%3!=0)continue;
                    yield return Exact("Column",.72f,2.37f,.66f,x,-1.63f,z);
                    yield return Exact("Arch",segment-.06f,1.16f,.37f,x,-1.42f,z,0);
                    if(i==0||i==9)yield return P("Bonfire_Lit",.52f,.47f,.51f,x,.72f,z);
                }
            }
        }
        static void CheckBridgeDeck(ArtData mesh,bool low)
        {
            float half=(FrontierMap.Boundary-FrontierMap.BridgeStart)*.5f,maxError=0;
            int probes=0;
            for(float x=-half+.08f;x<half;x+=.41f)for(int lane=-1;lane<=1;lane++)
            {
                var p=new Vector2(x,lane*2.70f);float top=-999;
                for(int i=0;i<mesh.idx.Count;i+=3)
                {
                    var a=mesh.p[mesh.idx[i]];var b=mesh.p[mesh.idx[i+1]];var c=mesh.p[mesh.idx[i+2]];
                    float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(Mathf.Abs(den)<1e-7f)continue;
                    float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.y-c.z))/den;
                    float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.y-c.z))/den;
                    if(u>=-.00001f&&v>=-.00001f&&u+v<=1.00001f)top=Mathf.Max(top,u*a.y+v*b.y+(1-u-v)*c.y);
                }
                maxError=Mathf.Max(maxError,Mathf.Abs(top));probes++;
            }
            if(maxError>.065f)throw new InvalidOperationException("Imported bridge deck misses the physics plane: "+maxError+" low="+low);
            Debug.Log("CC0_BRIDGE_DECK_OK probes="+probes+" low="+low+" maxMetres="+maxError);
        }
        internal static void EstateModels()
        {
            Exported.Clear();parts.Clear();UsedSources.Clear();
            var kinds=new List<string>{"manor_hall"};kinds.AddRange(Manor.Buildings);
            foreach(var kind in kinds)for(int level=1;level<=3;level++)
            {
                string suffix=level==1?"":level==2?"_wing":"_tower";
                WritePartPair(kind+suffix,Estate(kind,level),true);
                if(kind=="farm"||kind=="irrigated_field")
                    for(int crop=1;crop<=2;crop++)WritePartPair(kind+(crop==1?"_fallow":"_rich")+suffix,Estate(kind,level,crop),true);
            }
            void One(string key,string src,float w,float h,float d) =>
                WritePartPair(key,new[]{Exact(src,w,h,d)});
            One("tree","island_tree_01",3.05f,3.87f,3.07f);One("pine","pine_sapling_small",1.76f,3.98f,1.88f);
            WritePartPair("shore_rock",new[]{new Part("rock_07",.98f,.79f,.97f,exact:true,center:true)});
            One("shrub","shrub_01",.98f,.77f,.91f);One("grass","grass_bermuda_01",.67f,.23f,.61f);
            One("flowers","Flower_4",.61f,.44f,.53f);One("verge_flowers","Flower_4",.80f,.51f,.61f);
            One("reeds","grass_bermuda_01",.55f,.68f,.42f);
            WritePartPair("dead_tree",new[]{new Part("island_tree_01",2.1f,3.23f,1.96f,filter:"island_tree_01")});
            var hull=AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/rock_lod1.asset").bounds;
            WritePartPair("rock",new[]{new Part("rock_07",hull.size.x,hull.size.y,hull.size.z,hull.center.x,hull.center.y,hull.center.z,exact:true,center:true)});
            One("prop_barrel","wine_barrel_01",.82f,1.09f,.82f);
            var box=ContactShape.BarricadeSize;
            One("prop_barricade","fence_wood_straight",box.x,box.y,box.z);
            One("prop_banner","Banner",.89f,ContactShape.BannerHeight,.32f);
            One("prop_spikes","Trap_spikes",1.67f,.66f,1.67f);One("prop_spring","Bouncer",1.62f,.71f,1.62f);
            WritePartPair("wall",new[]{new Part("TallWallBricks",1,1,1,exact:true,center:true)});
            One("frontier_palisade","fence_wood_straight",1,1,1);
            WritePartPair("bridge_fittings",Bridge());
            WritePartPair("mill_sails",new[]{new Part("Mill",1.49f,1.49f,.16f,filter:"Mill_Blades",exact:true,center:true)});
            One("waypost","Sign_LeftRight",.51f,1.13f,.17f);One("plot_marker","Sign_LeftRight",.39f,.46f,.08f);
            WritePartPair("manor_pillar",new[]{Exact("Column",.43f,.91f,.43f),P("wooden_lantern_01",.29f,.41f,.29f,0,.91f,0)});
            One("manor_plaza","Path_Square",5.68f,.061f,4.34f);One("manor_bench","Bench_1",1.39f,.53f,.42f);
            One("column","Column",.98f,2.23f,.98f);One("arch","Arch",2.28f,2.18f,.69f);
            One("manor_well","Well",1.68f,1.56f,1.34f);One("manor_market","MarketStand_1",1.96f,1.85f,1.33f);
            WritePartPair("manor_dock",new[]{Exact("Port_FirstAge_Level1",4.25f,1.03f,2.65f,0,-.53f,0,180),P("Boat",2.3f,.58f,1.15f,1.48f,-1.06f,-2.30f)});
            One("manor_fence","Fence",1.48f,.55f,.22f);One("verge_wall","fence_stone_straight",1.5f,.53f,.41f);
            One("manor_firewood","Logs",.93f,.59f,.75f);One("manor_straw","Hay",1.12f,.81f,.9f);
            WritePartPair("market_cart",new[]{Exact("Cart",.80f,.93f,1.66f,yaw:90)});One("brazier","wooden_lantern_01",.63f,1.15f,.63f);
            One("manor_laundry","MarketStand_2",1.53f,1.1f,.72f);
            One("backyard_garden","Carrot_4",.76f,.40f,.46f);One("backyard_poultry","Gazebo",.76f,.68f,.46f);
            One("backyard_workshop","Sawmill",.76f,.72f,.46f);One("backyard_militia_yard","weaponrack",.76f,.61f,.46f);
            foreach(string gate in new[]{"frontier_gate","manor_gatehouse"})
            {
                var list=new List<Part>{Exact("TallWallEntrance",1.1f,3.57f,8.25f,0,0,0,90)};
                for(int sign=-1;sign<=1;sign+=2)list.Add(Exact("WatchTowerWRoof",1.1f,3.8f,1.01f,0,0,sign*3.55f));
                WritePartPair(gate,list);
            }
            One("carry_crate","wooden_crate_01",.46f,.38f,.41f);One("carry_bucket","bucket_water",.28f,.33f,.28f);
            One("carry_basket","Bags",.37f,.31f,.31f);One("carry_jug","Cauldron",.22f,.27f,.22f);
            One("carry_hoe","Shovel",.19f,.66f,.094f);One("carry_sickle","axe_2handed",.22f,.38f,.09f);One("carry_hammer","Hammer_Small",.28f,.39f,.13f);
            foreach(string id in new[]{"arrow","bolt","bomb","stone"})
            {
                string art=id=="bomb"?"smokebomb":id=="stone"?"rock_07":"arrow";
                WritePartPair(id,new[]{new Part(art,id=="stone"?.92f:id=="bomb"?.37f:.072f,
                    id=="stone"?.87f:id=="bomb"?.39f:.072f,id=="bomb"?.37f:id=="bolt"?.78f:1,exact:true,center:true)});
            }
            // The moving arm and the wheeled carriage are independent parts of Kenney's supplied FBX.
            WritePartPair("catapult",new[]{new Part("siege-catapult",1.29f,1.51f,1.69f,0,ContactShape.CartCenterY-.38f,0,90,"!catapult",true)});
            WritePartPair("catapult_arm",new[]{new Part("siege-catapult",.43f,.23f,1.99f,0,0,-.32f,90,"catapult",true,true)});
            WritePartPair("wreck_catapult",new[]{Exact("siege-catapult-demolished",1.60f,.82f,1.90f)});
        }
    }
}
