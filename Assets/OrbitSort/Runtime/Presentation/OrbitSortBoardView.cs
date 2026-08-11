using System;
using System.Collections;
using System.Collections.Generic;
using OrbitSort.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitSort.Presentation
{
    public sealed class OrbitSortBoardView : MonoBehaviour
    {
        private const float TrackHalfWidth = 0.48f;
        private const float MinimumSnapDuration = 0.10f;
        private const float MaximumSnapDuration = 0.24f;
        private const float DragFollowSharpness = 90f;
        private const float TransferMarbleHitRadius = 0.62f;
        private const float GravityEntryDuration = 0.28f;
        private const float GravityExitDuration = 0.28f;
        private const float PortalScale = 0.08f;
        private const string ModelResourceRoot = "Models/";
        private const float ReceiverTrackOffset = 0.91f;
        private const float CameraEdgePadding = 0.83f;
        private const float BackdropScale = 5f;

        private static readonly float[] ApprovedRingRadii =
        {
            1.80f,
            3.28f,
            4.76f
        };

        private static readonly Quaternion BlenderBoardRotation =
            Quaternion.AngleAxis(180f, Vector3.up)
            * Quaternion.AngleAxis(90f, Vector3.right);

        private readonly Dictionary<string, float> _ringRadii =
            new Dictionary<string, float>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Transform> _ringRoots =
            new Dictionary<string, Transform>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<
                string,
                Dictionary<int, MarbleVisual>>
            _marbleVisuals =
                new Dictionary<
                    string,
                    Dictionary<int, MarbleVisual>>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> _gatePositions =
            new Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> _exitPositions =
            new Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _marbleRemovalBuffer =
            new List<int>();
        private readonly List<UnityEngine.Object> _generatedAssets =
            new List<UnityEngine.Object>();
        private readonly Dictionary<MarbleColor, Material> _marbleMaterials =
            new Dictionary<MarbleColor, Material>();

        private Transform _contentRoot;
        private Transform _staticRoot;
        private Transform _dynamicRoot;
        private GameObject[] _ringModels;
        private GameObject _portalModel;
        private GameObject _receiverModel;
        private GameObject _centerModel;
        private GameObject _marbleModel;
        private GameObject _backdropModel;
        private ReceiverShredEffect _receiverShredEffect;
        private Material _trackMaterial;
        private Material _railMaterial;
        private Material _portalMaterial;
        private Material _centerMaterial;
        private Material _exitInteriorMaterial;
        private Material _arrowMaterial;
        private Material _backdropMaterial;
        private readonly Dictionary<MarbleColor, Material>
            _receiverMaterials =
                new Dictionary<MarbleColor, Material>();
        private Coroutine _ringAnimation;
        private Coroutine _transferAnimation;
        private Coroutine _exitAnimation;
        private string _previewRingId;
        private float _previewBaseAngle;
        private float _previewTargetAngle;
        private float _previewVisualAngle;
        private float _receiverRadius;

        public float RecommendedCameraHalfWidth { get; private set; } = 6.5f;

        public void Initialize()
        {
            _receiverShredEffect =
                gameObject.GetComponent<ReceiverShredEffect>();
            if (_receiverShredEffect == null)
            {
                _receiverShredEffect =
                    gameObject.AddComponent<ReceiverShredEffect>();
            }

            _ringModels = new[]
            {
                LoadModel("RingInner"),
                LoadModel("RingMiddle"),
                LoadModel("RingOuter")
            };
            _portalModel = LoadModel("Portal");
            _receiverModel = LoadModel("Receiver");
            _centerModel = LoadModel("CenterHub");
            _marbleModel = LoadModel("Marble");
            _backdropModel = LoadModel("Backdrop");

            _trackMaterial = CreateMaterial(
                "Track",
                new Color(0.70f, 0.68f, 0.78f),
                0.00f,
                0.66f);
            _railMaterial = CreateMaterial(
                "Rail",
                new Color(0.86f, 0.82f, 0.80f),
                0.04f,
                0.74f);
            _portalMaterial = CreateMaterial(
                "Portal",
                new Color(0.88f, 0.58f, 0.020f),
                0.18f,
                0.76f);
            _centerMaterial = CreateMaterial(
                "Center",
                new Color(0.018f, 0.025f, 0.09f),
                0.00f,
                0.52f);
            _exitInteriorMaterial = CreateMaterial(
                "Exit Interior",
                new Color(0.018f, 0.025f, 0.09f),
                0.00f,
                0.52f);
            _arrowMaterial = CreateMaterial(
                "Gate Arrow",
                new Color(0.92f, 0.92f, 0.96f),
                0.00f,
                0.68f);
            _backdropMaterial = CreateBackdropMaterial();

            _marbleMaterials[MarbleColor.Blue] =
                CreateMaterial(
                    "Blue Marble",
                    new Color(0.060f, 0.52f, 0.90f),
                    0.05f,
                    0.74f,
                    true);
            _marbleMaterials[MarbleColor.Red] =
                CreateMaterial(
                    "Red Marble",
                    new Color(0.95f, 0.060f, 0.030f),
                    0.03f,
                    0.74f,
                    true);
            _marbleMaterials[MarbleColor.Yellow] =
                CreateMaterial(
                    "Yellow Marble",
                    new Color(0.90f, 0.68f, 0.030f),
                    0.03f,
                    0.72f,
                    true);

            _receiverMaterials[MarbleColor.Blue] =
                CreateMaterial(
                    "Blue Receiver",
                    new Color(0.050f, 0.48f, 0.90f),
                    0.10f,
                    0.76f);
            _receiverMaterials[MarbleColor.Red] =
                CreateMaterial(
                    "Red Receiver",
                    new Color(0.95f, 0.050f, 0.025f),
                    0.08f,
                    0.76f);
            _receiverMaterials[MarbleColor.Yellow] =
                CreateMaterial(
                    "Yellow Receiver",
                    new Color(0.90f, 0.64f, 0.025f),
                    0.10f,
                    0.76f);
        }

        public void Render(BoardModel model)
        {
            StopRingAnimation();
            StopTransferAnimation();
            StopExitAnimation();
            ClearContent();
            _ringRadii.Clear();
            _ringRoots.Clear();
            _marbleVisuals.Clear();
            _gatePositions.Clear();
            _exitPositions.Clear();

            GameObject content = new GameObject("Board Content");
            content.transform.SetParent(transform, false);
            _contentRoot = content.transform;

            GameObject staticGeometry =
                new GameObject("Static Board Geometry");
            staticGeometry.transform.SetParent(_contentRoot, false);
            _staticRoot = staticGeometry.transform;

            GameObject dynamicMarbles =
                new GameObject("Dynamic Marbles");
            dynamicMarbles.transform.SetParent(_contentRoot, false);
            _dynamicRoot = dynamicMarbles.transform;
            _receiverShredEffect.Configure(
                _dynamicRoot,
                model.Exits.Count,
                _marbleMaterials);

            CreateBackdrop();

            if (model.Rings.Count < 2
                || model.Rings.Count > ApprovedRingRadii.Length)
            {
                throw new InvalidOperationException(
                    "Orbit Sort board art supports two or three rings.");
            }

            _receiverRadius =
                ApprovedRingRadii[model.Rings.Count - 1]
                + ReceiverTrackOffset;
            RecommendedCameraHalfWidth =
                _receiverRadius + CameraEdgePadding;

            for (int index = 0; index < model.Rings.Count; index++)
            {
                RingState ring = model.Rings[index];
                float radius = ApprovedRingRadii[index];
                _ringRadii[ring.Id] = radius;
                Transform ringRoot = CreateRingRoot(ring);
                _ringRoots[ring.Id] = ringRoot;
                CreateRing(index, ring.Id);
                CreateMarbles(ring, radius, ringRoot);
            }

            CreateCenter();

            foreach (GateState gate in model.Gates)
            {
                CreateGate(model, gate);
            }

            foreach (ExitState exit in model.Exits)
            {
                CreateExit(model, exit);
            }

            if (Application.isPlaying)
            {
                StaticBatchingUtility.Combine(
                    _staticRoot.gameObject);
            }
        }

        public void SynchronizeModel(BoardModel model)
        {
            if (!CanSynchronize(model))
            {
                Render(model);
                return;
            }

            foreach (RingState ring in model.Rings)
            {
                Transform ringRoot = _ringRoots[ring.Id];
                SetLocalAngle(
                    ringRoot,
                    -ring.RotationOffset * (360f / ring.Capacity));
                SynchronizeMarbles(ring, ringRoot);
            }
        }

        public bool TryGetRingAtWorldPoint(
            Vector2 worldPoint,
            out string ringId)
        {
            float radius = worldPoint.magnitude;
            float bestDistance = float.MaxValue;
            ringId = null;

            foreach (KeyValuePair<string, float> ring in _ringRadii)
            {
                float distance = Mathf.Abs(radius - ring.Value);
                if (distance < TrackHalfWidth + 0.22f
                    && distance < bestDistance)
                {
                    bestDistance = distance;
                    ringId = ring.Key;
                }
            }

            return ringId != null;
        }

        public bool TryGetTransferMarbleAtWorldPoint(
            BoardModel model,
            Vector2 worldPoint,
            out string gateId,
            out Vector2 marbleWorldPosition)
        {
            float bestDistance = TransferMarbleHitRadius;
            gateId = null;
            marbleWorldPosition = Vector2.zero;

            foreach (GateState gate in model.Gates)
            {
                RingState source = model.GetRing(gate.FromRing);
                if (!source.TryGetMarbleAtWorldIndex(
                        gate.FromIndex,
                        source.RotationOffset,
                        out int sourceLocalIndex,
                        out _)
                    || !_marbleVisuals.TryGetValue(
                        source.Id,
                        out Dictionary<int, MarbleVisual> visuals)
                    || !visuals.TryGetValue(
                        sourceLocalIndex,
                        out MarbleVisual visual)
                    || visual.Placement == null)
                {
                    continue;
                }

                Vector3 position = visual.Placement.transform.position;
                Vector2 marblePosition =
                    new Vector2(position.x, position.y);
                float distance =
                    Vector2.Distance(worldPoint, marblePosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    gateId = gate.Id;
                    marbleWorldPosition = marblePosition;
                }
            }

            return gateId != null;
        }

        public bool TryGetGateWorldPosition(
            string gateId,
            out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;
            if (_contentRoot == null
                || !_gatePositions.TryGetValue(
                    gateId,
                    out Vector2 localPosition))
            {
                return false;
            }

            Vector3 transformed = _contentRoot.TransformPoint(
                new Vector3(localPosition.x, localPosition.y, 0f));
            worldPosition = new Vector2(transformed.x, transformed.y);
            return true;
        }

        public int GetPendingExitAnimationCount(BoardModel model)
        {
            int count = 0;
            foreach (ExitState exit in model.Exits)
            {
                if (TryGetPendingExitVisual(
                        model,
                        exit,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    count++;
                }
            }

            return count;
        }

        public float GetRingStepAngle(string ringId, BoardModel model)
        {
            return 360f / model.GetRing(ringId).Capacity;
        }

        public bool BeginRingDrag(string ringId)
        {
            if (_ringAnimation != null
                || _transferAnimation != null
                || _exitAnimation != null
                || !_ringRoots.TryGetValue(ringId, out Transform ringRoot))
            {
                return false;
            }

            _previewRingId = ringId;
            _previewBaseAngle = SignedLocalAngle(ringRoot);
            _previewTargetAngle = _previewBaseAngle;
            _previewVisualAngle = _previewBaseAngle;
            return true;
        }

        public void PreviewRingRotation(
            string ringId,
            float dragAngleDegrees)
        {
            if (!string.Equals(
                    _previewRingId,
                    ringId,
                    StringComparison.OrdinalIgnoreCase)
                || !_ringRoots.TryGetValue(ringId, out Transform ringRoot))
            {
                return;
            }

            _previewTargetAngle = _previewBaseAngle + dragAngleDegrees;
        }

        public void AnimateRingToModel(
            string ringId,
            BoardModel model,
            Action onComplete)
        {
            if (!_ringRoots.TryGetValue(ringId, out Transform ringRoot))
            {
                Render(model);
                onComplete?.Invoke();
                return;
            }

            float currentAngle = string.Equals(
                _previewRingId,
                ringId,
                StringComparison.OrdinalIgnoreCase)
                ? _previewVisualAngle
                : SignedLocalAngle(ringRoot);
            StopRingAnimation();

            RingState ring = model.GetRing(ringId);
            float stepAngle = 360f / ring.Capacity;
            float modelAngle = -ring.RotationOffset * stepAngle;
            float targetAngle = currentAngle
                + Mathf.DeltaAngle(currentAngle, modelAngle);
            float snapDistance =
                Mathf.Abs(targetAngle - currentAngle);
            float duration = Mathf.Lerp(
                MinimumSnapDuration,
                MaximumSnapDuration,
                Mathf.Clamp01(snapDistance / stepAngle));

            if (snapDistance <= 0.01f)
            {
                SetLocalAngle(ringRoot, targetAngle);
                CompleteActionVisuals(model, onComplete);
                return;
            }

            _ringAnimation = StartCoroutine(
                AnimateRingRotation(
                    ringRoot,
                    currentAngle,
                    targetAngle,
                    duration,
                    model,
                    onComplete));
        }

        public bool AnimateGateTransfer(
            string gateId,
            BoardModel model,
            Action onComplete)
        {
            if (_transferAnimation != null
                || _ringAnimation != null
                || _exitAnimation != null
                || _dynamicRoot == null
                || !_gatePositions.TryGetValue(
                    gateId,
                    out Vector2 gateLocalPosition))
            {
                return false;
            }

            GateState gate = model.GetGate(gateId);
            RingState source = model.GetRing(gate.FromRing);
            RingState destination = model.GetRing(gate.ToRing);
            int sourceLocalIndex = BoardModel.Mod(
                gate.FromIndex - source.RotationOffset,
                source.Capacity);
            int destinationLocalIndex = BoardModel.Mod(
                gate.ToIndex - destination.RotationOffset,
                destination.Capacity);

            if (!_marbleVisuals.TryGetValue(
                    source.Id,
                    out Dictionary<int, MarbleVisual> sourceVisuals)
                || !sourceVisuals.TryGetValue(
                    sourceLocalIndex,
                    out MarbleVisual visual)
                || visual.Placement == null
                || !_ringRoots.TryGetValue(
                    destination.Id,
                    out Transform destinationRoot))
            {
                return false;
            }

            Transform marble = visual.Placement.transform;
            Vector3 startWorldPosition = marble.position;
            Vector3 portalWorldPosition = _contentRoot.TransformPoint(
                new Vector3(
                    gateLocalPosition.x,
                    gateLocalPosition.y,
                    0f));
            Vector2 destinationLocalPosition = PointOnCircle(
                _ringRadii[destination.Id],
                destinationLocalIndex,
                destination.Capacity);
            Vector3 destinationWorldPosition =
                destinationRoot.TransformPoint(
                    new Vector3(
                        destinationLocalPosition.x,
                        destinationLocalPosition.y,
                        0f));

            portalWorldPosition.z = startWorldPosition.z;
            destinationWorldPosition.z = startWorldPosition.z;
            marble.SetParent(_dynamicRoot, true);
            Vector3 fullScale = marble.localScale;

            _transferAnimation = StartCoroutine(
                AnimateGravityTransfer(
                    visual,
                    source,
                    sourceLocalIndex,
                    destination,
                    destinationLocalIndex,
                    destinationRoot,
                    startWorldPosition,
                    portalWorldPosition,
                    destinationWorldPosition,
                    fullScale,
                    model,
                    onComplete));
            return true;
        }

        private Transform CreateRingRoot(RingState ring)
        {
            GameObject root = new GameObject($"{ring.Id} Ring");
            root.transform.SetParent(_dynamicRoot, false);
            float stepAngle = 360f / ring.Capacity;
            SetLocalAngle(
                root.transform,
                -ring.RotationOffset * stepAngle);
            return root.transform;
        }

        private void CreateRing(int ringIndex, string ringId)
        {
            GameObject geometry = CreateBlenderModel(
                _ringModels[ringIndex],
                $"{ringId} Ring Geometry",
                _staticRoot,
                Vector2.zero,
                0f);
            AssignRingMaterials(geometry);
        }

        private void CreateMarbles(
            RingState ring,
            float radius,
            Transform ringRoot)
        {
            var visuals = new Dictionary<int, MarbleVisual>();
            _marbleVisuals[ring.Id] = visuals;

            foreach (KeyValuePair<int, MarbleColor> marble in ring.Marbles)
            {
                visuals.Add(
                    marble.Key,
                    CreateMarbleVisual(
                        ring,
                        radius,
                        ringRoot,
                        marble.Key,
                        marble.Value));
            }
        }

        private MarbleVisual CreateMarbleVisual(
            RingState ring,
            float radius,
            Transform ringRoot,
            int localIndex,
            MarbleColor color)
        {
            Vector2 point = PointOnCircle(
                radius,
                localIndex,
                ring.Capacity);
            GameObject placement = CreateBlenderModel(
                _marbleModel,
                $"{MarbleColorUtility.DisplayName(color)} Marble",
                ringRoot,
                point,
                0f);
            AssignAllRenderers(
                placement,
                _marbleMaterials[color]);
            return new MarbleVisual(placement, color);
        }

        private void SynchronizeMarbles(
            RingState ring,
            Transform ringRoot)
        {
            Dictionary<int, MarbleVisual> visuals =
                _marbleVisuals[ring.Id];
            _marbleRemovalBuffer.Clear();

            foreach (KeyValuePair<int, MarbleVisual> visual in visuals)
            {
                if (!ring.Marbles.ContainsKey(visual.Key))
                {
                    _marbleRemovalBuffer.Add(visual.Key);
                }
            }

            foreach (int localIndex in _marbleRemovalBuffer)
            {
                ReleaseObject(visuals[localIndex].Placement);
                visuals.Remove(localIndex);
            }

            float radius = _ringRadii[ring.Id];
            foreach (KeyValuePair<int, MarbleColor> marble in ring.Marbles)
            {
                if (!visuals.TryGetValue(
                        marble.Key,
                        out MarbleVisual visual))
                {
                    visuals.Add(
                        marble.Key,
                        CreateMarbleVisual(
                            ring,
                            radius,
                            ringRoot,
                            marble.Key,
                            marble.Value));
                    continue;
                }

                if (visual.Color == marble.Value)
                {
                    continue;
                }

                visual.Color = marble.Value;
                visual.Placement.name =
                    $"{MarbleColorUtility.DisplayName(marble.Value)} Marble";
                AssignAllRenderers(
                    visual.Placement,
                    _marbleMaterials[marble.Value]);
            }
        }

        private void CreateGate(BoardModel model, GateState gate)
        {
            RingState source = model.GetRing(gate.FromRing);
            float sourceRadius = _ringRadii[gate.FromRing];
            float destinationRadius = _ringRadii[gate.ToRing];
            float angle = AngleForIndex(gate.FromIndex, source.Capacity);
            float radius = (sourceRadius + destinationRadius) * 0.5f;
            Vector2 point = PointOnCircle(radius, angle);
            _gatePositions[gate.Id] = point;

            GameObject geometry = CreateBlenderModel(
                _portalModel,
                gate.Id,
                _staticRoot,
                point,
                angle - 90f);
            AssignPortalMaterials(geometry);
        }

        private void CreateExit(BoardModel model, ExitState exit)
        {
            RingState ring = model.GetRing(exit.Ring);
            float angle = AngleForIndex(exit.RingIndex, ring.Capacity);
            float radius = _receiverRadius;
            Vector2 point = PointOnCircle(radius, angle);
            _exitPositions[exit.Id] = point;

            GameObject geometry = CreateBlenderModel(
                _receiverModel,
                $"{MarbleColorUtility.DisplayName(exit.Color)} Receiver",
                _staticRoot,
                point,
                angle);
            AssignReceiverMaterials(geometry, exit.Color);
        }

        private void CreateCenter()
        {
            GameObject geometry = CreateBlenderModel(
                _centerModel,
                "Center Hub",
                _staticRoot,
                Vector2.zero,
                0f);
            AssignAllRenderers(geometry, _centerMaterial);
        }

        private void CreateBackdrop()
        {
            GameObject geometry = CreateBlenderModel(
                _backdropModel,
                "Studio Backdrop",
                _staticRoot,
                Vector2.zero,
                0f);
            geometry.transform.localScale = Vector3.one * BackdropScale;
            AssignAllRenderers(geometry, _backdropMaterial);

            foreach (Renderer renderer in
                     geometry.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private GameObject CreateBlenderModel(
            GameObject model,
            string instanceName,
            Transform parent,
            Vector2 point,
            float angleDegrees)
        {
            GameObject placement = new GameObject(instanceName);
            placement.transform.SetParent(parent, false);
            placement.transform.localPosition =
                new Vector3(point.x, point.y, 0f);
            placement.transform.localRotation =
                Quaternion.Euler(0f, 0f, angleDegrees);

            GameObject coordinateSpace =
                new GameObject("Blender Coordinate Space");
            coordinateSpace.transform.SetParent(placement.transform, false);
            coordinateSpace.transform.localRotation = BlenderBoardRotation;

            GameObject geometry = Instantiate(
                model,
                coordinateSpace.transform,
                false);
            geometry.name = "Imported Blender Mesh";

            foreach (Collider collider in
                     geometry.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            return placement;
        }

        private void AssignRingMaterials(GameObject geometry)
        {
            foreach (Renderer renderer in
                     geometry.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial =
                    renderer.gameObject.name.Contains("Trough")
                        ? _trackMaterial
                        : _railMaterial;
                ConfigureBoardRenderer(renderer);
            }
        }

        private void AssignPortalMaterials(GameObject geometry)
        {
            foreach (Renderer renderer in
                     geometry.GetComponentsInChildren<Renderer>(true))
            {
                string rendererName = renderer.gameObject.name;
                renderer.sharedMaterial =
                    rendererName.Contains("Arrow")
                        ? _arrowMaterial
                        : rendererName.Contains("Passage")
                            ? _exitInteriorMaterial
                            : _portalMaterial;
                ConfigureBoardRenderer(renderer);
            }
        }

        private void AssignReceiverMaterials(
            GameObject geometry,
            MarbleColor color)
        {
            foreach (Renderer renderer in
                     geometry.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial =
                    renderer.gameObject.name.Contains("Opening")
                        ? _exitInteriorMaterial
                        : _receiverMaterials[color];
                ConfigureBoardRenderer(renderer);
            }
        }

        private static void AssignAllRenderers(
            GameObject geometry,
            Material material)
        {
            foreach (Renderer renderer in
                     geometry.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                ConfigureBoardRenderer(renderer);
            }
        }

        private static void ConfigureBoardRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private IEnumerator AnimateGravityTransfer(
            MarbleVisual visual,
            RingState source,
            int sourceLocalIndex,
            RingState destination,
            int destinationLocalIndex,
            Transform destinationRoot,
            Vector3 startWorldPosition,
            Vector3 portalWorldPosition,
            Vector3 destinationWorldPosition,
            Vector3 fullScale,
            BoardModel model,
            Action onComplete)
        {
            Transform marble = visual.Placement.transform;
            float elapsed = 0f;
            while (elapsed < GravityEntryDuration && marble != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress =
                    Mathf.Clamp01(elapsed / GravityEntryDuration);
                float gravityProgress =
                    progress * progress * progress;
                marble.position = Vector3.LerpUnclamped(
                    startWorldPosition,
                    portalWorldPosition,
                    gravityProgress);
                marble.localScale = fullScale * Mathf.LerpUnclamped(
                    1f,
                    PortalScale,
                    SmootherStep(progress));
                yield return null;
            }

            if (marble != null)
            {
                marble.position = portalWorldPosition;
                marble.localScale = fullScale * PortalScale;
            }

            elapsed = 0f;
            while (elapsed < GravityExitDuration && marble != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress =
                    Mathf.Clamp01(elapsed / GravityExitDuration);
                float inverse = 1f - progress;
                float gravityProgress =
                    1f - inverse * inverse * inverse;
                marble.position = Vector3.LerpUnclamped(
                    portalWorldPosition,
                    destinationWorldPosition,
                    gravityProgress);
                marble.localScale = fullScale * Mathf.LerpUnclamped(
                    PortalScale,
                    1f,
                    SmootherStep(progress));
                yield return null;
            }

            _transferAnimation = null;
            CompleteGateTransfer(
                visual,
                source,
                sourceLocalIndex,
                destination,
                destinationLocalIndex,
                destinationRoot,
                fullScale,
                model,
                onComplete);
        }

        private void CompleteGateTransfer(
            MarbleVisual visual,
            RingState source,
            int sourceLocalIndex,
            RingState destination,
            int destinationLocalIndex,
            Transform destinationRoot,
            Vector3 fullScale,
            BoardModel model,
            Action onComplete)
        {
            _marbleVisuals[source.Id].Remove(sourceLocalIndex);

            Dictionary<int, MarbleVisual> destinationVisuals =
                _marbleVisuals[destination.Id];
            if (!destinationVisuals.ContainsKey(destinationLocalIndex)
                && visual.Placement != null)
            {
                Transform marble = visual.Placement.transform;
                Vector2 localPosition = PointOnCircle(
                    _ringRadii[destination.Id],
                    destinationLocalIndex,
                    destination.Capacity);
                marble.SetParent(destinationRoot, false);
                marble.localPosition = new Vector3(
                    localPosition.x,
                    localPosition.y,
                    0f);
                marble.localRotation = Quaternion.identity;
                marble.localScale = fullScale;
                destinationVisuals.Add(destinationLocalIndex, visual);
            }
            else if (visual.Placement != null)
            {
                ReleaseObject(visual.Placement);
            }

            CompleteActionVisuals(model, onComplete);
        }

        private void CompleteActionVisuals(
            BoardModel model,
            Action onComplete)
        {
            List<ReceiverShredTarget> resolvedExits =
                CollectResolvedExitVisuals(model);
            if (resolvedExits.Count == 0)
            {
                SynchronizeModel(model);
                onComplete?.Invoke();
                return;
            }

            _exitAnimation = StartCoroutine(
                _receiverShredEffect.Animate(
                    resolvedExits,
                    () =>
                    {
                        _exitAnimation = null;
                        SynchronizeModel(model);
                        onComplete?.Invoke();
                    }));
        }

        private List<ReceiverShredTarget> CollectResolvedExitVisuals(
            BoardModel model)
        {
            var resolved = new List<ReceiverShredTarget>(model.Exits.Count);
            foreach (ExitState exit in model.Exits)
            {
                if (!TryGetPendingExitVisual(
                        model,
                        exit,
                        out RingState ring,
                        out int localIndex,
                        out MarbleVisual visual,
                        out Vector2 receiverLocalPosition))
                {
                    continue;
                }

                Transform marble = visual.Placement.transform;
                Vector3 startWorldPosition = marble.position;
                Vector3 receiverWorldPosition =
                    _contentRoot.TransformPoint(
                        new Vector3(
                            receiverLocalPosition.x,
                            receiverLocalPosition.y,
                            0f));
                receiverWorldPosition.z = startWorldPosition.z;
                Vector3 outwardWorldDirection =
                    _contentRoot.TransformDirection(
                        new Vector3(
                            receiverLocalPosition.x,
                            receiverLocalPosition.y,
                            0f)).normalized;

                marble.SetParent(_dynamicRoot, true);
                Vector3 fullScale = marble.localScale;
                _marbleVisuals[ring.Id].Remove(localIndex);
                resolved.Add(
                    new ReceiverShredTarget(
                        visual.Placement,
                        visual.Placement.GetComponentsInChildren<Renderer>(
                            true),
                        visual.Color,
                        startWorldPosition,
                        receiverWorldPosition,
                        outwardWorldDirection,
                        fullScale));
            }

            return resolved;
        }

        private bool TryGetPendingExitVisual(
            BoardModel model,
            ExitState exit,
            out RingState ring,
            out int localIndex,
            out MarbleVisual visual,
            out Vector2 receiverLocalPosition)
        {
            ring = model.GetRing(exit.Ring);
            localIndex = BoardModel.Mod(
                exit.RingIndex - ring.RotationOffset,
                ring.Capacity);
            visual = null;
            receiverLocalPosition = Vector2.zero;

            return !ring.Marbles.ContainsKey(localIndex)
                   && _exitPositions.TryGetValue(
                       exit.Id,
                       out receiverLocalPosition)
                   && _marbleVisuals.TryGetValue(
                       ring.Id,
                       out Dictionary<int, MarbleVisual> visuals)
                   && visuals.TryGetValue(localIndex, out visual)
                   && visual.Color == exit.Color
                   && visual.Placement != null;
        }

        private IEnumerator AnimateRingRotation(
            Transform ringRoot,
            float startAngle,
            float targetAngle,
            float duration,
            BoardModel model,
            Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration && ringRoot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress =
                    progress
                    * progress
                    * progress
                    * (progress * (progress * 6f - 15f) + 10f);
                SetLocalAngle(
                    ringRoot,
                    Mathf.LerpUnclamped(
                        startAngle,
                        targetAngle,
                        easedProgress));
                yield return null;
            }

            if (ringRoot != null)
            {
                SetLocalAngle(ringRoot, targetAngle);
            }

            _ringAnimation = null;
            _previewRingId = null;
            CompleteActionVisuals(model, onComplete);
        }

        private static float SmootherStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t
                   * t
                   * t
                   * (t * (t * 6f - 15f) + 10f);
        }

        private void LateUpdate()
        {
            if (_ringAnimation != null
                || _previewRingId == null
                || !_ringRoots.TryGetValue(
                    _previewRingId,
                    out Transform ringRoot))
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float follow =
                1f - Mathf.Exp(-DragFollowSharpness * deltaTime);
            _previewVisualAngle = Mathf.Lerp(
                _previewVisualAngle,
                _previewTargetAngle,
                follow);

            if (Mathf.Abs(
                    _previewTargetAngle - _previewVisualAngle) < 0.01f)
            {
                _previewVisualAngle = _previewTargetAngle;
            }

            SetLocalAngle(ringRoot, _previewVisualAngle);
        }

        private void StopRingAnimation()
        {
            if (_ringAnimation != null)
            {
                StopCoroutine(_ringAnimation);
                _ringAnimation = null;
            }

            _previewRingId = null;
        }

        private void StopTransferAnimation()
        {
            if (_transferAnimation == null)
            {
                return;
            }

            StopCoroutine(_transferAnimation);
            _transferAnimation = null;
        }

        private void StopExitAnimation()
        {
            if (_exitAnimation == null)
            {
                return;
            }

            StopCoroutine(_exitAnimation);
            _exitAnimation = null;
        }

        private static float SignedLocalAngle(Transform target)
        {
            return Mathf.DeltaAngle(0f, target.localEulerAngles.z);
        }

        private static void SetLocalAngle(
            Transform target,
            float angleDegrees)
        {
            target.localRotation =
                Quaternion.Euler(0f, 0f, angleDegrees);
        }

        private static GameObject LoadModel(string modelName)
        {
            GameObject model = Resources.Load<GameObject>(
                ModelResourceRoot + modelName);
            if (model == null)
            {
                throw new InvalidOperationException(
                    $"Blender board model '{modelName}' could not be loaded.");
            }

            return model;
        }

        private Material CreateMaterial(
            string materialName,
            Color color,
            float metallic,
            float smoothness,
            bool enableInstancing = false)
        {
            Shader shader = Resources.Load<Shader>(
                "Shaders/PrototypeSurface");
            if (shader == null)
            {
                shader = Shader.Find("OrbitSort/PrototypeSurface");
            }

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Orbit Sort prototype shader could not be loaded.");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = enableInstancing
            };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            _generatedAssets.Add(material);
            return material;
        }

        private Material CreateBackdropMaterial()
        {
            Shader shader = Resources.Load<Shader>(
                "Shaders/PremiumBackdrop");
            if (shader == null)
            {
                shader = Shader.Find("OrbitSort/PremiumBackdrop");
            }

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Orbit Sort premium backdrop shader could not be loaded.");
            }

            var material = new Material(shader)
            {
                name = "Premium Periwinkle Backdrop"
            };
            material.SetColor(
                "_TopColor",
                new Color(0.72f, 0.80f, 1.00f));
            material.SetColor(
                "_BottomColor",
                new Color(0.62f, 0.55f, 0.91f));
            material.SetColor(
                "_CenterColor",
                new Color(0.91f, 0.86f, 1.00f));
            material.SetFloat("_Smoothness", 0.18f);
            _generatedAssets.Add(material);
            return material;
        }

        private static Vector2 PointOnCircle(
            float radius,
            int worldIndex,
            int capacity)
        {
            return PointOnCircle(
                radius,
                AngleForIndex(worldIndex, capacity));
        }

        private static Vector2 PointOnCircle(float radius, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius);
        }

        private static float AngleForIndex(int index, int capacity)
        {
            return 90f - index * (360f / capacity);
        }

        private bool CanSynchronize(BoardModel model)
        {
            if (_contentRoot == null
                || model.Rings.Count != _ringRoots.Count
                || model.Rings.Count != _marbleVisuals.Count)
            {
                return false;
            }

            foreach (RingState ring in model.Rings)
            {
                if (!_ringRoots.ContainsKey(ring.Id)
                    || !_marbleVisuals.ContainsKey(ring.Id))
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearContent()
        {
            _previewRingId = null;
            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(false);
                ReleaseObject(_contentRoot.gameObject);
                _contentRoot = null;
                _staticRoot = null;
                _dynamicRoot = null;
            }
        }

        private void OnDestroy()
        {
            StopRingAnimation();
            StopTransferAnimation();
            StopExitAnimation();
            foreach (UnityEngine.Object asset in _generatedAssets)
            {
                if (asset != null)
                {
                    ReleaseObject(asset);
                }
            }

            _generatedAssets.Clear();
        }

        private static void ReleaseObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class MarbleVisual
        {
            public MarbleVisual(
                GameObject placement,
                MarbleColor color)
            {
                Placement = placement;
                Color = color;
            }

            public GameObject Placement { get; }
            public MarbleColor Color { get; set; }
        }
    }
}
