using System;
using System.Collections;
using System.Collections.Generic;
using OrbitSort.Core;
using UnityEngine;

namespace OrbitSort.Presentation
{
    public sealed class OrbitSortBoardView : MonoBehaviour
    {
        private const float TrackHalfWidth = 0.48f;
        private const float MinimumSnapDuration = 0.10f;
        private const float MaximumSnapDuration = 0.24f;
        private const string ModelResourceRoot = "Models/";

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
        private readonly Dictionary<string, Vector2> _gatePositions =
            new Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);
        private readonly List<UnityEngine.Object> _generatedAssets =
            new List<UnityEngine.Object>();
        private readonly Dictionary<MarbleColor, Material> _marbleMaterials =
            new Dictionary<MarbleColor, Material>();

        private Transform _contentRoot;
        private GameObject[] _ringModels;
        private GameObject _portalModel;
        private GameObject _receiverModel;
        private GameObject _centerModel;
        private GameObject _marbleModel;
        private Material _trackMaterial;
        private Material _railMaterial;
        private Material _portalMaterial;
        private Material _centerMaterial;
        private Material _exitInteriorMaterial;
        private Material _arrowMaterial;
        private readonly Dictionary<MarbleColor, Material>
            _receiverMaterials =
                new Dictionary<MarbleColor, Material>();
        private Coroutine _ringAnimation;
        private string _previewRingId;
        private float _previewBaseAngle;
        private float _previewAngle;

        public void Initialize()
        {
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

            _trackMaterial = CreateMaterial(
                "Track",
                new Color(0.41f, 0.37f, 0.58f),
                0.00f,
                0.58f);
            _railMaterial = CreateMaterial(
                "Rail",
                new Color(0.89f, 0.78f, 0.60f),
                0.00f,
                0.72f);
            _portalMaterial = CreateMaterial(
                "Portal",
                new Color(0.92f, 0.48f, 0.035f),
                0.62f,
                0.75f);
            _centerMaterial = CreateMaterial(
                "Center",
                new Color(0.018f, 0.009f, 0.06f),
                0.00f,
                0.52f);
            _exitInteriorMaterial = CreateMaterial(
                "Exit Interior",
                new Color(0.018f, 0.009f, 0.06f),
                0.00f,
                0.52f);
            _arrowMaterial = CreateMaterial(
                "Gate Arrow",
                new Color(1f, 0.97f, 0.88f),
                0.00f,
                0.80f);

            _marbleMaterials[MarbleColor.Blue] =
                CreateMaterial(
                    "Blue Marble",
                    new Color(0.025f, 0.24f, 0.95f),
                    0.05f,
                    0.90f);
            _marbleMaterials[MarbleColor.Red] =
                CreateMaterial(
                    "Red Marble",
                    new Color(0.93f, 0.035f, 0.018f),
                    0.03f,
                    0.90f);
            _marbleMaterials[MarbleColor.Yellow] =
                CreateMaterial(
                    "Yellow Marble",
                    new Color(1.00f, 0.54f, 0.015f),
                    0.03f,
                    0.89f);

            _receiverMaterials[MarbleColor.Blue] =
                CreateMaterial(
                    "Blue Receiver",
                    new Color(0.015f, 0.22f, 0.95f),
                    0.10f,
                    0.82f);
            _receiverMaterials[MarbleColor.Red] =
                CreateMaterial(
                    "Red Receiver",
                    new Color(0.93f, 0.025f, 0.018f),
                    0.08f,
                    0.82f);
            _receiverMaterials[MarbleColor.Yellow] =
                CreateMaterial(
                    "Yellow Receiver",
                    new Color(1.00f, 0.52f, 0.01f),
                    0.10f,
                    0.82f);
        }

