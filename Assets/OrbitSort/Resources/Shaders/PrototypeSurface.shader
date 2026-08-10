Shader "OrbitSort/PrototypeSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [PerRendererData] _ClipEnabled ("Clip Enabled", Float) = 0
        [PerRendererData] _ClipPlane ("Clip Plane", Vector) = (0, 0, 0, 1)
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

        UNITY_INSTANCING_BUFFER_START(OrbitSortPerRenderer)
            UNITY_DEFINE_INSTANCED_PROP(float, _ClipEnabled)
            UNITY_DEFINE_INSTANCED_PROP(float4, _ClipPlane)
        UNITY_INSTANCING_BUFFER_END(OrbitSortPerRenderer)

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float clipEnabled = UNITY_ACCESS_INSTANCED_PROP(
                OrbitSortPerRenderer,
                _ClipEnabled);
            float4 clipPlane = UNITY_ACCESS_INSTANCED_PROP(
                OrbitSortPerRenderer,
                _ClipPlane);
            float planeDistance = dot(
                float4(input.worldPos, 1.0),
                clipPlane);
            clip(lerp(1.0, planeDistance, saturate(clipEnabled)));

            output.Albedo = _Color.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Smoothness;
            output.Alpha = _Color.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
