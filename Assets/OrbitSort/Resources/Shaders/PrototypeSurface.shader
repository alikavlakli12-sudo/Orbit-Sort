Shader "OrbitSort/PrototypeSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct VertexToFragment
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldNormal =
                    UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                float3 keyDirection =
                    normalize(float3(-0.35, 0.45, -0.82));
                float lightAmount =
                    0.72
                    + 0.28
                    * saturate(
                        dot(normalize(input.worldNormal), keyDirection));
                return fixed4(_Color.rgb * lightAmount, _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
