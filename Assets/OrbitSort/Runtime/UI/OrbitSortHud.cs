using OrbitSort.Core;
using OrbitSort.Gameplay;
using UnityEngine;

namespace OrbitSort.UI
{
    public sealed class OrbitSortHud : MonoBehaviour
    {
        private static readonly Color PanelColor =
            new Color(0.08f, 0.06f, 0.18f, 0.94f);
        private static readonly Color PanelSoftColor =
            new Color(0.13f, 0.10f, 0.27f, 0.92f);
        private static readonly Color AccentColor =
            new Color(1.00f, 0.69f, 0.10f, 1f);
        private static readonly Color ButtonColor =
            new Color(0.20f, 0.15f, 0.42f, 1f);
        private static readonly Color ButtonDisabledColor =
            new Color(0.13f, 0.11f, 0.21f, 1f);
        private static readonly Color DangerColor =
            new Color(0.92f, 0.18f, 0.26f, 1f);
        private static readonly Color SuccessColor =
            new Color(0.20f, 0.78f, 0.48f, 1f);

        private OrbitSortGameController _controller;
        private BoardModel _model;
        private string _levelLabel = "ORBIT SORT";
        private string _message =
            "Swipe a ring. Tap a green gate to transfer.";
        private string _fatalMessage;
        private int _levelIndex;
        private int _levelCount;
        private Texture2D _whiteTexture;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _overlayTitleStyle;
        private GUIStyle _overlayBodyStyle;
        private Rect _headerRect;
        private Rect _messageRect;
        private Rect _buttonBarRect;
        private Rect _overlayRect;

        public void Initialize(OrbitSortGameController controller)
        {
            _controller = controller;
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Orbit Sort HUD Pixel",
                hideFlags = HideFlags.HideAndDontSave
            };
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        public void Refresh(
            BoardModel model,
            int levelIndex,
            int levelCount,
            string message)
        {
            _model = model;
            _levelIndex = levelIndex;
            _levelCount = levelCount;
            _levelLabel = model == null
                ? "ORBIT SORT"
                : $"LEVEL {levelIndex + 1}  ·  "
                  + model.DisplayName.ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(message))
            {
                _message = message;
            }

            _fatalMessage = null;
        }

        public void ShowFatalError(string message)
        {
            _fatalMessage = message;
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            RecalculateLayout();
            Vector2 guiPoint = new Vector2(
                screenPosition.x,
                Screen.height - screenPosition.y);
            return _headerRect.Contains(guiPoint)
                   || _messageRect.Contains(guiPoint)
                   || _buttonBarRect.Contains(guiPoint)
                   || (_model != null
                       && _model.Phase != BoardPhase.Playing
                       && _overlayRect.Contains(guiPoint))
                   || (!string.IsNullOrWhiteSpace(_fatalMessage)
                       && _overlayRect.Contains(guiPoint));
        }

        private void OnGUI()
        {
            EnsureStyles();
            RecalculateLayout();

            DrawPanel(_headerRect, PanelColor);
            GUI.Label(_headerRect, _levelLabel, _titleStyle);

            if (_model != null)
            {
                Rect remainingRect = new Rect(
                    _headerRect.xMax - 142f,
                    _headerRect.y + 13f,
                    126f,
                    _headerRect.height - 26f);
                DrawPanel(remainingRect, PanelSoftColor);
                GUI.Label(
                    remainingRect,
                    $"{_model.RemainingMarbles} LEFT",
                    _labelStyle);
            }

            DrawPanel(_messageRect, PanelColor);
            GUI.Label(_messageRect, _message, _messageStyle);

            DrawControls();

            if (!string.IsNullOrWhiteSpace(_fatalMessage))
            {
                DrawFatalOverlay();
            }
            else if (_model != null
                     && _model.Phase != BoardPhase.Playing)
            {
                DrawResultOverlay();
            }
        }

        private void DrawControls()
        {
            DrawPanel(_buttonBarRect, PanelColor);
            float spacing = 10f;
            float width =
                (_buttonBarRect.width - spacing * 5f) / 4f;
            float height = _buttonBarRect.height - spacing * 2f;
            float x = _buttonBarRect.x + spacing;
            float y = _buttonBarRect.y + spacing;

            bool canUndo = _model != null && _model.CanUndo;
            if (DrawButton(
                    new Rect(x, y, width, height),
                    "UNDO",
                    canUndo))
            {
                _controller.Undo();
            }

            x += width + spacing;
            if (DrawButton(
                    new Rect(x, y, width, height),
                    "RETRY",
                    _model != null))
            {
                _controller.RestartLevel();
            }

            x += width + spacing;
            if (DrawButton(
                    new Rect(x, y, width, height),
                    "‹ LEVEL",
                    _levelIndex > 0))
            {
                _controller.PreviousLevel();
            }

            x += width + spacing;
            if (DrawButton(
                    new Rect(x, y, width, height),
                    "LEVEL ›",
                    _levelIndex + 1 < _levelCount))
            {
                _controller.NextLevel();
            }
        }

        private void DrawResultOverlay()
        {
            DrawPanel(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.02f, 0.01f, 0.07f, 0.72f));

