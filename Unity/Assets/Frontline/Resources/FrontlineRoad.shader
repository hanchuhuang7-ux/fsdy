Shader "Frontline/Road" {
 Properties {}
 CGINCLUDE
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 UNITY_INSTANCING_BUFFER_START(RoadProps)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_INSTANCING_BUFFER_END(RoadProps)
 UNITY_DECLARE_TEX2DARRAY(_LandscapeColor);
 UNITY_DECLARE_TEX2DARRAY(_LandscapeDetail);
 struct appdata {float4 vertex:POSITION;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float4 worldAndSpan:TEXCOORD0;half4 field:COLOR0;half3 dye:COLOR1;SHADOW_COORDS(1) UNITY_FOG_COORDS(2)};
 v2f vert(appdata v)
 {
     UNITY_SETUP_INSTANCE_ID(v);
     v2f o;o.pos=UnityObjectToClipPos(v.vertex);
     o.worldAndSpan.xy=mul(unity_ObjectToWorld,v.vertex).xz;
     o.worldAndSpan.zw=float2(length(unity_ObjectToWorld._m00_m10_m20),length(unity_ObjectToWorld._m02_m12_m22));
     o.field=v.color;o.dye=UNITY_ACCESS_INSTANCED_PROP(RoadProps,_TeamColor).rgb;
     TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o;
 }
 half4 frag(v2f i):SV_Target
 {
     float2 world=i.worldAndSpan.xy;
     float2 uv=world*.42;
     half pave=step(.28,i.field.b)*(1-step(.78,i.field.b));
     half layer=i.field.b>.78?6:pave>.5?9:8;
     half4 ground=UNITY_SAMPLE_TEX2DARRAY(_LandscapeColor,float3(uv,layer));
     half4 detail=UNITY_SAMPLE_TEX2DARRAY(_LandscapeDetail,float3(uv,layer));
     half drift=dot(ground.rgb,half3(.21,.65,.14))-.35;
     half lateral=abs(i.field.r*2-1);
     half edge=lateral+drift*.19+(detail.a-.65)*.12;
     half alpha=1-smoothstep(.62,1,edge);
     alpha*=smoothstep(-.018,.13,min(i.field.g,1-i.field.g)*i.worldAndSpan.w);
     half3 dry=lerp(ground.rgb,sqrt(max(ground.rgb,.001))*.86+.034,pave>.5?.22:.54);
     half3 base=dry*lerp(half3(1,1,1),i.dye*1.55,.30);
     if(i.field.b>.78)alpha*=.44*(1-smoothstep(.28,.98,length((i.field.rg-.5)*2)));
     else if(pave<.5){
       half rut=1-smoothstep(.024,.15,abs(lateral-(.44+drift*.12)));
       base*=1-.11*rut;
       base=lerp(base,UNITY_SAMPLE_TEX2DARRAY(_LandscapeColor,float3(world*.38,6)).rgb,
         smoothstep(.65,.98,edge)*.5+(1-smoothstep(.10,.32,lateral))*.19);
     }
     half3 n=normalize(half3((detail.r*2-1)*.42,1,(detail.g*2-1)*.42));
     half light=(.50+.77*saturate(dot(n,_WorldSpaceLightPos0.xyz))*SHADOW_ATTENUATION(i))*lerp(.78,1,detail.a);
     half3 x=base*base*light*lerp(half3(1,1,1),_LightColor0.rgb,.48);
     half4 c=half4(sqrt(saturate(x*(2.51*x+.03)/(x*(2.43*x+.59)+.14))),alpha);
     UNITY_APPLY_FOG(i.fogCoord,c);return c;
 }
 ENDCG
 SubShader { Tags {"Queue"="Geometry+40" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
 Pass {Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma exclude_renderers gles3
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fwdbase
 #pragma multi_compile_fog
 ENDCG
 }}
 SubShader { Tags {"Queue"="Geometry+40" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
 Pass {Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma only_renderers gles3
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma instancing_options forcemaxcount:64
 #pragma multi_compile_fwdbase
 #pragma multi_compile_fog
 ENDCG
 }}
 Fallback Off
}
