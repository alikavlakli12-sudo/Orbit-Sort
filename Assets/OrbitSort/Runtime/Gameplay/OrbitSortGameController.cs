using OrbitSort.Core;
using OrbitSort.Data;
using OrbitSort.Presentation;
using OrbitSort.UI;
using UnityEngine;

namespace OrbitSort.Gameplay
{
    public sealed class OrbitSortGameController : MonoBehaviour
    {
        private const float TapThresholdPixels = 26f;
        private const float DragThresholdPixels = 32f;
        private const float MinimumRotationAngleDegrees = 3f;

        private LevelCatalogData _catalog;
        private BoardModel _model;
        private OrbitSortBoardView _boardView;
        private OrbitSortHud _hud;
        private Camera _camera;
        private int _levelIndex;
        private bool _pointerDown;
        private bool _pointerBlockedByHud;
        private Vector2 _pointerStartScreen;
        private Vector2 _pointerStartWorld;
        private Vector2 _pointerLastWorld;
        private float _dragAngleDegrees;
        private float _maximumDragDistancePixels;
        private bool _isAnimatingRing;
        private string _selectedRingId;
        private string _selectedGateId;

        public BoardModel Model => _model;
        public int LevelIndex => _levelIndex;

        private void Awake()
        {
            OrbitSortGameController[] controllers =
                FindObjectsByType<OrbitSortGameController>(
                    FindObjectsSortMode.None);
            if (controllers.Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            ConfigureScene();
        }

        private void Start()
        {
            try
            {
                _catalog = LevelCatalogLoader.LoadFromResources();
                LoadLevel(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                _hud.ShowFatalError(exception.Message);
            }
        }

        private void ConfigureScene()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("Orbit Sort Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(transform, false);
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0f, 0f, -20f);
            _camera.transform.rotation = Quaternion.identity;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.055f, 0.035f, 0.14f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            UpdateCameraSize();

            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.78f, 0.92f);

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject =
                    new GameObject("Orbit Sort Key Light");
                Light keyLight = lightObject.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.15f;
                keyLight.color = new Color(1f, 0.94f, 0.84f);
                lightObject.transform.rotation =
                    Quaternion.Euler(25f, -30f, 0f);
                lightObject.transform.SetParent(transform, true);
            }

            GameObject boardObject = new GameObject("Orbit Sort Board");
            boardObject.transform.SetParent(transform, false);
            _boardView =
                boardObject.AddComponent<OrbitSortBoardView>();
            _boardView.Initialize();

            _hud = gameObject.AddComponent<OrbitSortHud>();
            _hud.Initialize(this);
        }