            Rect card = new Rect(
                Mathf.Max(24f, Screen.width * 0.08f),
                Screen.height * 0.35f,
                Mathf.Min(Screen.width - 48f, 620f),
                Mathf.Clamp(Screen.height * 0.29f, 280f, 430f));
            card.x = (Screen.width - card.width) * 0.5f;
            DrawPanel(
                card,
                _model.Phase == BoardPhase.Won
                    ? new Color(
                        SuccessColor.r,
                        SuccessColor.g,
                        SuccessColor.b,
                        0.98f)
                    : new Color(
                        DangerColor.r,
                        DangerColor.g,
                        DangerColor.b,
                        0.98f));

            string title = _model.Phase == BoardPhase.Won
                ? "LEVEL COMPLETE"
                : "RINGS JAMMED";
            string body = _model.Phase == BoardPhase.Won
                ? "Every marble reached its matching exit."
                : "No rotation or transfer can create progress.\n"
                  + "Undo the last action or retry the level.";
            GUI.Label(
                new Rect(card.x + 24f, card.y + 34f, card.width - 48f, 70f),
                title,
                _overlayTitleStyle);
            GUI.Label(
                new Rect(card.x + 34f, card.y + 105f, card.width - 68f, 92f),
                body,
                _overlayBodyStyle);

            float buttonWidth = (card.width - 78f) * 0.5f;
            Rect left = new Rect(
                card.x + 24f,
                card.yMax - 82f,
                buttonWidth,
                58f);
            Rect right = new Rect(
                left.xMax + 30f,
                left.y,
                buttonWidth,
                left.height);

            if (_model.Phase == BoardPhase.Won)
            {
                if (DrawButton(left, "RETRY", true))
                {
                    _controller.RestartLevel();
                }

                bool hasNext = _levelIndex + 1 < _levelCount;
                if (DrawButton(
                        right,
                        hasNext ? "NEXT LEVEL" : "LEVELS DONE",
                        hasNext))
                {
                    _controller.NextLevel();
                }
            }
            else
            {
                if (DrawButton(left, "UNDO", _model.CanUndo))
                {
                    _controller.Undo();
                }

                if (DrawButton(right, "RETRY", true))
                {
                    _controller.RestartLevel();
                }
            }
        }

        private void DrawFatalOverlay()
        {
            DrawPanel(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.02f, 0.01f, 0.07f, 0.92f));
            Rect card = new Rect(
                30f,
                Screen.height * 0.35f,
                Screen.width - 60f,
                300f);
            DrawPanel(card, DangerColor);
            GUI.Label(
                new Rect(card.x + 20f, card.y + 28f, card.width - 40f, 70f),
                "LEVEL DATA ERROR",
                _overlayTitleStyle);
            GUI.Label(
                new Rect(card.x + 30f, card.y + 100f, card.width - 60f, 160f),
                _fatalMessage,
                _overlayBodyStyle);
        }

        private bool DrawButton(Rect rect, string text, bool enabled)
        {
            Color color = GUI.color;
            GUI.color = enabled ? ButtonColor : ButtonDisabledColor;
            GUI.DrawTexture(rect, _whiteTexture);
            GUI.color = color;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, text, _buttonStyle);
            GUI.enabled = previousEnabled;
            return clicked;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTexture);
            GUI.color = previous;
        }

        private void RecalculateLayout()
        {
            Rect safe = Screen.safeArea;
            float safeTop = Screen.height - safe.yMax;
            float safeBottom = safe.y;
            float horizontal = Mathf.Max(18f, Screen.width * 0.035f);

            _headerRect = new Rect(
                horizontal,
                safeTop + 18f,
                Screen.width - horizontal * 2f,
                Mathf.Clamp(Screen.height * 0.065f, 82f, 112f));
            _messageRect = new Rect(
                horizontal,
                Screen.height - safeBottom
                - Mathf.Clamp(Screen.height * 0.115f, 145f, 205f),
                Screen.width - horizontal * 2f,
                Mathf.Clamp(Screen.height * 0.045f, 62f, 82f));
            _buttonBarRect = new Rect(
                horizontal,
                Screen.height - safeBottom
                - Mathf.Clamp(Screen.height * 0.068f, 86f, 122f),
                Screen.width - horizontal * 2f,
                Mathf.Clamp(Screen.height * 0.058f, 76f, 102f));
            _overlayRect = new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = CreateStyle(
                27,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            _titleStyle.padding = new RectOffset(26, 160, 0, 0);

            _labelStyle = CreateStyle(
                20,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _messageStyle = CreateStyle(
                21,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _messageStyle.padding = new RectOffset(16, 16, 5, 5);
            _messageStyle.wordWrap = true;

            _buttonStyle = CreateStyle(
                19,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _buttonStyle.normal.background = null;
            _buttonStyle.hover.background = null;
            _buttonStyle.active.background = null;
            _buttonStyle.focused.background = null;

            _overlayTitleStyle = CreateStyle(
                38,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _overlayBodyStyle = CreateStyle(
                22,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            _overlayBodyStyle.wordWrap = true;
        }

        private static GUIStyle CreateStyle(
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = Color.white }
            };
        }

        private void OnDestroy()
        {
            if (_whiteTexture != null)
            {
                Destroy(_whiteTexture);
            }
        }
    }
}
