using OrbitSort.Core;
using OrbitSort.Data;
using OrbitSort.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace OrbitSort.Tests.EditMode
{
    public sealed class BoardPresentationOptimizationTests
    {
        [Test]
        public void SynchronizingRotationKeepsImportedRingInstances()
        {
            GameObject host = new GameObject("Optimized Board Test");
            try
            {
                LevelCatalogData catalog =
                    LevelCatalogLoader.LoadFromResources();
                BoardModel model = new BoardModel(catalog.levels[0]);
                OrbitSortBoardView view =
                    host.AddComponent<OrbitSortBoardView>();
                view.Initialize();
                view.Render(model);

                Transform content = host.transform.Find("Board Content");
                Transform innerRing = content.Find("inner Ring");
                Transform importedRing =
                    innerRing.Find("Blender Ring Geometry");

                BoardActionResult result =
                    model.TryRotateRing("inner", 1);
                Assert.That(result.Succeeded, Is.True);

                view.SynchronizeModel(model);

                Assert.That(
                    host.transform.Find("Board Content"),
                    Is.SameAs(content));
                Assert.That(
                    content.Find("inner Ring"),
                    Is.SameAs(innerRing));
                Assert.That(
                    innerRing.Find("Blender Ring Geometry"),
                    Is.SameAs(importedRing));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
