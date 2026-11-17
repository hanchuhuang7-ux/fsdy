Shader "Frontline/GradientSky" {
 Properties {_Zenith("Zenith",Color)=(.18,.29,.38,1) _Horizon("Horizon",Color)=(.7,.76,.7,1) _Nadir("Below",Color)=(.18,.22,.25,1)}
 SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Zenith,_Horizon,_Nadir;
 struct v2f {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
 v2f vert(float4 vertex:POSITION){v2f o;o.pos=UnityObjectToClipPos(vertex);o.direction=vertex.xyz;return o;}
 fixed4 frag(v2f i):SV_Target {float h=normalize(i.direction).y;float3 c=h>0?lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(h),.55)):lerp(_Horizon.rgb,_Nadir.rgb,pow(saturate(-h),.75));return float4(c,1);}
 ENDCG}
 } Fallback Off
}
