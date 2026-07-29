using OrbitSort.Core;
using OrbitSort.Data;
using OrbitSort.Presentation;
using OrbitSort.UI;
using UnityEngine;

namespace OrbitSort.Gameplay
{
    public sealed class OrbitSortGameController : MonoBehaviour
    {
        private const float DragThresholdPixels = 32f;
        private const float MinimumTransferDirectionDot = 0.68f;
        private const float MinimumRotationAngleDegrees = 3f;
        private const float MinimumPointerRadiusSqr = 0.25f;

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
        private float _pointerLastAngleDegrees;
        private bool _hasPointerAngle;
        private float _dragAngleDegrees;
        private float _maximumDragDistanceSqr;
        private bool _isAnimatingRing;
        private bool _isAnimatingTransfer;
        private string _selectedRingId;
        private string _selectedGateId;
        private Vector2 _selectedMarbleWorldPosition;
        private float _lastCameraAspect = -1f;

        public BoardModel Model => _model;
        public int LevelIndex => _levelIndex;

        private void Awake()
        {
            OrbitSortGameController[] controllers =
                FindObjectsByType<OrbitSortGameController>();
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

            OrbitSortLightingRig.Configure(_camera, transform);

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
                return "Swipe a ring to rotate. Swipe an aligned marble "
                       + "toward its portal.";
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
            _hasPointerAngle =
                _pointerStartWorld.sqrMagnitude > MinimumPointerRadiusSqr;
            if (_hasPointerAngle)
            {
                _pointerLastAngleDegrees = Mathf.Atan2(
                    _pointerStartWorld.y,
                    _pointerStartWorld.x) * Mathf.Rad2Deg;
            }

            _dragAngleDegrees = 0f;
            _maximumDragDistanceSqr = 0f;
            _pointerBlockedByHud = _hud.IsPointerOverUi(screenPosition);
            _selectedRingId = null;
            _selectedGateId = null;
            _selectedMarbleWorldPosition = Vector2.zero;

            if (_pointerBlockedByHud)
            {
                return;
            }

            if (_boardView.TryGetTransferMarbleAtWorldPoint(
                    _model,
                    _pointerStartWorld,
                    out string gateId,
                    out Vector2 marbleWorldPosition))
            {
                _selectedGateId = gateId;
                _selectedMarbleWorldPosition = marbleWorldPosition;
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
                || _pointerBlockedByHud)
            {
                return;
            }

            float dragDistanceSqr =
                (screenPosition - _pointerStartScreen).sqrMagnitude;
            _maximumDragDistanceSqr = Mathf.Max(
                _maximumDragDistanceSqr,
                dragDistanceSqr);

            if (_selectedRingId == null)
            {
                return;
            }

            Vector2 currentWorld = ScreenToBoardWorld(screenPosition);
            if (currentWorld.sqrMagnitude > MinimumPointerRadiusSqr)
            {
                float currentAngle = Mathf.Atan2(
                    currentWorld.y,
                    currentWorld.x) * Mathf.Rad2Deg;

                if (_hasPointerAngle)
                {
                    _dragAngleDegrees += Mathf.DeltaAngle(
                        _pointerLastAngleDegrees,
                        currentAngle);
                    _boardView.PreviewRingRotation(
                        _selectedRingId,
                        _dragAngleDegrees);
                }

                _pointerLastAngleDegrees = currentAngle;
                _hasPointerAngle = true;
            }
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
            float dragDistanceSqr = Mathf.Max(
                _maximumDragDistanceSqr,
                (screenPosition - _pointerStartScreen).sqrMagnitude);
            if (_selectedGateId != null)
            {
                string gateId = _selectedGateId;
                Vector2 swipe = ScreenToBoardWorld(screenPosition)
                                - _pointerStartWorld;
                bool isLongEnough =
                    dragDistanceSqr
                    >= DragThresholdPixels * DragThresholdPixels;
                bool pointsTowardPortal =
                    _boardView.TryGetGateWorldPosition(
                        gateId,
                        out Vector2 portalWorldPosition)
                    && Vector2.Dot(
                        swipe.normalized,
                        (portalWorldPosition
                         - _selectedMarbleWorldPosition).normalized)
                    >= MinimumTransferDirectionDot;
                ClearPointerState();

                if (isLongEnough && pointsTowardPortal)
                {
                    AnimateGateTransfer(gateId);
                }
                else
                {
                    RefreshHud(
                        BoardActionResult.Failure(
                            "Swipe the aligned marble toward its portal.",
                            _model.Phase));
                }

                return;
            }

            if (_selectedRingId != null
                && dragDistanceSqr
                    >= DragThresholdPixels * DragThresholdPixels
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

        private void AnimateGateTransfer(string gateId)
        {
            BoardActionResult result = _model.TryTransferGate(gateId);
            if (!result.Succeeded)
            {
                RefreshHud(result);
                return;
            }

            _isAnimatingTransfer = true;
            if (_boardView.AnimateGateTransfer(
                    gateId,
                    _model,
                    () =>
                    {
                        _isAnimatingTransfer = false;
                        RefreshHud(result);
                    }))
            {
                return;
            }

            _isAnimatingTransfer = false;
            ApplyResult(result);
        }

        private void ApplyResult(BoardActionResult result)
        {
            if (_model == null)
            {
                return;
            }

            _boardView.SynchronizeModel(_model);
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
            if (Mathf.Abs(aspect - _lastCameraAspect) < 0.0001f)
            {
                return;
            }

            _lastCameraAspect = aspect;
            float size = Mathf.Max(7.2f, 6.5f / aspect);
            if (Mathf.Abs(_camera.orthographicSize - size) > 0.0001f)
            {
                _camera.orthographicSize = size;
            }
        }

        private bool InputIsBusy =>
            _isAnimatingRing
            || _isAnimatingTransfer
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
            _hasPointerAngle = false;
            _dragAngleDegrees = 0f;
            _maximumDragDistanceSqr = 0f;
            _selectedRingId = null;
            _selectedGateId = null;
            _selectedMarbleWorldPosition = Vector2.zero;
        }
    }
}
