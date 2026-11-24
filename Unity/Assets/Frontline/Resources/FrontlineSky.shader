Shader "Frontline/GradientSky"
{
    Properties {_Zenith("Zenith",Color)=(.27,.44,.56,1) _Horizon("Horizon",Color)=(.76,.76,.63,1) _Nadir("Below",Color)=(.22,.28,.28,1)}
    SubShader {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Zenith,_Horizon,_Nadir;
            float4 _FrontlineSunDirection;
            struct SkyVertex {float4 pos:SV_POSITION;float3 direction:TEXCOORD0;};
            SkyVertex vert(float4 v:POSITION){SkyVertex o;o.pos=UnityObjectToClipPos(v);o.direction=v.xyz;return o;}
            half4 frag(SkyVertex i):SV_Target
            {
                float3 d=normalize(i.direction);
                float height=saturate(d.y),above=smoothstep(-.02,.12,d.y);
                float sun=saturate(dot(d,_FrontlineSunDirection.xyz));
                half3 horizon=lerp(_Horizon.rgb,half3(.96,.80,.61),.23*sun*above);
                half3 color=lerp(horizon,d.y>0?_Zenith.rgb:_Nadir.rgb,pow(abs(d.y),.65));
                float2 sheet=d.xz/max(.17,height+.15);
                float bands=sin(sheet.y*14+sheet.x*2+1.6*sin(sheet.x*2.5))+sin(sheet.y*22-sheet.x*1.8);
                float cloud=smoothstep(.45,1.88,bands)*smoothstep(.05,.42,height)*(.08+.06*sun);
                color=lerp(color,half3(.90,.87,.78),cloud);
                color+=half3(.21,.14,.066)*pow(sun,18)+half3(.63,.52,.35)*smoothstep(.9992,.99994,sun);
                return half4(saturate(color),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
