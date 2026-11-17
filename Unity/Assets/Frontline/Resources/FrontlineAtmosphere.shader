Shader "Frontline/Atmosphere" {
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass {CGPROGRAM
 #pragma target 3.0
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_Softness)
 UNITY_INSTANCING_BUFFER_END(Props)
 struct appdata {float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float2 patch:TEXCOORD1;UNITY_FOG_COORDS(0) UNITY_VERTEX_INPUT_INSTANCE_ID};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);o.patch=v.vertex.xz*2;UNITY_TRANSFER_FOG(o,o.pos);return o;}
 fixed4 frag(v2f i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);fixed4 c=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor);float soft=UNITY_ACCESS_INSTANCED_PROP(Props,_Softness);c.a*=lerp(1,saturate(1-dot(i.patch,i.patch)),soft);UNITY_APPLY_FOG(i.fogCoord,c);return c;}
 ENDCG}
 } Fallback Off
}

