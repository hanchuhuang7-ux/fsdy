Shader "Frontline/Instanced" {
 Properties { _Color("Material tint",Color)=(1,1,1,1) _SkyLight("Hemisphere sky",Color)=(.58,.65,.72,1) _GroundLight("Hemisphere ground",Color)=(.25,.27,.21,1) }
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
   if(strength==0)return v.vertex;
   float3 origin=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
   float phase=_Time.y*1.65+origin.x*.57+origin.z*.38;
   float height=max(0,v.vertex.y);
   v.vertex.x+=sin(phase+height*1.6)*height*strength;
   v.vertex.z+=cos(phase*.83+height)*height*strength*.36;
   return v.vertex;
 }
 #if !defined(UNITY_PASS_SHADOWCASTER)
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 struct v2f {float4 pos:SV_POSITION;half3 baseLight:COLOR0;half4 directAndFlash:TEXCOORD0;SHADOW_COORDS(1) UNITY_FOG_COORDS(2)};
 v2f vert(appdata v)
 {
   v2f o;UNITY_SETUP_INSTANCE_ID(v);v.vertex=WindVertex(v);o.pos=UnityObjectToClipPos(v.vertex);
   // Each baked face has a constant normal and colour. Only shadow attenuation and fog vary across the face.
   half3 n=UnityObjectToWorldNormal(v.normal);
   half3 ambient=lerp(_GroundLight.rgb,_SkyLight.rgb,n.y*.5+.5);
   half fill=max(0,dot(n,_FillDirection.xyz));
   half3 tint=UNITY_ACCESS_INSTANCED_PROP(Props,_TeamColor).rgb;
   half3 albedo=v.color.rgb*lerp(half3(1,1,1),tint,v.color.a)*_Color.rgb;
   o.baseLight=albedo*(ambient+fill*_FillColor.rgb);
   o.directAndFlash=half4(albedo*_LightColor0.rgb*(.62*max(0,dot(n,_WorldSpaceLightPos0.xyz))),
       saturate(UNITY_ACCESS_INSTANCED_PROP(Props,_HitFlash))*.88);
   TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o;
 }
 fixed4 frag(v2f i):SV_Target
 {
   half3 lit=i.baseLight+i.directAndFlash.rgb*SHADOW_ATTENUATION(i);
   fixed4 c=fixed4(lerp(lit,half3(1.35,1.38,1.4),i.directAndFlash.a),1);
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
