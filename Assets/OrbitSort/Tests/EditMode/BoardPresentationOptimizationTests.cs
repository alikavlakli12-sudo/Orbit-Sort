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
                Assert.That(gateId, Is.EqualTo("gate_inner_outer"));
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

        [Test]
        public void TwoRingBoardUsesCompactGeometryAndCameraFraming()
        {
            GameObject host = new GameObject("Two Ring Board Test");
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
                Assert.That(
                    content.Find("Dynamic Marbles/inner Ring"),
                    Is.Not.Null);
                Assert.That(
                    content.Find("Dynamic Marbles/outer Ring"),
                    Is.Not.Null);
                Assert.That(
                    content.Find("Dynamic Marbles/middle Ring"),
                    Is.Null);
                Assert.That(
                    content.Find(
                        "Static Board Geometry/gate_inner_outer"),
                    Is.Not.Null);
                Assert.That(
                    content.Find(
                            "Static Board Geometry/Studio Backdrop")
                        .localScale,
                    Is.EqualTo(Vector3.one * 5f));
                Assert.That(
                    view.RecommendedCameraHalfWidth,
                    Is.EqualTo(5.02f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ResolvedExitRemainsAvailableForReceiverAnimation()
        {
            GameObject host = new GameObject("Receiver Animation Test");
            try
            {
                LevelCatalogData catalog =
                    LevelCatalogLoader.LoadFromResources();
                BoardModel model = new BoardModel(catalog.levels[0]);
                OrbitSortBoardView view =
                    host.AddComponent<OrbitSortBoardView>();
                view.Initialize();
                view.Render(model);

                Assert.That(
                    view.GetPendingExitAnimationCount(model),
                    Is.Zero);

                BoardActionResult rotation =
                    model.TryRotateRing("outer", -4);

                Assert.That(rotation.Succeeded, Is.True);
                Assert.That(rotation.ExitedMarbles, Is.GreaterThan(0));
                Assert.That(
                    view.GetPendingExitAnimationCount(model),
                    Is.EqualTo(rotation.ExitedMarbles));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
