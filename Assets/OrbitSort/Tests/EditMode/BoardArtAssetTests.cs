using NUnit.Framework;
using UnityEngine;

namespace OrbitSort.Tests.EditMode
{
    public sealed class BoardArtAssetTests
    {
        private static readonly Quaternion BlenderBoardRotation =
            Quaternion.AngleAxis(180f, Vector3.up)
            * Quaternion.AngleAxis(90f, Vector3.right);

        [TestCase("PrototypeSurface")]
        [TestCase("AdditiveGlow")]
        [TestCase("PremiumBackdrop")]
        public void RuntimeShaderIsAvailable(string shaderName)
        {
            Shader shader = Resources.Load<Shader>(
                $"Shaders/{shaderName}");

            Assert.That(shader, Is.Not.Null);
        }

        [TestCase("RingInner", 4)]
        [TestCase("RingMiddle", 4)]
        [TestCase("RingOuter", 4)]
        [TestCase("Portal", 3)]
        [TestCase("Receiver", 3)]
        [TestCase("CenterHub", 1)]
        [TestCase("Backdrop", 1)]
        [TestCase("Marble", 1)]
        public void BlenderBoardModelIsAvailable(
            string modelName,
            int expectedRendererCount)
        {
            GameObject model = Resources.Load<GameObject>(
                $"Models/{modelName}");

            Assert.That(model, Is.Not.Null);
            Assert.That(
                model.GetComponentsInChildren<Renderer>(true),
                Has.Length.EqualTo(expectedRendererCount));
        }

        [TestCase("UI/LevelIndicator", 938, 384)]
        [TestCase("UI/SettingsButton", 692, 718)]
        public void ApprovedHudTextureIsAvailableAtFullResolution(
            string resourcePath,
            int expectedWidth,
            int expectedHeight)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(expectedWidth));
            Assert.That(texture.height, Is.EqualTo(expectedHeight));
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
        }

        [TestCase("RingInner")]
        [TestCase("RingMiddle")]
        [TestCase("RingOuter")]
        public void RingModelLiesFlatInTheGameplayPlane(string modelName)
        {
            GameObject host = new GameObject("Board Art Test");
            try
            {
                host.transform.rotation = BlenderBoardRotation;
                GameObject instance = Object.Instantiate(
                    Resources.Load<GameObject>($"Models/{modelName}"),
                    host.transform,
                    false);
                Bounds bounds = CombinedBounds(instance);

                Assert.That(bounds.size.x, Is.EqualTo(bounds.size.y).Within(0.02f));
                Assert.That(bounds.size.x, Is.GreaterThan(3f));
                Assert.That(bounds.size.z, Is.LessThan(0.8f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase("RingInner")]
        [TestCase("RingMiddle")]
        [TestCase("RingOuter")]
        [TestCase("Portal")]
        [TestCase("Receiver")]
        [TestCase("CenterHub")]
        [TestCase("Backdrop")]
        [TestCase("Marble")]
        public void BlenderModelIsCenteredOnItsPlacementPivot(
            string modelName)
        {
            GameObject host = new GameObject("Board Art Pivot Test");
            try
            {
                host.transform.rotation = BlenderBoardRotation;
                GameObject instance = Object.Instantiate(
                    Resources.Load<GameObject>($"Models/{modelName}"),
                    host.transform,
                    false);
                Bounds bounds = CombinedBounds(instance);

                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.04f));
                Assert.That(bounds.center.y, Is.EqualTo(0f).Within(0.04f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Bounds CombinedBounds(GameObject root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);

            Bounds result = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                result.Encapsulate(renderers[index].bounds);
            }

            return result;
        }
    }
}
