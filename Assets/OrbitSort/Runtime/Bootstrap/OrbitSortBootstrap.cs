using OrbitSort.Gameplay;
using UnityEngine;

namespace OrbitSort.Bootstrap
{
    public static class OrbitSortBootstrap
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindFirstObjectByType<OrbitSortGameController>()
                != null)
            {
                return;
            }

            GameObject prototype = new GameObject("Orbit Sort Prototype");
            prototype.AddComponent<OrbitSortGameController>();
        }
    }
}
