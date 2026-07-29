using OrbitSort.Gameplay;
using UnityEngine;

namespace OrbitSort.Bootstrap
{
    public static class OrbitSortBootstrap
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void LockUprightPortraitOrientation()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindAnyObjectByType<OrbitSortGameController>()
                != null)
            {
                return;
            }

            GameObject prototype = new GameObject("Orbit Sort Prototype");
            prototype.AddComponent<OrbitSortGameController>();
        }
    }
}
