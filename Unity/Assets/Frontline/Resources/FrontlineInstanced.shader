Shader "Frontline/Instanced" {
 Properties { _SurfaceAtlas("Albedo and roughness",2D)="white" {} _NormalAtlas("Detail normals",2D)="bump" {} _Color("Material tint",Color)=(1,1,1,1) _SkyLight("Hemisphere sky",Color)=(.58,.65,.72,1) _GroundLight("Hemisphere ground",Color)=(.25,.27,.21,1) }
 CGINCLUDE
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_Wind)
 UNITY_DEFINE_INSTANCED_PROP(float,_HitFlash)
 UNITY_INSTANCING_BUFFER_END(Props)
 float4 _Color,_SkyLight,_GroundLight,_FillColor,_FillDirection;
 float _LakeEnvironment,_RigAttributes;
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;float2 uv:TEXCOORD0;float4 surface:TEXCOORD1;float4 flex:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
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
   v2f o; UNITY_SETUP_INSTANCE_ID(v); v.vertex=WindVertex(v);
   o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
   o.pos=UnityObjectToClipPos(v.vertex);
   o.normal=UnityObjectToWorldNormal(v.normal);
   half3 team=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor).rgb;
   o.dye=half4(v.color.rgb*lerp(half3(1,1,1),team,v.color.a)*_Color.rgb,UNITY_ACCESS_INSTANCED_PROP(Props,_HitFlash));
   o.surface=v.surface;o.uv=v.uv;
   TRANSFER_SHADOW(o); UNITY_TRANSFER_FOG(o,o.pos);return o;
 }
 sampler2D _SurfaceAtlas,_NormalAtlas;
 half4 Swatch(sampler2D atlas,float2 uv,half tile)
 {
   float2 grid=float2(.25,.5), origin=float2(fmod(tile,4.0),floor(tile*.25));
   // Analytic gradients are taken before wrapping; cap the footprint within the periodic gutter.
   float2 dx=clamp(ddx(uv),-.055,.055)*(.875*grid);
   float2 dy=clamp(ddy(uv),-.055,.055)*(.875*grid);
   return tex2Dgrad(atlas,(origin+.0625+frac(uv)*.875)*grid,dx,dy);
 }
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
   half3 weight=abs(n);weight*=weight;weight*=weight;weight/=max(.001,dot(weight,half3(1,1,1)));
   float2 plane=p.xz*.53;
   half4 grass=Swatch(_SurfaceAtlas,plane,6);
   half2 flatNormal=Swatch(_NormalAtlas,plane,6).rg*2-1;
   half3 bump=half3(flatNormal.x,0,flatNormal.y)*weight.y;
   half3 pigment=grass.rgb*weight.y;
   if(weight.y<.995)
   {
     float2 xProjection=p.zy*.61,zProjection=p.xy*.61;
     half2 nr=Swatch(_NormalAtlas,xProjection,0).rg*2-1;
     half2 nf=Swatch(_NormalAtlas,zProjection,0).rg*2-1;
     pigment+=weight.x*Swatch(_SurfaceAtlas,xProjection,0).rgb+weight.z*Swatch(_SurfaceAtlas,zProjection,0).rgb;
     bump+=weight.x*half3(0,nr.y,nr.x)+weight.z*half3(nf.x,nf.y,0);
   }
   half patch=Swatch(_SurfaceAtlas,p.xz*.041,7).g;
   half moss=smoothstep(.67,1.02,patch), path=smoothstep(.62,.95,1-grass.r*.65+(.82-patch));
   half slope=saturate((1-n.y)*2.3+max(0,p.y-4)*.025);
   half beach=saturate((-p.y+.19)*.71);
   half3 biome=lerp(half3(.94,.96,.90),half3(.82,.88,.77),moss);
   biome=lerp(biome,half3(1.07,.94,.82),path*.62);
   biome=lerp(biome,half3(.87,.86,.81),slope);
   biome=lerp(biome,half3(1.10,1.02,.85),beach);
   color=lerp(dot(color,half3(.24,.59,.17)).xxx,color,.72)*pigment*1.06*biome;
   n=normalize(n+bump*.46);rough=lerp(.89,.53,beach);
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
     if(id==7)Terrain(i.world,n,pigment,rough);
     else
     {
       half tile=id<=8?id-1:id==9?6:id==10?5:id==11?1:id==13?6:2;
       half4 detail=Swatch(_SurfaceAtlas,i.uv,tile);
       half strength=id==10?.23:id==12?.12:1;
       pigment*=lerp(half3(1,1,1),detail.rgb*1.13,strength);
       half2 bump=(Swatch(_NormalAtlas,i.uv,tile).rg*2-1)*strength;
       n=DetailNormal(i.world,i.uv,n,bump*.73);
       rough=clamp(rough+(detail.a-.65)*.24,.17,.97);
     }
   }
   half shore=0;
   if(_LakeEnvironment>.5 && (id==7 || id==9 || id==13)) {
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
   sum+=reflection*(min(5.5,distribution*visibility)*direct+ambient*(.18+.36*metal)*ao);
   sum+=rim*(.018+.039*metal)*lerp(half3(.58,.68,.81),_LightColor0.rgb,nl);
   if(id==8) {
     half through=saturate(dot(-v,l)*.75+.25)*(1-nl);
     sum+=pigment*half3(.23,.32,.105)*through*(.36+.64*light);
   }
   if(id==14 || id==15) {
     half pulse=.90+.075*sin(_Time.y*(id==15?8.1:1.7)+dot(i.world.xz,float2(2,3)))+.025*sin(_Time.y*13+i.world.y*9);
     sum+=(id==15?half3(1.38,.32,.044):half3(.51,.255,.079))*pulse;
   }
   sum+=shore*.055*rim*_SkyLight.rgb;
   fixed4 c=fixed4(lerp(Film(sum),half3(1,.97,.90),saturate(i.dye.a)*.88),1);
   UNITY_APPLY_FOG(i.fogCoord,c);return c;
 }
 #else
 struct shadowOut {V2F_SHADOW_CASTER;};
 shadowOut vertShadow(appdata v){shadowOut o;UNITY_SETUP_INSTANCE_ID(v);v.vertex=WindVertex(v);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);return o;}
 float4 fragShadow(shadowOut i):SV_Target {SHADOW_CASTER_FRAGMENT(i)}
 #endif
 ENDCG
 SubShader { Tags {"RenderType"="Opaque"} LOD 150
   Pass { Tags {"LightMode"="ForwardBase"}
     CGPROGRAM
     #pragma exclude_renderers gles3
     #pragma target 3.0
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
     #pragma target 3.0
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
     #pragma target 3.0
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
     #pragma target 3.0
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
