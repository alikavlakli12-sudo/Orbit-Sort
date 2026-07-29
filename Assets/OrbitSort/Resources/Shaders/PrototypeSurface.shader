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
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow noforwardadd
        #pragma target 3.0
        #pragma multi_compile_instancing

        fixed4 _Color;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = _Color.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Alpha = _Color.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
