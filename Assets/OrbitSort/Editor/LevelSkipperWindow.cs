using OrbitSort.Data;
using OrbitSort.Gameplay;
using UnityEditor;
using UnityEngine;

namespace OrbitSort.Editor
{
    public sealed class LevelSkipperWindow : EditorWindow
    {
        private const string MenuPath =
            "Window/Orbit Sort/Level Skipper";

        private int _levelCount;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            LevelSkipperWindow window =
                GetWindow<LevelSkipperWindow>();
            window.titleContent = new GUIContent("Level Skipper");
            window.minSize = new Vector2(260f, 88f);
            window.Show();
        }

        private void OnEnable()
        {
            _levelCount =
                LevelCatalogLoader.LoadFromResources().levels.Length;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Level skipping is available in Play Mode.",
                    MessageType.Info);
                return;
            }

            OrbitSortGameController controller =
                Object.FindAnyObjectByType<OrbitSortGameController>();
            if (controller == null || controller.Model == null)
            {
                EditorGUILayout.HelpBox(
                    "Waiting for the game to load.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                $"LEVEL {controller.LevelIndex + 1}",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int index = 0; index < _levelCount; index++)
                {
                    using (new EditorGUI.DisabledScope(
                               index == controller.LevelIndex))
                    {
                        if (GUILayout.Button(
                                (index + 1).ToString(),
                                GUILayout.Height(30f)))
                        {
                            GoToLevel(controller, index);
                        }
                    }
                }
            }
        }

        private static void GoToLevel(
            OrbitSortGameController controller,
            int targetIndex)
        {
            while (controller.LevelIndex < targetIndex)
            {
                int previousIndex = controller.LevelIndex;
                controller.NextLevel();
                if (controller.LevelIndex == previousIndex)
                {
                    break;
                }
            }

            while (controller.LevelIndex > targetIndex)
            {
                int previousIndex = controller.LevelIndex;
                controller.PreviousLevel();
                if (controller.LevelIndex == previousIndex)
                {
                    break;
                }
            }
        }
    }
}
