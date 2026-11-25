Shader "Frontline/Road" {
 Properties { _RoadDetail("Earth, gravel, setts and lichen",2D)="white" {} }
 CGINCLUDE
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 UNITY_INSTANCING_BUFFER_START(RoadProps)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_INSTANCING_BUFFER_END(RoadProps)
 sampler2D _RoadDetail;
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
     half4 detail=tex2D(_RoadDetail,world*.78);
     half drift=tex2D(_RoadDetail,world*.086+.371).a-.5;
     half lateral=abs(i.field.r*2-1);
     half edge=lateral+drift*.22+(detail.a-.5)*.16;
     half alpha=1-smoothstep(.66,1,edge);
     float endDist=min(i.field.g,1-i.field.g)*i.worldAndSpan.w;
     alpha*=smoothstep(-.018,.13,endDist);
     half3 base=i.dye;
     if(i.field.b>.78) // Trampled grass at the survey ropes.
     {
         half island=1-smoothstep(.29,.96,length((i.field.rg-.5)*2)+drift*.25);
         alpha=island*(.22+.27*detail.a);
         base*=.83+detail.r*.25;
     }
     else if(i.field.b>.28)
     {
         half edging=smoothstep(.57,.65,lateral);
         half4 curb=tex2D(_RoadDetail,float2(i.field.g*i.worldAndSpan.w*1.01,i.field.r*.48));
         half stone=lerp(detail.b,curb.b,edging);
         base*=lerp(.66,1.15,stone)*(.88+.19*detail.r);
         base=lerp(base,half3(.35,.38,.245),(.98-stone)*.16);
     }
     else
     {
         half rut=1-smoothstep(.038,.16,abs(lateral-(.43+drift*.17+(detail.a-.5)*.047)));
         half grass=(1-smoothstep(.07,.39,lateral))*(.32+.48*drift);
         base*=.77+.32*detail.r+.16*detail.g-(.071+.081*detail.a)*rut;
         base=lerp(base,half3(.38,.414,.245),grass+smoothstep(.61,1,edge)*.35);
     }
     half light=.48+(max(.12,_WorldSpaceLightPos0.y)*.68+.13)*SHADOW_ATTENUATION(i);
     half3 x=base*base*light*lerp(half3(1,1,1),_LightColor0.rgb,.48);
     half4 c=half4(sqrt(saturate(x*(2.51*x+.03)/(x*(2.43*x+.59)+.14))),alpha);
     UNITY_APPLY_FOG(i.fogCoord,c);return c;
 }
 ENDCG
 SubShader { Tags {"Queue"="Geometry+40" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
 Pass {Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma exclude_renderers gles3
 #pragma target 3.0
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
 #pragma target 3.0
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
