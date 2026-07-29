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
                Transform innerRing =
                    content.Find("Dynamic Marbles/inner Ring");
                Transform importedRing =
                    content.Find(
                        "Static Board Geometry/inner Ring Geometry");

                BoardActionResult result =
                    model.TryRotateRing("inner", 1);
                Assert.That(result.Succeeded, Is.True);

                view.SynchronizeModel(model);

                Assert.That(
                    host.transform.Find("Board Content"),
                    Is.SameAs(content));
                Assert.That(
                    content.Find("Dynamic Marbles/inner Ring"),
                    Is.SameAs(innerRing));
                Assert.That(
                    content.Find(
                        "Static Board Geometry/inner Ring Geometry"),
                    Is.SameAs(importedRing));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TransferSelectionUsesTheAlignedMarbleNotThePortal()
        {
            GameObject host = new GameObject("Transfer Selection Test");
            try
            {
                LevelCatalogData catalog =
                    LevelCatalogLoader.LoadFromResources();
                BoardModel model = new BoardModel(catalog.levels[0]);
                BoardActionResult rotation =
                    model.TryRotateRing("inner", -1);
                Assert.That(rotation.Succeeded, Is.True);

                OrbitSortBoardView view =
                    host.AddComponent<OrbitSortBoardView>();
                view.Initialize();
                view.Render(model);

                bool marbleSelected =
                    view.TryGetTransferMarbleAtWorldPoint(
                        model,
                        new Vector2(0f, 1.80f),
                        out string gateId,
                        out Vector2 marblePosition);
                Assert.That(marbleSelected, Is.True);
                Assert.That(gateId, Is.EqualTo("gate_inner_middle"));
                Assert.That(
                    Vector2.Distance(
                        marblePosition,
                        new Vector2(0f, 1.80f)),
                    Is.LessThan(0.001f));

                Assert.That(
                    view.TryGetGateWorldPosition(
                        gateId,
                        out Vector2 portalPosition),
                    Is.True);
                Assert.That(
                    view.TryGetTransferMarbleAtWorldPoint(
                        model,
                        portalPosition,
                        out _,
                        out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
