using System.Collections;
using NUnit.Framework;
using OrbitSort.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitSort.Tests.PlayMode
{
    public sealed class PrototypeSmokeTests
    {
        [UnityTest]
        public IEnumerator PrototypeBootsWithAPlayableJsonLevel()
        {
            OrbitSortGameController controller =
                Object.FindAnyObjectByType<OrbitSortGameController>();
            if (controller == null)
            {
                GameObject prototype =
                    new GameObject("Orbit Sort Smoke Test");
                controller =
                    prototype.AddComponent<OrbitSortGameController>();
            }

            yield return null;
            yield return null;

            Assert.That(controller.Model, Is.Not.Null);
            Assert.That(controller.Model.Rings, Has.Count.EqualTo(3));
            Assert.That(controller.Model.RemainingMarbles, Is.GreaterThan(0));

            Object.Destroy(controller.gameObject);
            yield return null;
        }
    }
}