        public void Render(BoardModel model)
        {
            StopRingAnimation();
            ClearContent();
            _ringRadii.Clear();
            _ringRoots.Clear();
            _gatePositions.Clear();

            GameObject content = new GameObject("Board Content");
            content.transform.SetParent(transform, false);
            _contentRoot = content.transform;

            if (model.Rings.Count != ApprovedRingRadii.Length)
            {
                throw new InvalidOperationException(
                    "Orbit Sort board art requires exactly three rings.");
            }

            for (int index = 0; index < model.Rings.Count; index++)
            {
                RingState ring = model.Rings[index];
                float radius = ApprovedRingRadii[index];
                _ringRadii[ring.Id] = radius;
                Transform ringRoot = CreateRingRoot(ring);
                _ringRoots[ring.Id] = ringRoot;
                CreateRing(index, ringRoot);
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

        public bool TryGetGateAtWorldPoint(
            Vector2 worldPoint,
            out string gateId)
        {
            float bestDistance = 0.72f;
            gateId = null;

            foreach (KeyValuePair<string, Vector2> gate in _gatePositions)
            {
                float distance = Vector2.Distance(worldPoint, gate.Value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    gateId = gate.Key;
                }
            }

            return gateId != null;
        }

        public float GetRingStepAngle(string ringId, BoardModel model)
        {
            return 360f / model.GetRing(ringId).Capacity;
        }

        public bool BeginRingDrag(string ringId)
        {
            if (_ringAnimation != null
                || !_ringRoots.TryGetValue(ringId, out Transform ringRoot))
            {
                return false;
            }

            _previewRingId = ringId;
            _previewBaseAngle = SignedLocalAngle(ringRoot);
            _previewAngle = _previewBaseAngle;
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

            _previewAngle = _previewBaseAngle + dragAngleDegrees;
            SetLocalAngle(ringRoot, _previewAngle);
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
                ? _previewAngle
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

            _ringAnimation = StartCoroutine(
                AnimateRingRotation(
                    ringRoot,
                    currentAngle,
                    targetAngle,
                    duration,
                    model,
                    onComplete));
        }

        private Transform CreateRingRoot(RingState ring)
        {
            GameObject root = new GameObject($"{ring.Id} Ring");
            root.transform.SetParent(_contentRoot, false);
            float stepAngle = 360f / ring.Capacity;
            SetLocalAngle(
                root.transform,
                -ring.RotationOffset * stepAngle);
            return root.transform;
        }

        private void CreateRing(int ringIndex, Transform ringRoot)
        {
            GameObject geometry = CreateBlenderModel(
                _ringModels[ringIndex],
                "Blender Ring Geometry",
                ringRoot,
                Vector2.zero,
                0f);
            AssignRingMaterials(geometry);
        }

        private void CreateMarbles(
            RingState ring,
            float radius,
            Transform ringRoot)
        {
            foreach (KeyValuePair<int, MarbleColor> marble in ring.Marbles)
            {
                Vector2 point = PointOnCircle(
                    radius,
                    marble.Key,
                    ring.Capacity);

                GameObject geometry = CreateBlenderModel(
                    _marbleModel,
                    $"{MarbleColorUtility.DisplayName(marble.Value)} Marble",
                    ringRoot,
                    point,
                    0f);
                AssignAllRenderers(
                    geometry,
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
                _contentRoot,
                point,
                angle - 90f);
            AssignPortalMaterials(geometry);
        }

        private void CreateExit(BoardModel model, ExitState exit)
        {
            RingState ring = model.GetRing(exit.Ring);
            float angle = AngleForIndex(exit.RingIndex, ring.Capacity);
            float radius = _ringRadii[exit.Ring] + 1.20f;
            Vector2 point = PointOnCircle(radius, angle);

            GameObject geometry = CreateBlenderModel(
                _receiverModel,
                $"{MarbleColorUtility.DisplayName(exit.Color)} Receiver",
                _contentRoot,
                point,
                angle);
            AssignReceiverMaterials(geometry, exit.Color);
        }

        private void CreateCenter()
        {
            GameObject geometry = CreateBlenderModel(
                _centerModel,
                "Center Hub",
                _contentRoot,
                Vector2.zero,
                0f);
            AssignAllRenderers(geometry, _centerMaterial);
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

            return geometry;
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
            }
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
                    1f - Mathf.Pow(1f - progress, 3f);
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
            Render(model);
            onComplete?.Invoke();
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
            float smoothness)
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
                color = color
            };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

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

        private void ClearContent()
        {
            _previewRingId = null;
            if (_contentRoot != null)
            {
                Destroy(_contentRoot.gameObject);
                _contentRoot = null;
            }

        }

        private void OnDestroy()
        {
            StopRingAnimation();
            foreach (UnityEngine.Object asset in _generatedAssets)
            {
                if (asset != null)
                {
                    Destroy(asset);
                }
            }

            _generatedAssets.Clear();
        }
    }
}
