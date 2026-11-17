Shader "Frontline/Instanced" {
 Properties { _Color("Material tint",Color)=(1,1,1,1) _SkyLight("Hemisphere sky",Color)=(.58,.65,.72,1) _GroundLight("Hemisphere ground",Color)=(.25,.27,.21,1) }
 SubShader { Tags {"RenderType"="Opaque"} LOD 150
 CGINCLUDE
 #include "UnityCG.cginc"
 UNITY_INSTANCING_BUFFER_START(Props)
 UNITY_DEFINE_INSTANCED_PROP(float4,_TeamColor)
 UNITY_DEFINE_INSTANCED_PROP(float,_Wind)
 UNITY_DEFINE_INSTANCED_PROP(float,_HitFlash)
 UNITY_INSTANCING_BUFFER_END(Props)
 float4 _Color,_SkyLight,_GroundLight,_FillColor,_FillDirection;
 struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 float4 WindVertex(appdata v) {
   float strength=UNITY_ACCESS_INSTANCED_PROP(Props,_Wind);
   float3 origin=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
   float phase=_Time.y*1.65+origin.x*.57+origin.z*.38;
   float height=max(0,v.vertex.y);
   v.vertex.x+=sin(phase+height*1.6)*height*strength;
   v.vertex.z+=cos(phase*.83+height)*height*strength*.36;
   return v.vertex;
 }
 ENDCG
 Pass { Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma target 3.0
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fwdbase
 #pragma multi_compile_fog
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 struct v2f {float4 pos:SV_POSITION;half3 normal:TEXCOORD0;fixed4 color:COLOR;SHADOW_COORDS(1) UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);v.vertex=WindVertex(v);o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color;TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o;}
 fixed4 frag(v2f i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.normal);half hemi=n.y*.5+.5;half3 ambient=lerp(_GroundLight.rgb,_SkyLight.rgb,hemi);half sun=max(0,dot(n,_WorldSpaceLightPos0.xyz))*SHADOW_ATTENUATION(i);half fill=max(0,dot(n,normalize(_FillDirection.xyz)));fixed4 team=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor);half3 albedo=i.color.rgb*lerp(half3(1,1,1),team.rgb,i.color.a)*_Color.rgb;fixed4 c=fixed4(albedo*(ambient+sun*_LightColor0.rgb*.62+fill*_FillColor.rgb),1);c.rgb=lerp(c.rgb,half3(1.35,1.38,1.4),saturate(UNITY_ACCESS_INSTANCED_PROP(Props,_HitFlash))*.88);UNITY_APPLY_FOG(i.fogCoord,c);return c;}
 ENDCG
 }
 Pass {Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual
 CGPROGRAM
 #pragma target 3.0
 #pragma vertex vertShadow
 #pragma fragment fragShadow
 #pragma multi_compile_shadowcaster
 #pragma multi_compile_instancing
 struct shadowOut {V2F_SHADOW_CASTER;};
 shadowOut vertShadow(appdata v){shadowOut o;UNITY_SETUP_INSTANCE_ID(v);v.vertex=WindVertex(v);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);return o;}
 float4 fragShadow(shadowOut i):SV_Target {SHADOW_CASTER_FRAGMENT(i)}
 ENDCG
 }
 }
 Fallback Off
}
