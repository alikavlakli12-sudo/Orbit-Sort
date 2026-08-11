using OrbitSort.Core;
using OrbitSort.Gameplay;
using UnityEngine;

namespace OrbitSort.UI
{
    public sealed class OrbitSortHud : MonoBehaviour
    {
        private static readonly Color DeepIndigo =
            new Color(0.10f, 0.13f, 0.32f, 1f);
        private static readonly Color CardWhite =
            new Color(0.97f, 0.97f, 1f, 0.98f);
        private static readonly Color SecondaryButton =
            new Color(0.82f, 0.84f, 0.94f, 1f);
        private static readonly Color DisabledButton =
            new Color(0.80f, 0.81f, 0.87f, 0.72f);
        private static readonly Color Scrim =
            new Color(0.10f, 0.12f, 0.28f, 0.34f);
        private static readonly Color DangerColor =
            new Color(0.88f, 0.20f, 0.29f, 1f);
        private static readonly Color SuccessColor =
            new Color(0.18f, 0.68f, 0.47f, 1f);

        private OrbitSortGameController _controller;
        private BoardModel _model;
        private string _fatalMessage;
        private int _levelIndex;
        private int _levelCount;
        private bool _settingsOpen;
        private Texture2D _whiteTexture;
        private Texture2D _roundedTexture;
        private Texture2D _levelIndicatorTexture;
        private Texture2D _settingsButtonTexture;
        private GUIStyle _roundedStyle;
        private GUIStyle _overlayTitleStyle;
        private GUIStyle _overlayBodyStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _secondaryButtonStyle;
        private GUIStyle _transparentButtonStyle;
        private Rect _levelRect;
        private Rect _settingsRect;
        private Rect _overlayRect;

        public void Initialize(OrbitSortGameController controller)
        {
            _controller = controller;
            _whiteTexture = CreateSolidTexture(
                "Orbit Sort HUD Pixel",
                Color.white);
            _roundedTexture = CreateRoundedRectangleTexture(
                "Orbit Sort Rounded Panel",
                64,
                64,
                18f);
            _levelIndicatorTexture = LoadUiTexture("UI/LevelIndicator");
            _settingsButtonTexture = LoadUiTexture("UI/SettingsButton");
        }

        public void Refresh(
            BoardModel model,
            int levelIndex,
            int levelCount)
        {
            _model = model;
            _levelIndex = levelIndex;
            _levelCount = levelCount;
            _fatalMessage = null;
            _settingsOpen = false;
        }

        public void ShowFatalError(string message)
        {
            _fatalMessage = message;
            _settingsOpen = false;
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            RecalculateLayout();
            Vector2 guiPoint = new Vector2(
                screenPosition.x,
                Screen.height - screenPosition.y);
            return _levelRect.Contains(guiPoint)
                   || _settingsRect.Contains(guiPoint)
                   || _settingsOpen
                   || (_model != null
                       && _model.Phase != BoardPhase.Playing)
                   || !string.IsNullOrWhiteSpace(_fatalMessage);
        }

        private void OnGUI()
        {
            EnsureStyles();
            RecalculateLayout();

            DrawTopControls();

            if (!string.IsNullOrWhiteSpace(_fatalMessage))
            {
                DrawFatalOverlay();
            }
            else if (_settingsOpen)
            {
                DrawSettingsOverlay();
            }
            else if (_model != null
                     && _model.Phase != BoardPhase.Playing)
            {
                DrawResultOverlay();
            }
        }

        private void DrawTopControls()
        {
            GUI.DrawTexture(
                _levelRect,
                _levelIndicatorTexture,
                ScaleMode.ScaleToFit,
                true);

            GUI.DrawTexture(
                _settingsRect,
                _settingsButtonTexture,
                ScaleMode.ScaleToFit,
                true);
            if (GUI.Button(
                    _settingsRect,
                    GUIContent.none,
                    _transparentButtonStyle))
            {
                _settingsOpen = !_settingsOpen;
            }
        }

