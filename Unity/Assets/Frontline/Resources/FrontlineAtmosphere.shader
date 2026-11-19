Shader "Frontline/Atmosphere" {
 Properties { _GroundFade("Ground contact fade start and inverse span",Vector)=(0,0,0,0) }
 CGINCLUDE
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_Softness)
 UNITY_INSTANCING_BUFFER_END(Props)
 float4 _GroundFade;
 struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float2 patch:TEXCOORD1;half2 fadeSoftness:TEXCOORD2;fixed4 tint:COLOR0;UNITY_FOG_COORDS(0)};
 v2f vert(appdata v)
 {
   v2f o;UNITY_SETUP_INSTANCE_ID(v);
   o.pos=UnityObjectToClipPos(v.vertex);o.patch=v.vertex.xz*2;
   o.fadeSoftness=half2(1,UNITY_ACCESS_INSTANCED_PROP(Props,_Softness));
   o.tint=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor);
   if(_GroundFade.y>0){
     float3 center=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
     float t=saturate((distance(_WorldSpaceCameraPos,center)-_GroundFade.x)*_GroundFade.y);
     o.fadeSoftness.x=t*t*(3-2*t);
   }
   UNITY_TRANSFER_FOG(o,o.pos);return o;
 }
 fixed4 frag(v2f i):SV_Target
 {
   fixed4 c=i.tint;
   c.a*=i.fadeSoftness.x*lerp(1,saturate(1-dot(i.patch,i.patch)),i.fadeSoftness.y);
   UNITY_APPLY_FOG(i.fogCoord,c);return c;
 }
 ENDCG
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   Pass {
     CGPROGRAM
     #pragma exclude_renderers gles3
     #pragma target 3.0
     #pragma vertex vert
     #pragma fragment frag
     #pragma multi_compile_instancing
     #pragma multi_compile_fog
     ENDCG
   }
 }
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   Pass {
     CGPROGRAM
     #pragma only_renderers gles3
     #pragma target 3.0
     #pragma vertex vert
     #pragma fragment frag
     #pragma multi_compile_instancing
     #pragma instancing_options forcemaxcount:64
     #pragma multi_compile_fog
     ENDCG
   }
 }
 Fallback Off
}

