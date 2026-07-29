using System;
using System.Collections.Generic;
using OrbitSort.Core;
using UnityEngine;

namespace OrbitSort.Presentation
{
    public sealed class OrbitSortBoardView : MonoBehaviour
    {
        private const int CircleSegments = 96;
        private const float TrackHalfWidth = 0.48f;

        private readonly Dictionary<string, float> _ringRadii =
            new Dictionary<string, float>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> _gatePositions =
            new Dictionary<string, Vector2>(
                StringComparer.OrdinalIgnoreCase);
        private readonly List<UnityEngine.Object> _generatedAssets =
            new List<UnityEngine.Object>();
        private readonly Dictionary<MarbleColor, Material> _marbleMaterials =
            new Dictionary<MarbleColor, Material>();

        private Transform _contentRoot;
        private Material _trackMaterial;
        private Material _railMaterial;
        private Material _warningMaterial;
        private Material _jammedMaterial;
        private Material _gateMaterial;
        private Material _gateReadyMaterial;
        private Material _centerMaterial;
        private Material _exitInteriorMaterial;
        private Material _arrowMaterial;

        public void Initialize()
        {
            _trackMaterial = CreateMaterial(
                "Track",
                new Color(0.70f, 0.68f, 0.75f));
            _railMaterial = CreateMaterial(
                "Rail",
                new Color(0.95f, 0.86f, 0.68f));
            _warningMaterial = CreateMaterial(
                "Last Gap",
                new Color(1.00f, 0.65f, 0.13f));
            _jammedMaterial = CreateMaterial(
                "Jammed",
                new Color(0.95f, 0.18f, 0.22f));
            _gateMaterial = CreateMaterial(
                "Gate",
                new Color(0.88f, 0.56f, 0.10f));
            _gateReadyMaterial = CreateMaterial(
                "Gate Ready",
                new Color(0.38f, 0.92f, 0.52f));
            _centerMaterial = CreateMaterial(
                "Center",
                new Color(0.17f, 0.13f, 0.30f));
            _exitInteriorMaterial = CreateMaterial(
                "Exit Interior",
                new Color(0.06f, 0.04f, 0.12f));
            _arrowMaterial = CreateMaterial(
                "Gate Arrow",
                new Color(1f, 0.97f, 0.88f));

            _marbleMaterials[MarbleColor.Blue] =
                CreateMaterial("Blue Marble", new Color(0.10f, 0.50f, 1.00f));
            _marbleMaterials[MarbleColor.Red] =
                CreateMaterial("Red Marble", new Color(1.00f, 0.22f, 0.18f));
            _marbleMaterials[MarbleColor.Yellow] =
                CreateMaterial(
                    "Yellow Marble",
                    new Color(1.00f, 0.72f, 0.08f));
        }

        public void Render(BoardModel model)
        {
            ClearContent();
            _ringRadii.Clear();
            _gatePositions.Clear();

            GameObject content = new GameObject("Board Content");
            content.transform.SetParent(transform, false);
            _contentRoot = content.transform;

            float[] radii = CalculateRadii(model.Rings.Count);
            for (int index = 0; index < model.Rings.Count; index++)
            {
                RingState ring = model.Rings[index];
                float radius = radii[index];
                _ringRadii[ring.Id] = radius;
                CreateRing(ring, radius);
                CreateMarbles(ring, radius);
            }

            CreateCenter(radii[0]);

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

        private void CreateRing(RingState ring, float radius)
        {
            Material rail = ring.GapCount == 0
                ? _jammedMaterial
                : ring.GapCount == 1
                    ? _warningMaterial
                    : _railMaterial;

            CreateAnnulusObject(
                $"{ring.Id} Track",
                radius - TrackHalfWidth,
                radius + TrackHalfWidth,
                0.34f,
                _trackMaterial);
            CreateAnnulusObject(
                $"{ring.Id} Inner Rail",
                radius - TrackHalfWidth - 0.08f,
                radius - TrackHalfWidth + 0.04f,
                0.14f,
                rail);
            CreateAnnulusObject(
                $"{ring.Id} Outer Rail",
                radius + TrackHalfWidth - 0.04f,
                radius + TrackHalfWidth + 0.08f,
                0.14f,
                rail);
        }

        private void CreateMarbles(RingState ring, float radius)
        {
            float circumferenceSpacing =
                2f * Mathf.PI * radius / ring.Capacity;
            float diameter = Mathf.Clamp(
                circumferenceSpacing * 0.62f,
                0.42f,
                0.72f);

            foreach (KeyValuePair<int, MarbleColor> marble in ring.Marbles)
            {
                int worldIndex = BoardModel.Mod(
                    marble.Key + ring.RotationOffset,
                    ring.Capacity);
                Vector2 point = PointOnCircle(
                    radius,
                    worldIndex,
                    ring.Capacity);

                GameObject sphere = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                sphere.name =
                    $"{MarbleColorUtility.DisplayName(marble.Value)} Marble";
                sphere.transform.SetParent(_contentRoot, false);
                sphere.transform.localPosition =
                    new Vector3(point.x, point.y, -0.12f);
                sphere.transform.localScale =
                    new Vector3(diameter, diameter, diameter);
                sphere.GetComponent<Renderer>().sharedMaterial =
                    _marbleMaterials[marble.Value];

                Collider collider = sphere.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
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

            GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridge.name = gate.Id;
            bridge.transform.SetParent(_contentRoot, false);
            bridge.transform.localPosition =
                new Vector3(point.x, point.y, -0.28f);
            bridge.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 90f);
            bridge.transform.localScale = new Vector3(
                0.58f,
                Mathf.Abs(destinationRadius - sourceRadius) + 0.78f,
                0.22f);
            bridge.GetComponent<Renderer>().sharedMaterial =
                model.IsGateImmediatelyAvailable(gate.Id)
                    ? _gateReadyMaterial
                    : _gateMaterial;

            Collider collider = bridge.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            CreateGateArrow(point, angle);
        }

        private void CreateGateArrow(Vector2 point, float angle)
        {
            GameObject arrow = new GameObject("Outward Arrow");
            arrow.transform.SetParent(_contentRoot, false);
            arrow.transform.localPosition =
                new Vector3(point.x, point.y, -0.43f);
            arrow.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle - 90f);

            MeshFilter filter = arrow.AddComponent<MeshFilter>();
            MeshRenderer renderer = arrow.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh
            {
                name = "Gate Arrow"
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.18f, -0.12f, 0f),
                new Vector3(0.18f, -0.12f, 0f),
                new Vector3(0f, 0.22f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1 };
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = _arrowMaterial;
            _generatedAssets.Add(mesh);
        }

