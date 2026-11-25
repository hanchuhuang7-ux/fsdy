Shader "Frontline/Instanced" {
 Properties { _BonePalette("Baked artist bones",2D)="black" {} _Color("Material tint",Color)=(1,1,1,1) _SkyLight("Hemisphere sky",Color)=(.58,.65,.72,1) _GroundLight("Hemisphere ground",Color)=(.25,.27,.21,1) }
 CGINCLUDE
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_Wind)
 UNITY_DEFINE_INSTANCED_PROP(float,_HitFlash)
 UNITY_INSTANCING_BUFFER_END(Props)
 UNITY_DECLARE_TEX2DARRAY(_LandscapeColor);
 UNITY_DECLARE_TEX2DARRAY(_LandscapeDetail);
 UNITY_DECLARE_TEX2DARRAY(_SpecimenColor);
 UNITY_DECLARE_TEX2DARRAY(_SpecimenDetail);
 UNITY_DECLARE_TEX2DARRAY(_FoliageViews);
 UNITY_DECLARE_TEX2DARRAY(_WardrobeColor);
 UNITY_DECLARE_TEX2DARRAY(_WardrobeDetail);
 sampler2D _BonePalette;
 float4 _BonePalette_TexelSize;
 float4 _Color,_SkyLight,_GroundLight,_FillColor,_FillDirection;
 float _LakeEnvironment,_RigAttributes,_EstateWinter;
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 surface:TEXCOORD1;float4 flex:TEXCOORD2;float4 joints:TEXCOORD3;float4 weights:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID};
 float4 WindVertex(appdata v) {
   float strength=UNITY_ACCESS_INSTANCED_PROP(Props,_Wind);
   // Platforms can alias missing texture coordinates to an earlier vertex stream.
   // Only authored LOD0 pages own UV2. In particular, no proxy interprets UV0 as a hinge weight.
   #if defined(UNITY_PASS_SHADOWCASTER)
   if(strength<=0)return v.vertex;
   v.flex=0;
   #else
   if(_RigAttributes<.5) {
     if(strength<=0)return v.vertex;
     v.flex=0;
   } else {
     v.flex=clamp(v.flex,float4(0,-1,0,0),float4(1,1,1,1));
     if(strength<-6){
       if(v.flex.w>0){
         float a=(strength+8)*3.141593*v.flex.w;
         float s,c;sincos(a,s,c);
         float2 yz=v.vertex.yz+float2(.263,0);
         v.vertex.yz=float2(c*yz.x-s*yz.y,s*yz.x+c*yz.y)-float2(.263,0);
       }
       return v.vertex;
     }
     if(strength==0 && max(v.flex.x,v.flex.y)<=0)return v.vertex;
   }
   #endif
   float3 origin=unity_ObjectToWorld._m03_m13_m23;
   float seed=dot(origin.xz,float2(.57,.38));
   // Negative values use the existing wind slot for an actor's motion. Pins and rigid steel stay fixed.
   if(strength<0 || max(v.flex.x,v.flex.y)>0) {
     float draw=saturate(-strength-3);
     float drive=strength<=-3?0:saturate(-strength-1);
     float tintCode=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor).a;
     float turn=strength<0?(tintCode>=4?fmod(tintCode-4,4)-1:tintCode-1):0;
     if(v.flex.z>0 && tintCode>=4) {
       float yaw=radians(floor((tintCode-4)*.25)-48)*v.flex.z;
       float s,c;sincos(yaw,s,c);
       v.vertex.xz=float2(c*v.vertex.x+s*v.vertex.z,-s*v.vertex.x+c*v.vertex.z);
     }
     v.vertex.z-=.34*draw*saturate(-v.flex.y);
     float t=_Time.y*(2.1+4.8*drive)+seed;
     float wave=sin(t+v.vertex.x*8.3+v.vertex.y*4.7);
     v.vertex.xyz+=float3(wave*(.033+.092*drive)+.115*turn,drive*(.15+.065*wave)+.011*wave,
       -.27*drive+cos(t*.83+v.vertex.y*7)*(.025+.059*drive))*v.flex.x;
     if(v.flex.y>0) {
       float nod=(.035*sin(t*.64)+.092*pow(saturate(sin(t*.27)),6))*(1-drive);
       v.vertex.y-=nod*(v.vertex.z-.38)*v.flex.y;
       v.vertex.x+=.035*sin(t*.43)*v.flex.y*(1-drive);
     }
     return v.vertex;
   }
   float phase=_Time.y*1.65+seed;
   float height=max(0,v.vertex.y);
   v.vertex.x+=sin(phase+height*1.6)*height*strength;
   v.vertex.z+=cos(phase*.83+height)*height*strength*.36;
   return v.vertex;
 }
 void JointRows(float bone,float row,float w,inout float4 a,inout float4 b,inout float4 c)
 {
   float x=(bone*3+.5)*_BonePalette_TexelSize.x;
   a+=w*tex2Dlod(_BonePalette,float4(x,row,0,0));
   b+=w*tex2Dlod(_BonePalette,float4(x+_BonePalette_TexelSize.x,row,0,0));
   c+=w*tex2Dlod(_BonePalette,float4(x+2*_BonePalette_TexelSize.x,row,0,0));
 }
 void ArtistVertex(inout appdata v)
 {
   #if defined(FRONTLINE_SKIN)
   float row=(UNITY_ACCESS_INSTANCED_PROP(Props,_Wind)+.5)*_BonePalette_TexelSize.y;
   float4 a=0,b=0,c=0;
   JointRows(v.joints.x,row,v.weights.x,a,b,c);
   if(v.weights.y>.0001)JointRows(v.joints.y,row,v.weights.y,a,b,c);
   if(v.weights.z>.0001)JointRows(v.joints.z,row,v.weights.z,a,b,c);
   if(v.weights.w>.0001)JointRows(v.joints.w,row,v.weights.w,a,b,c);
   v.vertex=float4(dot(a,v.vertex),dot(b,v.vertex),dot(c,v.vertex),1);
   v.normal=normalize(float3(dot(a.xyz,v.normal),dot(b.xyz,v.normal),dot(c.xyz,v.normal)));
   if(_RigAttributes>.5){float w=saturate(v.flex.x),drive=saturate(UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor).a-1);
     float swing=sin(_Time.y*(2.5+4*drive)+v.vertex.y*6+unity_ObjectToWorld._m03*.62);
     v.vertex.xyz+=float3(.034*swing,.082*drive,-.17*drive+.028*swing)*w;}
   #else
   v.vertex=WindVertex(v);
   #endif
 }
 float VisibleSlice(float3 direction,float id)
 {
   float3 v=normalize(direction);
   float4 scores=abs(float4(v.z,-.866025*v.x+.5*v.z,-.866025*v.x-.5*v.z,v.y));
   float best=max(max(scores.x,scores.y),max(scores.z,scores.w));
   float winner=best==scores.x?0:best==scores.y?1:best==scores.z?2:3;
   return floor(fmod(id-64,16)*.25)==winner;
 }
 #if !defined(UNITY_PASS_SHADOWCASTER)
 #include "Lighting.cginc"
 #include "AutoLight.cginc"

 struct v2f {
   float4 pos:SV_POSITION;
   float3 world:TEXCOORD0;
   half3 normal:TEXCOORD1;
   half4 dye:COLOR0;
   float2 uv:TEXCOORD2;
   half4 surface:TEXCOORD3;
   SHADOW_COORDS(4)
   UNITY_FOG_COORDS(5)
 };
 v2f vert(appdata v)
 {
   v2f o; UNITY_SETUP_INSTANCE_ID(v); ArtistVertex(v);
   o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
   o.pos=UnityObjectToClipPos(v.vertex);
   #if defined(FRONTLINE_FOLIAGE)
   if(v.surface.x>=64&&v.surface.x<112&&!VisibleSlice(mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz-float3(0,1.6,0),v.surface.x))o.pos=float4(2,2,2,1);
   #endif
   o.normal=UnityObjectToWorldNormal(v.normal);
   half3 team=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor).rgb;
   o.dye=half4(v.color.rgb*lerp(half3(1,1,1),team,v.color.a)*_Color.rgb,UNITY_ACCESS_INSTANCED_PROP(Props,_HitFlash));
   o.surface=v.surface;o.uv=v.uv;
   TRANSFER_SHADOW(o); UNITY_TRANSFER_FOG(o,o.pos);return o;
 }
 half4 Scan(float2 uv,half layer){return UNITY_SAMPLE_TEX2DARRAY(_LandscapeColor,float3(uv,layer));}
 half4 Grain(float2 uv,half layer){return UNITY_SAMPLE_TEX2DARRAY(_LandscapeDetail,float3(uv,layer));}
 half3 DetailNormal(float3 pos,float2 uv,half3 n,half2 bump)
 {
   float3 dp1=ddx(pos),dp2=ddy(pos);
   float2 du1=ddx(uv),du2=ddy(uv);
   float3 p=cross(dp2,n),q=cross(n,dp1);
   float3 t=p*du1.x+q*du2.x,b=p*du1.y+q*du2.y;
   float scale=rsqrt(max(1e-10,max(dot(t,t),dot(b,b))));
   return normalize(n+(t*bump.x+b*bump.y)*scale);
 }
 void Terrain(float3 p,inout half3 n,inout half3 color,out half rough)
 {
   float2 plane=p.xz*.32;
   half patch=Scan(p.xz*.041,7).g;
   half moss=smoothstep(.39,.68,patch)*.21,beach=saturate((-p.y+.24)*.73);
   half4 grass=Scan(plane,6),ground=Grain(plane,6);
   half3 soil=lerp(grass.rgb*half3(.92,1.06,.88),Scan(p.xz*.27,7).rgb,moss);
   soil=lerp(soil,Scan(p.xz*.47,10).rgb,beach);
   half slope=smoothstep(.19,.64,1-abs(n.y));
   if(slope>.001) {
     float2 rock=abs(n.x)>abs(n.z)?p.zy:p.xy;
     soil=lerp(soil,Scan(rock*.45,14).rgb,slope);
   }
   color=soil*lerp(half3(.95,.96,.90),color*1.52,.38);
   n=normalize(n+half3(ground.x*2-1,0,ground.y*2-1)*.32);
   color*=lerp(.82,1.05,ground.a);rough=lerp(ground.b,.42,beach);
 }
 half3 Film(half3 linearColor)
 {
   half3 x=max(linearColor,0);
   half3 mapped=saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14));
   return sqrt(mapped)*half3(1,.988,.971);
 }
 fixed4 frag(v2f i):SV_Target
 {
   half id=floor(i.surface.x+.5);
   half3 n=normalize(i.normal);
   half3 pigment=i.dye.rgb;
   half metal=0,rough=.86,ao=1;
   if(id>.5)
   {
     metal=i.surface.y;rough=i.surface.z;ao=i.surface.w;
     if(id>=112) {
       half4 fabric=UNITY_SAMPLE_TEX2DARRAY(_WardrobeColor,float3(i.uv,id-112));
       half4 grain=UNITY_SAMPLE_TEX2DARRAY(_WardrobeDetail,float3(i.uv,id-112));
       half grey=dot(fabric.rgb,half3(.24,.62,.14));
       pigment=lerp(fabric.rgb,grey*(.19+1.50*i.dye.rgb),fabric.a*.86);
       n=DetailNormal(i.world,i.uv,n,(grain.rg*2-1)*.57);
       metal=0;rough=grain.b;ao*=lerp(.62,1,grain.a);
     } else if(id>=64) {
       half4 foliage=UNITY_SAMPLE_TEX2DARRAY(_FoliageViews,float3(i.uv,id-64));
       #if defined(FRONTLINE_FOLIAGE)
       clip(foliage.a-.27);
       #endif
       pigment=foliage.rgb*lerp(half3(1,1,1),pigment,.24);
       n=normalize(n*.28+half3(0,.90,0));rough=.86;
     } else if(id>=32) {
       half4 photo=UNITY_SAMPLE_TEX2DARRAY(_SpecimenColor,float3(i.uv,id-32));
       half4 detail=UNITY_SAMPLE_TEX2DARRAY(_SpecimenDetail,float3(i.uv,id-32));
       #if defined(FRONTLINE_FOLIAGE)
       if(id==34||id==36||id==37||id==43)clip(photo.a-.39);
       #endif
       pigment=photo.rgb*lerp(half3(1,1,1),pigment,.38);
       n=DetailNormal(i.world,i.uv,n,(detail.rg*2-1)*.61);rough=detail.b;ao*=lerp(.61,1,detail.a);
     } else if(id==16) {
       float3 p=i.world;
       half3 w=abs(n);w*=w;w/=max(.001,dot(w,half3(1,1,1)));
       float2 flat=p.xz*.48,side=p.zy*.48,front=p.xy*.48;
       half3 rock=w.x*Scan(side,14).rgb+w.y*Scan(flat,14).rgb+w.z*Scan(front,14).rgb;
       half sand=(1-smoothstep(-1.55,-.48,p.y))*(.38+.48*abs(n.y));
       half grass=smoothstep(-.49,.095,p.y)*(.43+.37*saturate(n.y));
       pigment=lerp(lerp(rock,Scan(flat,10).rgb,sand),Scan(flat,6).rgb,grass);
       rough=lerp(.87,.53,sand);
     } else if(id==7)Terrain(i.world,n,pigment,rough);
     else {
       half tile=id<=6?id-1:id==8?6:id==9?8:id==10?15:id==11?12:id==13?14:2;
       half3 photo=Scan(i.uv,tile).rgb;
       half4 grain=Grain(i.uv,tile);
       half luma=dot(photo,half3(.23,.62,.15));
       half detail=id==5?.18:id==10?.13:id==6?.48:id==14||id==15?.16:.83;
       half3 albedo=id==6||id==10?lerp(half3(1,1,1),(.59+luma*1.40).xxx,detail)*pigment:
         lerp(pigment,photo*(.64+.59*pigment),detail);
       pigment=albedo;ao*=lerp(.75,1,grain.a);
       n=DetailNormal(i.world,i.uv,n,(grain.rg*2-1)*(.44*detail));
       rough=clamp(lerp(rough,grain.b,id==5?.31:.67),.21,.95);
     }
   }
   if(id==4||id==11)pigment=lerp(pigment,half3(.80,.84,.84),_EstateWinter*smoothstep(.28,.86,n.y)*.76);
   half shore=0;
   if(_LakeEnvironment>.5 && (id==7 || id==9 || id==13 || id==16 || id==38)) {
     // The lake plane sits at -1.70 m. Wetting ends below the inhabited shelf.
     shore=1-smoothstep(-1.67,-1.08,i.world.y+.045*sin(i.world.x*3.7+i.world.z));
     pigment*=lerp(half3(1,1,1),half3(.65,.73,.74),shore);
     rough=lerp(rough,.28,shore);
   }
   half3 v=normalize(_WorldSpaceCameraPos-i.world),l=_WorldSpaceLightPos0.xyz;
   half3 h=normalize(l+v);
   half nl=saturate(dot(n,l)),nv=saturate(dot(n,v)),nh=saturate(dot(n,h));
   half luma=dot(pigment,half3(.23,.63,.14));
   pigment=lerp(luma.xxx,pigment,.80);
   half3 diffuse=pigment*pigment;
   half3 ambient=lerp(_GroundLight.rgb,_SkyLight.rgb,n.y*.5+.5);
   half light=SHADOW_ATTENUATION(i);
   half3 reflection=lerp(half3(.035,.037,.039),diffuse,metal);
   half alpha=max(.0625,rough*rough),a2=alpha*alpha;
   // A compact GGX NDF and Schlick visibility. Use float for grazing steel on GLES.
   float denominator=nh*nh*(a2-1.0)+1.0;
   float distribution=a2/max(.0001,3.14159*denominator*denominator);
   half visibility=rcp(max(.24,4*(nv*(1-alpha)+alpha)*(nl*(1-alpha)+alpha)));
   half rim=1-nv;rim*=rim;rim*=rim;
   half3 direct=_LightColor0.rgb*nl*light;
   half3 fill=_FillColor.rgb*saturate(dot(n,_FillDirection.xyz))*.32;
   half3 sum=diffuse*((ambient*.62+fill)*ao+direct*.84)*(1-.74*metal);
   sum+=metal*diffuse*ambient*.31;
   sum+=reflection*(min(5.5,distribution*visibility)*direct+ambient*(.18+.36*metal)*ao);
   sum+=rim*(.018+.039*metal)*lerp(half3(.58,.68,.81),_LightColor0.rgb,nl);
   if(id==8||(id>=64&&id<112)||id==34||id==36) {
     half through=saturate(dot(-v,l)*.75+.25)*(1-nl);
     sum+=pigment*half3(.23,.32,.105)*through*(.36+.64*light);
   }
   if(id==14 || id==15 || id==40) {
     half pulse=.90+.075*sin(_Time.y*(id==15?8.1:1.7)+dot(i.world.xz,float2(2,3)))+.025*sin(_Time.y*13+i.world.y*9);
     sum+=(id==15?half3(1.38,.32,.044):half3(.51,.255,.079))*pulse;
   }
   sum+=shore*.055*rim*_SkyLight.rgb;
   fixed4 c=fixed4(lerp(Film(sum),half3(1,.97,.90),saturate(i.dye.a)*.88),1);
   UNITY_APPLY_FOG(i.fogCoord,c);return c;
 }
 #else
 struct shadowOut {V2F_SHADOW_CASTER;float3 specimen:TEXCOORD1;};
 shadowOut vertShadow(appdata v){shadowOut o;UNITY_SETUP_INSTANCE_ID(v);v.vertex=WindVertex(v);o.specimen=float3(v.uv,v.surface.x);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
 #if defined(FRONTLINE_FOLIAGE)
 if(v.surface.x>=64&&v.surface.x<112&&!VisibleSlice(mul((float3x3)unity_WorldToObject,_WorldSpaceLightPos0.xyz),v.surface.x))o.pos=float4(2,2,2,1);
 #endif
 return o;}
 float4 fragShadow(shadowOut i):SV_Target {
 #if defined(FRONTLINE_FOLIAGE)
 if(i.specimen.z>=64&&i.specimen.z<112)clip(UNITY_SAMPLE_TEX2DARRAY(_FoliageViews,float3(i.specimen.xy,i.specimen.z-64)).a-.40);
 #endif
 SHADOW_CASTER_FRAGMENT(i)}
 #endif
 ENDCG
 SubShader { Tags {"RenderType"="Opaque"} LOD 150
   Pass { Tags {"LightMode"="ForwardBase"}
     CGPROGRAM
     #pragma exclude_renderers gles3
     #pragma target 3.5
     #pragma multi_compile_local __ FRONTLINE_SKIN FRONTLINE_FOLIAGE
     #pragma vertex vert
     #pragma fragment frag
     #pragma multi_compile_instancing
     #pragma multi_compile_fwdbase
     #pragma multi_compile_fog
     ENDCG
   }
   Pass { Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual
     CGPROGRAM
     #pragma exclude_renderers gles3
     #pragma target 3.5
     #pragma multi_compile_local __ FRONTLINE_FOLIAGE
     #pragma vertex vertShadow
     #pragma fragment fragShadow
     #pragma multi_compile_shadowcaster
     #pragma multi_compile_instancing
     ENDCG
   }
 }
 // GLES3 links a new program for each flexible array size. Use the same 64-instance page as BattleView.
 SubShader { Tags {"RenderType"="Opaque"} LOD 150
   Pass { Tags {"LightMode"="ForwardBase"}
     CGPROGRAM
     #pragma only_renderers gles3
     #pragma target 3.5
     #pragma multi_compile_local __ FRONTLINE_SKIN FRONTLINE_FOLIAGE
     #pragma vertex vert
     #pragma fragment frag
     #pragma multi_compile_instancing
     #pragma instancing_options forcemaxcount:64
     #pragma multi_compile_fwdbase
     #pragma multi_compile_fog
     ENDCG
   }
   Pass { Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual
     CGPROGRAM
     #pragma only_renderers gles3
     #pragma target 3.5
     #pragma multi_compile_local __ FRONTLINE_FOLIAGE
     #pragma vertex vertShadow
     #pragma fragment fragShadow
     #pragma multi_compile_shadowcaster
     #pragma multi_compile_instancing
     #pragma instancing_options forcemaxcount:64
     ENDCG
   }
 }
 Fallback Off
}
