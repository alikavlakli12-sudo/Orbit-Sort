using System.IO;
using OrbitSort.Core;
using OrbitSort.Data;
using OrbitSort.Presentation;
using UnityEditor;
using UnityEngine;

namespace OrbitSort.Editor
{
    public static class PrototypePreviewRenderer
    {
        private const int PreviewSize = 1400;

        [MenuItem("Orbit Sort/Render Static Board Preview")]
        public static void RenderStaticBoardPreview()
        {
            LevelCatalogData catalog =
                LevelCatalogLoader.LoadFromResources();
            BoardModel model = new BoardModel(catalog.levels[0]);

            GameObject boardObject =
                new GameObject("Static Preview Board");
            OrbitSortBoardView boardView =
                boardObject.AddComponent<OrbitSortBoardView>();
            boardView.Initialize();
            boardView.Render(model);

            GameObject cameraObject =
                new GameObject("Static Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.65f;
            camera.transform.position = new Vector3(0f, 0f, -20f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.035f, 0.14f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            OrbitSortLightingRig.Configure(camera, boardObject.transform);

            RenderTexture target = new RenderTexture(
                PreviewSize,
                PreviewSize,
                24,
                RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(
                PreviewSize,
                PreviewSize,
                TextureFormat.RGB24,
                false);
            image.ReadPixels(
                new Rect(0f, 0f, PreviewSize, PreviewSize),
                0,
                0);
            image.Apply();

            string projectRoot =
                Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory =
                Path.Combine(projectRoot, "Docs", "Previews");
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(
                outputDirectory,
                "orbit_sort_unity_topdown_static.png");
            File.WriteAllBytes(outputPath, image.EncodeToPNG());

            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);

            Debug.Log($"Static top-down board preview: {outputPath}");
        }
    }
}
