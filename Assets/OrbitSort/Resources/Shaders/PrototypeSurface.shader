Shader "OrbitSort/PrototypeSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
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
            half _Metallic;
            half _Smoothness;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct VertexToFragment
            {
                float4 position : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldNormal =
                    UnityObjectToWorldNormal(input.normal);
                output.worldPosition =
                    mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 keyDirection =
                    normalize(float3(-0.35, 0.45, -0.82));
                float diffuse = saturate(dot(normal, keyDirection));
                float3 viewDirection = normalize(
                    _WorldSpaceCameraPos.xyz - input.worldPosition);
                float3 halfDirection =
                    normalize(keyDirection + viewDirection);
                float specularPower =
                    lerp(10.0, 110.0, _Smoothness);
                float specular = pow(
                    saturate(dot(normal, halfDirection)),
                    specularPower);
                float3 specularColor =
                    lerp(float3(0.34, 0.34, 0.34), _Color.rgb, _Metallic);
                float3 surface =
                    _Color.rgb * (0.62 + 0.38 * diffuse);
                surface += specularColor
                    * specular
                    * lerp(0.12, 0.55, _Smoothness);
                return fixed4(surface, _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
