Shader "Frontline/Highlands" {
 SubShader { Tags {"RenderType"="Opaque"} Pass {
 CGPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 UNITY_DECLARE_TEX2DARRAY(_LandscapeColor);
 float3 _FrontlineSunDirection;
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;half4 color:COLOR;};
 struct v2f {float4 pos:SV_POSITION;half4 albedo:COLOR;float3 world:TEXCOORD0;};
 v2f vert(appdata v) {
   v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=v.vertex.xyz;
   float relief=saturate(dot(v.normal,_FrontlineSunDirection));
   float slope=1-saturate(v.normal.y);
   o.albedo.a=v.color.a;
   o.albedo.rgb=v.color.rgb*lerp(half3(.61,.70,.79),half3(.98,.95,.84),relief);
   o.albedo.rgb=lerp(o.albedo.rgb,half3(.48,.51,.54),slope*.22);return o;
 }
 half4 frag(v2f i):SV_Target {
   float2 p=i.world.xz;
   half mottling=.5+.5*sin(p.x*.073+p.y*.041)*cos(p.y*.063-p.x*.033);
   half haze=saturate((distance(_WorldSpaceCameraPos.xz,p)-95)*.00081);
   half textureValue=UNITY_SAMPLE_TEX2DARRAY(_LandscapeColor,float3(p*.030,7)).r;
   half3 air=lerp(half3(.44,.54,.63),unity_FogColor.rgb,.26);
   half clarity=lerp(.38,.70,smoothstep(0,.36,i.albedo.a));
   return half4(lerp(i.albedo.rgb*(.90+.09*mottling+.19*textureValue),air,haze*clarity),1);
 }
 ENDCG
 }}
 Fallback Off
}
