Shader "Frontline/Lake"
{
    Properties {
        _Water ("Deep water",Color)=(.18,.38,.36,1)
        _Mist ("Horizon",Color)=(.74,.83,.77,1)
        _ShoreDistance ("Shore distance",2D)="white" {}
        _WaveNormals ("Wind ripples",2D)="bump" {}
    }
    SubShader {
        Tags {"RenderType"="Opaque" "Queue"="Geometry+22"}
        ZWrite On
        Pass {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _ShoreDistance,_WaveNormals;
            float4 _ShoreRect,_FrontlineSunDirection;
            half4 _Water,_Mist;
            struct WaterVertex {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float depth:TEXCOORD1;};
            WaterVertex vert(float4 vertex:POSITION)
            {
                WaterVertex o;o.pos=UnityObjectToClipPos(vertex);
                o.world=mul(unity_ObjectToWorld,vertex).xyz;o.depth=-UnityObjectToViewPos(vertex).z;return o;
            }
            half4 frag(WaterVertex i):SV_Target
            {
                float2 p=i.world.xz;
                half3 v=normalize(_WorldSpaceCameraPos-i.world);
                float t=_Time.y;
                float2 advect=float2(.013,-.008)*t;
                float2 broad=tex2D(_WaveNormals,p*.047+advect*.33).rg*2-1;
                float2 crossUV=float2(p.x*.71-p.y*.70,p.x*.70+p.y*.71);
                float2 fine=tex2D(_WaveNormals,crossUV*.117-advect*.74+broad*.040).rg*2-1;
                float2 slope=(broad*.25+float2(fine.x-fine.y,fine.x+fine.y)*.105);
                float longWave=dot(p,float2(.69,.44))+t*.88+broad.x*4.2;
                float crossWave=dot(p,float2(-.56,.83))-t*.79+fine.y*2.8;
                half3 n=normalize(float3(-slope.x,1,-slope.y));
                half facing=saturate(dot(n,v)),fresnel=pow(1-facing,3.2);
                float2 map=(p-_ShoreRect.xy)/_ShoreRect.zw;
                float shore=tex2D(_ShoreDistance,map).r*16;
                half shallows=1-smoothstep(.5,11.8,shore);
                half3 water=lerp(_Water.rgb*.82,half3(.46,.55,.378),shallows*.82);
                water+=shallows*(.014*cos(crossWave+longWave)+.010*cos(2.3*longWave));
                half3 ray=reflect(-v,n);
                half3 sky=lerp(_Mist.rgb,half3(.41,.58,.67),saturate(ray.y*2.8));
                half sunward=saturate(dot(ray,_FrontlineSunDirection.xyz));
                sky=lerp(sky,half3(.96,.80,.56),pow(sunward,5)*.52);
                half3 color=lerp(water,sky,.12+.80*fresnel);
                float glint=pow(sunward,160.0);
                color+=half3(1,.77,.44)*glint*(.13+.31*saturate((broad.x+fine.y)*2.4+.23));
                float front=shore-.68-.33*sin(longWave*.59)-.22*cos(crossWave*.47);
                float foam=(1-smoothstep(.10,.56,abs(front)))*(.38+.32*cos(crossWave)*cos(longWave));
                color=lerp(color,half3(.80,.85,.73),saturate(foam)*saturate(1-i.depth*.0025));
                float mist=saturate(i.depth*.00059);
                return half4(lerp(color,_Mist.rgb,mist*(2-mist)),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