        private void Update()
        {
            UpdateCameraSize();
            HandleKeyboardShortcuts();

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        BeginPointer(touch.position);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        UpdatePointer(touch.position);
                        break;
                    case TouchPhase.Ended:
                        EndPointer(touch.position);
                        break;
                    case TouchPhase.Canceled:
                        CancelPointerAndRestoreRing();
                        break;
                }

                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                BeginPointer(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                UpdatePointer(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0))
            {
                EndPointer(Input.mousePosition);
            }
        }

        public void Undo()
        {
            if (_model == null || InputIsBusy)
            {
                return;
            }

            ApplyResult(_model.Undo());
        }

        public void RestartLevel()
        {
            if (_model == null || InputIsBusy)
            {
                return;
            }

            ApplyResult(_model.Restart());
        }

        public void NextLevel()
        {
            if (InputIsBusy
                || _catalog == null
                || _levelIndex + 1 >= _catalog.levels.Length)
            {
                return;
            }

            LoadLevel(_levelIndex + 1);
        }

        public void PreviousLevel()
        {
            if (InputIsBusy || _catalog == null || _levelIndex <= 0)
            {
                return;
            }

            LoadLevel(_levelIndex - 1);
        }

        private void LoadLevel(int index)
        {
            _levelIndex = index;
            _model = new BoardModel(_catalog.levels[index]);
            _boardView.Render(_model);
            _hud.Refresh(
                _model,
                _levelIndex,
                _catalog.levels.Length,
                InitialInstruction(index));
            ClearPointerState();
        }

        private string InitialInstruction(int index)
        {
            if (index == 0)
            {
                return "Swipe a ring to rotate. Tap a green gate to transfer.";
            }

            return "Warning: filling the final outer gap can jam the board.";
        }

        private void BeginPointer(Vector2 screenPosition)
        {
            if (InputIsBusy
                || _model == null
                || _model.Phase != BoardPhase.Playing)
            {
                return;
            }

            _pointerDown = true;
            _pointerStartScreen = screenPosition;
            _pointerStartWorld = ScreenToBoardWorld(screenPosition);
            _pointerLastWorld = _pointerStartWorld;
            _dragAngleDegrees = 0f;
            _maximumDragDistancePixels = 0f;
            _pointerBlockedByHud = _hud.IsPointerOverUi(screenPosition);
            _selectedRingId = null;
            _selectedGateId = null;

            if (_pointerBlockedByHud)
            {
                return;
            }

            if (_boardView.TryGetGateAtWorldPoint(
                    _pointerStartWorld,
                    out string gateId))
            {
                _selectedGateId = gateId;
                return;
            }

            _boardView.TryGetRingAtWorldPoint(
                _pointerStartWorld,
                out _selectedRingId);
            if (_selectedRingId != null
                && !_boardView.BeginRingDrag(_selectedRingId))
            {
                _selectedRingId = null;
            }
        }

        private void UpdatePointer(Vector2 screenPosition)
        {
            if (!_pointerDown
                || _pointerBlockedByHud
                || _selectedRingId == null)
            {
                return;
            }

            _maximumDragDistancePixels = Mathf.Max(
                _maximumDragDistancePixels,
                Vector2.Distance(_pointerStartScreen, screenPosition));

            Vector2 currentWorld = ScreenToBoardWorld(screenPosition);
            if (_pointerLastWorld.sqrMagnitude > 0.25f
                && currentWorld.sqrMagnitude > 0.25f)
            {
                float previousAngle = Mathf.Atan2(
                    _pointerLastWorld.y,
                    _pointerLastWorld.x) * Mathf.Rad2Deg;
                float currentAngle = Mathf.Atan2(
                    currentWorld.y,
                    currentWorld.x) * Mathf.Rad2Deg;
                _dragAngleDegrees += Mathf.DeltaAngle(
                    previousAngle,
                    currentAngle);
                _boardView.PreviewRingRotation(
                    _selectedRingId,
                    _dragAngleDegrees);
            }

            _pointerLastWorld = currentWorld;
        }

        private void EndPointer(Vector2 screenPosition)
        {
            if (!_pointerDown)
            {
                return;
            }

            UpdatePointer(screenPosition);
            if (_pointerBlockedByHud
                || _model == null
                || _model.Phase != BoardPhase.Playing)
            {
                CancelPointerAndRestoreRing();
                return;
            }

            _pointerDown = false;
            float dragDistance =
                Mathf.Max(
                    _maximumDragDistancePixels,
                    Vector2.Distance(_pointerStartScreen, screenPosition));
            if (_selectedGateId != null
                && dragDistance <= TapThresholdPixels)
            {
                ApplyResult(_model.TryTransferGate(_selectedGateId));
                ClearPointerState();
                return;
            }

            if (_selectedRingId != null
                && dragDistance >= DragThresholdPixels
                && Mathf.Abs(_dragAngleDegrees)
                    >= MinimumRotationAngleDegrees)
            {
                float stepAngle = _boardView.GetRingStepAngle(
                    _selectedRingId,
                    _model);
                int steps = Mathf.RoundToInt(
                    -_dragAngleDegrees / stepAngle);
                if (steps == 0)
                {
                    steps = _dragAngleDegrees < 0f ? 1 : -1;
                }

                string ringId = _selectedRingId;
                BoardActionResult result =
                    _model.TryRotateRing(ringId, steps);
                ClearPointerState();
                AnimateRingResult(ringId, result);
                return;
            }

            if (_selectedRingId != null)
            {
                string ringId = _selectedRingId;
                ClearPointerState();
                AnimateRingResult(ringId, null);
                return;
            }

            ClearPointerState();
        }

        private void ApplyResult(BoardActionResult result)
        {
            if (_model == null)
            {
                return;
            }

            _boardView.Render(_model);
            RefreshHud(result);
        }

        private void AnimateRingResult(
            string ringId,
            BoardActionResult result)
        {
            _isAnimatingRing = true;
            _boardView.AnimateRingToModel(
                ringId,
                _model,
                () =>
                {
                    _isAnimatingRing = false;
                    if (result != null)
                    {
                        RefreshHud(result);
                    }
                });
        }

        private void RefreshHud(BoardActionResult result)
        {
            string message = result.Message;
            if (_model.Phase == BoardPhase.Deadlocked)
            {
                message = "No productive route remains. Undo or retry.";
            }
            else if (_model.Phase == BoardPhase.Won)
            {
                message = "All marbles sorted.";
            }

            _hud.Refresh(
                _model,
                _levelIndex,
                _catalog.levels.Length,
                message);
        }

        private Vector2 ScreenToBoardWorld(Vector2 screenPosition)
        {
            Vector3 point = _camera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -_camera.transform.position.z));
            return new Vector2(point.x, point.y);
        }

        private void HandleKeyboardShortcuts()
        {
            if (InputIsBusy)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                Undo();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RestartLevel();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextLevel();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PreviousLevel();
            }
        }

        private void UpdateCameraSize()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            float aspect = Mathf.Max(0.2f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(7.2f, 6.5f / aspect);
        }

        private bool InputIsBusy =>
            _isAnimatingRing
            || (_pointerDown && !_pointerBlockedByHud);

        private void CancelPointerAndRestoreRing()
        {
            string ringId = _selectedRingId;
            ClearPointerState();
            if (ringId != null && _model != null)
            {
                AnimateRingResult(ringId, null);
            }
        }

        private void ClearPointerState()
        {
            _pointerDown = false;
            _pointerBlockedByHud = false;
            _dragAngleDegrees = 0f;
            _maximumDragDistancePixels = 0f;
            _selectedRingId = null;
            _selectedGateId = null;
        }
    }
}