        private void DrawSettingsOverlay()
        {
            DrawScreenScrim();
            Rect card = CenteredCard(420f);
            DrawRoundedPanel(card, CardWhite);
            DrawAccent(new Rect(
                card.center.x - 31f,
                card.y + 24f,
                62f,
                5f),
                DeepIndigo);

            GUI.Label(
                new Rect(
                    card.x + 28f,
                    card.y + 42f,
                    card.width - 56f,
                    60f),
                "SETTINGS",
                _overlayTitleStyle);

            string levelName = _model == null
                ? ""
                : _model.DisplayName.ToUpperInvariant();
            GUI.Label(
                new Rect(
                    card.x + 34f,
                    card.y + 102f,
                    card.width - 68f,
                    54f),
                $"{levelName}  ·  {_levelIndex + 1} OF {_levelCount}",
                _overlayBodyStyle);

            Rect resume = new Rect(
                card.x + 32f,
                card.yMax - 204f,
                card.width - 64f,
                52f);
            Rect undo = new Rect(
                resume.x,
                resume.yMax + 14f,
                resume.width,
                resume.height);
            Rect restart = new Rect(
                undo.x,
                undo.yMax + 14f,
                undo.width,
                undo.height);

            if (DrawButton(resume, "RESUME", true, true))
            {
                _settingsOpen = false;
            }

            if (DrawButton(
                    undo,
                    "UNDO LAST MOVE",
                    _model != null && _model.CanUndo,
                    false))
            {
                _settingsOpen = false;
                _controller.Undo();
            }

            if (DrawButton(
                    restart,
                    "RESTART LEVEL",
                    _model != null,
                    false))
            {
                _settingsOpen = false;
                _controller.RestartLevel();
            }
        }

        private void DrawResultOverlay()
        {
            DrawScreenScrim();
            Rect card = CenteredCard(390f);
            DrawRoundedPanel(card, CardWhite);

            bool won = _model.Phase == BoardPhase.Won;
            Color accent = won ? SuccessColor : DangerColor;
            DrawAccent(
                new Rect(
                    card.center.x - 31f,
                    card.y + 24f,
                    62f,
                    5f),
                accent);

            string title = won ? "LEVEL COMPLETE" : "RINGS JAMMED";
            string body = won
                ? "Every marble reached its matching receiver."
                : "No productive route remains. Undo the last action "
                  + "or restart the level.";
            GUI.Label(
                new Rect(
                    card.x + 24f,
                    card.y + 46f,
                    card.width - 48f,
                    64f),
                title,
                _overlayTitleStyle);
            GUI.Label(
                new Rect(
                    card.x + 38f,
                    card.y + 116f,
                    card.width - 76f,
                    82f),
                body,
                _overlayBodyStyle);

            float buttonWidth = (card.width - 78f) * 0.5f;
            Rect left = new Rect(
                card.x + 24f,
                card.yMax - 82f,
                buttonWidth,
                56f);
            Rect right = new Rect(
                left.xMax + 30f,
                left.y,
                buttonWidth,
                left.height);

            if (won)
            {
                if (DrawButton(left, "RETRY", true, false))
                {
                    _controller.RestartLevel();
                }

                bool hasNext = _levelIndex + 1 < _levelCount;
                if (DrawButton(
                        right,
                        hasNext ? "NEXT LEVEL" : "ALL DONE",
                        hasNext,
                        true))
                {
                    _controller.NextLevel();
                }
            }
            else
            {
                if (DrawButton(
                        left,
                        "UNDO",
                        _model.CanUndo,
                        false))
                {
                    _controller.Undo();
                }

                if (DrawButton(right, "RETRY", true, true))
                {
                    _controller.RestartLevel();
                }
            }
        }

        private void DrawFatalOverlay()
        {
            DrawScreenScrim();
            Rect card = CenteredCard(340f);
            DrawRoundedPanel(card, CardWhite);
            DrawAccent(
                new Rect(
                    card.center.x - 31f,
                    card.y + 24f,
                    62f,
                    5f),
                DangerColor);
            GUI.Label(
                new Rect(
                    card.x + 24f,
                    card.y + 48f,
                    card.width - 48f,
                    64f),
                "LEVEL DATA ERROR",
                _overlayTitleStyle);
            GUI.Label(
                new Rect(
                    card.x + 34f,
                    card.y + 116f,
                    card.width - 68f,
                    150f),
                _fatalMessage,
                _overlayBodyStyle);
        }

