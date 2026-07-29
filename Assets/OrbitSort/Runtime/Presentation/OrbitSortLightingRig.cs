using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitSort.Presentation
{
    public static class OrbitSortLightingRig
    {
        public static void Configure(Camera camera, Transform parent)
        {
            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
            }

            QualitySettings.pixelLightCount =
                Mathf.Max(QualitySettings.pixelLightCount, 4);
            QualitySettings.antiAliasing =
                Mathf.Max(QualitySettings.antiAliasing, 4);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance =
                Mathf.Max(QualitySettings.shadowDistance, 30f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor =
                new Color(0.18f, 0.22f, 0.44f);
            RenderSettings.ambientEquatorColor =
                new Color(0.09f, 0.08f, 0.24f);
            RenderSettings.ambientGroundColor =
                new Color(0.025f, 0.018f, 0.075f);
            RenderSettings.ambientIntensity = 0.62f;
            RenderSettings.reflectionIntensity = 0.62f;
            RenderSettings.fog = false;

            CreateDirectional(
                parent,
                "Orbit Sort Warm Key",
                new Color(1.0f, 0.82f, 0.64f),
                0.60f,
                new Vector3(20f, -30f, 0f),
                true);
            CreateDirectional(
                parent,
                "Orbit Sort Cool Key Fill",
                new Color(0.58f, 0.66f, 1.0f),
                0.38f,
                new Vector3(-18f, 35f, 0f),
                false);
            CreatePoint(
                parent,
                "Orbit Sort Cool Fill",
                new Color(0.34f, 0.50f, 1.0f),
                1.40f,
                20f,
                new Vector3(-5.5f, 1.5f, -6.0f));
            CreatePoint(
                parent,
                "Orbit Sort Warm Fill",
                new Color(1.0f, 0.48f, 0.23f),
                0.90f,
                22f,
                new Vector3(5.0f, -4.0f, -7.0f));
            CreatePoint(
                parent,
                "Orbit Sort Violet Rim",
                new Color(0.48f, 0.34f, 1.0f),
                0.90f,
                20f,
                new Vector3(0.0f, 6.0f, -7.0f));
        }

        private static void CreateDirectional(
            Transform parent,
            string name,
            Color color,
            float intensity,
            Vector3 eulerAngles,
            bool castShadows)
        {
            Light light = CreateLight(parent, name, LightType.Directional);
            light.color = color;
            light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(eulerAngles);
            light.shadows =
                castShadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.58f;
            light.shadowBias = 0.035f;
            light.shadowNormalBias = 0.22f;
            light.shadowNearPlane = 0.1f;
        }

        private static void CreatePoint(
            Transform parent,
            string name,
            Color color,
            float intensity,
            float range,
            Vector3 position)
        {
            Light light = CreateLight(parent, name, LightType.Point);
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.transform.localPosition = position;
            light.shadows = LightShadows.None;
        }

        private static Light CreateLight(
            Transform parent,
            string name,
            LightType type)
        {
            Transform child = parent.Find(name);
            GameObject lightObject =
                child != null ? child.gameObject : new GameObject(name);
            lightObject.transform.SetParent(parent, false);

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
            }

            light.type = type;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }
    }
}