        private void CreateExit(BoardModel model, ExitState exit)
        {
            RingState ring = model.GetRing(exit.Ring);
            float angle = AngleForIndex(exit.RingIndex, ring.Capacity);
            float radius = _ringRadii[exit.Ring] + 1.20f;
            Vector2 point = PointOnCircle(radius, angle);

            Material colorMaterial = _marbleMaterials[exit.Color];
            CreateCylinder(
                $"{MarbleColorUtility.DisplayName(exit.Color)} Exit",
                point,
                0.78f,
                -0.10f,
                colorMaterial);
            CreateCylinder(
                "Exit Interior",
                point,
                0.50f,
                -0.28f,
                _exitInteriorMaterial);
        }

        private void CreateCenter(float innerRadius)
        {
            CreateCylinder(
                "Center Hub",
                Vector2.zero,
                Mathf.Max(0.70f, innerRadius - TrackHalfWidth - 0.28f),
                0.28f,
                _centerMaterial);
        }

        private void CreateCylinder(
            string objectName,
            Vector2 point,
            float radius,
            float depth,
            Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.SetParent(_contentRoot, false);
            cylinder.transform.localPosition =
                new Vector3(point.x, point.y, depth);
            cylinder.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            cylinder.transform.localScale =
                new Vector3(radius, 0.10f, radius);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void CreateAnnulusObject(
            string objectName,
            float innerRadius,
            float outerRadius,
            float depth,
            Material material)
        {
            GameObject ring = new GameObject(objectName);
            ring.transform.SetParent(_contentRoot, false);
            ring.transform.localPosition = new Vector3(0f, 0f, depth);

            MeshFilter filter = ring.AddComponent<MeshFilter>();
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            Mesh mesh = CreateAnnulusMesh(
                objectName,
                innerRadius,
                outerRadius);
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            _generatedAssets.Add(mesh);
        }

        private static Mesh CreateAnnulusMesh(
            string meshName,
            float innerRadius,
            float outerRadius)
        {
            Vector3[] vertices = new Vector3[(CircleSegments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[CircleSegments * 6];

            for (int index = 0; index <= CircleSegments; index++)
            {
                float radians =
                    index / (float)CircleSegments * Mathf.PI * 2f;
                float cosine = Mathf.Cos(radians);
                float sine = Mathf.Sin(radians);
                vertices[index * 2] =
                    new Vector3(cosine * innerRadius, sine * innerRadius, 0f);
                vertices[index * 2 + 1] =
                    new Vector3(cosine * outerRadius, sine * outerRadius, 0f);
                uv[index * 2] = new Vector2(0f, index / (float)CircleSegments);
                uv[index * 2 + 1] =
                    new Vector2(1f, index / (float)CircleSegments);
            }

            for (int index = 0; index < CircleSegments; index++)
            {
                int vertex = index * 2;
                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateMaterial(string materialName, Color color)
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

            _generatedAssets.Add(material);
            return material;
        }

        private static float[] CalculateRadii(int ringCount)
        {
            if (ringCount == 2)
            {
                return new[] { 2.45f, 4.25f };
            }

            float[] radii = new float[ringCount];
            for (int index = 0; index < ringCount; index++)
            {
                radii[index] = 1.80f + index * 1.48f;
            }

            return radii;
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
            if (_contentRoot != null)
            {
                Destroy(_contentRoot.gameObject);
                _contentRoot = null;
            }

            for (int index = _generatedAssets.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object asset = _generatedAssets[index];
                if (asset is Mesh)
                {
                    Destroy(asset);
                }
            }

            _generatedAssets.RemoveAll(asset => asset is Mesh);
        }

        private void OnDestroy()
        {
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