        private bool DrawButton(
            Rect rect,
            string text,
            bool enabled,
            bool primary)
        {
            Color color = !enabled
                ? DisabledButton
                : primary
                    ? DeepIndigo
                    : SecondaryButton;
            DrawRoundedPanel(rect, color);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(
                rect,
                text,
                primary ? _primaryButtonStyle : _secondaryButtonStyle);
            GUI.enabled = previousEnabled;
            return clicked;
        }

        private void DrawScreenScrim()
        {
            Color previous = GUI.color;
            GUI.color = Scrim;
            GUI.DrawTexture(_overlayRect, _whiteTexture);
            GUI.color = previous;
        }

        private void DrawRoundedPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, _roundedStyle);
            GUI.color = previous;
        }

        private void DrawAccent(Rect rect, Color color)
        {
            DrawRoundedPanel(rect, color);
        }

        private Rect CenteredCard(float preferredHeight)
        {
            float width = Mathf.Min(Screen.width - 48f, 520f);
            float height = Mathf.Min(
                preferredHeight,
                Screen.height - 80f);
            return new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        private void RecalculateLayout()
        {
            Rect safe = Screen.safeArea;
            float safeTop = Screen.height - safe.yMax;
            float top = safeTop + Mathf.Max(18f, Screen.height * 0.012f);
            float controlSize = Mathf.Clamp(
                Screen.width * 0.105f,
                62f,
                78f);
            float levelWidth = Mathf.Clamp(
                Screen.width * 0.28f,
                172f,
                228f);
            float edge = Mathf.Max(18f, Screen.width * 0.052f);

            _levelRect = new Rect(
                (Screen.width - levelWidth) * 0.5f,
                top,
                levelWidth,
                controlSize);
            _settingsRect = new Rect(
                safe.xMax - edge - controlSize,
                top,
                controlSize,
                controlSize);
            _overlayRect = new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height);
        }

        private void EnsureStyles()
        {
            if (_roundedStyle != null)
            {
                return;
            }

            _roundedStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _roundedTexture },
                border = new RectOffset(22, 22, 22, 22)
            };
            _overlayTitleStyle = CreateStyle(
                34,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                DeepIndigo);
            _overlayBodyStyle = CreateStyle(
                20,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Color(0.28f, 0.30f, 0.46f, 1f));
            _overlayBodyStyle.wordWrap = true;
            _primaryButtonStyle = CreateButtonStyle(Color.white);
            _secondaryButtonStyle = CreateButtonStyle(DeepIndigo);
            _transparentButtonStyle = new GUIStyle();
        }

        private static GUIStyle CreateButtonStyle(Color textColor)
        {
            GUIStyle style = CreateStyle(
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                textColor);
            ClearButtonBackground(style);
            return style;
        }

        private static void ClearButtonBackground(GUIStyle style)
        {
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
        }

        private static GUIStyle CreateStyle(
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color textColor)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = textColor }
            };
        }

        private static Texture2D CreateSolidTexture(
            string textureName,
            Color color)
        {
            var texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateRoundedRectangleTexture(
            string textureName,
            int width,
            int height,
            float radius)
        {
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float straightX = centerX - radius;
            float straightY = centerY - radius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(
                        Mathf.Abs(x - centerX) - straightX,
                        0f);
                    float dy = Mathf.Max(
                        Mathf.Abs(y - centerY) - straightY,
                        0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.SmoothStep(
                        radius - 1.25f,
                        radius + 0.75f,
                        distance);
                    pixels[y * width + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D LoadUiTexture(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                throw new System.InvalidOperationException(
                    $"Missing HUD texture at Resources/{resourcePath}.");
            }

            return texture;
        }

        private void OnDestroy()
        {
            ReleaseTexture(_whiteTexture);
            ReleaseTexture(_roundedTexture);
        }

        private static void ReleaseTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }
    }
}
