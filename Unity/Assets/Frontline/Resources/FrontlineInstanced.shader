Shader "Frontline/Instanced" {
 Properties {_Color("Color",Color)=(1,1,1,1)}
 SubShader {Tags {"RenderType"="Opaque"} LOD 100
 Pass {Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fwdbase
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 fixed4 _Color;
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;SHADOW_COORDS(1) UNITY_VERTEX_INPUT_INSTANCE_ID};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);TRANSFER_SHADOW(o);return o;}
 fixed4 frag(v2f i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);float light=max(0,dot(normalize(i.normal),_WorldSpaceLightPos0.xyz));float shadow=SHADOW_ATTENUATION(i);return fixed4(_Color.rgb*(.44+light*.56*shadow),1);}
 ENDCG
 }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 }
}
