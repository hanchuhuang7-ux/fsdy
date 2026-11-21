Shader "Frontline/Lake"
{
    Properties
    {
        _Water ("Lake", Color) = (.29,.47,.43,1)
        _Mist ("Horizon", Color) = (.74,.83,.77,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Water, _Mist;
            struct Interpolants
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float depth : TEXCOORD1;
            };
            Interpolants vert(float4 vertex : POSITION)
            {
                Interpolants o;
                o.position = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.depth = -UnityObjectToViewPos(vertex).z;
                return o;
            }
            half4 frag(Interpolants i) : SV_Target
            {
                float mist = saturate(i.depth * .00059);
                float nearWater = saturate(1 - i.depth * .0065);
                float ripple = sin(i.world.z * .76 + i.world.x * .085 + _Time.y * .36);
                float patch = sin(i.world.z * .083 - i.world.x * .116 + _Time.y * .24);
                half3 water = _Water.rgb + (ripple * .012 + patch * .014) * nearWater;
                return half4(lerp(water, _Mist.rgb, mist * (2 - mist)), 1);
            }
            ENDCG
        }
    }
}
