using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>Marker: collisions with this surface never damage the hull (banks, shallows edges).</summary>
    public sealed class SoftSurface : MonoBehaviour
    {
    }

    /// <summary>Animates a water ribbon's vertices with the shared wave function.</summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WaterSurface : MonoBehaviour
    {
        Mesh _mesh;
        Vector3[] _baseVerts;
        Vector3[] _workVerts;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            _mesh = filter.mesh; // instance copy
            _baseVerts = _mesh.vertices;
            _workVerts = new Vector3[_baseVerts.Length];
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < _baseVerts.Length; i++)
            {
                Vector3 v = _baseVerts[i];
                Vector3 world = transform.TransformPoint(v);
                v.y = _baseVerts[i].y + RiverFlow.WaterHeightAt(world, t) - 0.02f;
                _workVerts[i] = v;
            }
            _mesh.vertices = _workVerts;
        }
    }

    /// <summary>
    /// Small white foam quads drifting with the current — cheap, readable
    /// visualisation of the river flow around the player.
    /// </summary>
    public sealed class FoamDrifters : MonoBehaviour
    {
        public RiverFlow Flow;
        public Transform Player;
        public Material FoamMaterial;
        public int Count = 42;

        readonly List<Transform> _quads = new List<Transform>();

        void Start()
        {
            for (int i = 0; i < Count; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Foam";
                Object.Destroy(quad.GetComponent<Collider>());
                if (FoamMaterial != null)
                    quad.GetComponent<MeshRenderer>().sharedMaterial = FoamMaterial;
                quad.transform.SetParent(transform, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, Random.Range(0f, 360f));
                float s = Random.Range(0.12f, 0.4f);
                quad.transform.localScale = new Vector3(s, s, s);
                _quads.Add(quad.transform);
                Scatter(quad.transform, initial: true);
            }
        }

        void Scatter(Transform quad, bool initial)
        {
            if (Player == null)
                return;
            Vector2 c = Random.insideUnitCircle;
            float ahead = initial ? Random.Range(-15f, 30f) : Random.Range(8f, 30f);
            Vector3 basePos = Player.position + Player.forward * ahead;
            quad.position = new Vector3(basePos.x + c.x * 9f, 0.03f, basePos.z + c.y * 9f);
        }

        void Update()
        {
            if (Flow == null || Player == null)
                return;
            float t = Time.time;
            foreach (var quad in _quads)
            {
                Vector3 cur = Flow.CurrentAt(quad.position);
                Vector3 p = quad.position + cur * (Time.deltaTime * 0.9f);
                p.y = RiverFlow.WaterHeightAt(p, t) + 0.03f;
                quad.position = p;

                if ((quad.position - Player.position).sqrMagnitude > 45f * 45f)
                    Scatter(quad, initial: false);
            }
        }
    }
}
