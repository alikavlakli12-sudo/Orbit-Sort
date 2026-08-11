Shader "OrbitSort/PremiumBackdrop"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.72, 0.80, 1.00, 1)
        _BottomColor ("Bottom Color", Color) = (0.62, 0.55, 0.91, 1)
        _CenterColor ("Center Glow", Color) = (0.91, 0.86, 1.00, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-10"
            "RenderType" = "Opaque"
        }
        LOD 220

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows noforwardadd
        #pragma target 3.0

        fixed4 _TopColor;
        fixed4 _BottomColor;
        fixed4 _CenterColor;
        half _Smoothness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float vertical = smoothstep(-9.0, 9.0, input.worldPos.y);
            fixed3 gradient = lerp(
                _BottomColor.rgb,
                _TopColor.rgb,
                vertical);

            float2 glowCoordinates =
                input.worldPos.xy * float2(0.105, 0.080);
            float centerGlow = saturate(1.0 - length(glowCoordinates));
            centerGlow = centerGlow * centerGlow * 0.34;
            fixed3 color = lerp(
                gradient,
                _CenterColor.rgb,
                centerGlow);

            output.Albedo = color;
            output.Emission = color * 0.12;
            output.Metallic = 0.0;
            output.Smoothness = _Smoothness;
            output.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
