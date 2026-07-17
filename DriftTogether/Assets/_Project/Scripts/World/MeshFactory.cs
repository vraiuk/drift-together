using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>Procedural low-poly meshes: river ribbons, bank strips, kayak hull, cones.</summary>
    public static class MeshFactory
    {
        /// <summary>Flat ribbon following a spline, used for the water surface.</summary>
        public static Mesh BuildRibbon(IReadOnlyList<Vector3> samples, float halfWidth, float y)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 fwd = i < samples.Count - 1
                    ? samples[i + 1] - samples[i]
                    : samples[i] - samples[i - 1];
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);

                Vector3 c = samples[i];
                verts.Add(new Vector3(c.x, y, c.z) - right * halfWidth);
                verts.Add(new Vector3(c.x, y, c.z) + right * halfWidth);
                float v = i * 0.35f;
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));

                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }

            var mesh = new Mesh { name = "Ribbon" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Sloped bank strip along one side of a spline: rises from the water
        /// edge up to a shore line further out.
        /// </summary>
        public static Mesh BuildBankStrip(IReadOnlyList<Vector3> samples, float halfWidth,
            float bankWidth, float bankHeight, bool rightSide)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float side = rightSide ? 1f : -1f;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 fwd = i < samples.Count - 1
                    ? samples[i + 1] - samples[i]
                    : samples[i] - samples[i - 1];
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd) * side;

                Vector3 c = samples[i];
                Vector3 edge = new Vector3(c.x, -0.15f, c.z) + right * halfWidth;
                Vector3 mid = edge + right * (bankWidth * 0.45f) + Vector3.up * (bankHeight * 0.55f + 0.15f);
                Vector3 outer = edge + right * bankWidth + Vector3.up * (bankHeight + 0.15f);
                verts.Add(edge);
                verts.Add(mid);
                verts.Add(outer);

                if (i > 0)
                {
                    int b = (i - 1) * 3;
                    if (rightSide)
                    {
                        tris.AddRange(new[] { b, b + 3, b + 1, b + 1, b + 3, b + 4 });
                        tris.AddRange(new[] { b + 1, b + 4, b + 2, b + 2, b + 4, b + 5 });
                    }
                    else
                    {
                        tris.AddRange(new[] { b, b + 1, b + 3, b + 1, b + 4, b + 3 });
                        tris.AddRange(new[] { b + 1, b + 2, b + 4, b + 2, b + 5, b + 4 });
                    }
                }
            }

            var mesh = new Mesh { name = "BankStrip" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Simple lofted low-poly kayak hull, bow at +Z.</summary>
        public static Mesh BuildKayakHull()
        {
            // Cross sections along the hull (z, halfWidth, deckY, keelY).
            (float z, float w, float deck, float keel)[] sections =
            {
                (-1.6f, 0.06f, 0.22f, 0.10f),
                (-1.2f, 0.28f, 0.24f, -0.12f),
                (-0.4f, 0.42f, 0.26f, -0.20f),
                (0.4f, 0.42f, 0.26f, -0.20f),
                (1.2f, 0.26f, 0.24f, -0.12f),
                (1.7f, 0.05f, 0.24f, 0.12f)
            };

            var verts = new List<Vector3>();
            var tris = new List<int>();

            foreach (var s in sections)
            {
                // 5 points per section: deckL, waterL, keel, waterR, deckR
                verts.Add(new Vector3(-s.w, s.deck, s.z));
                verts.Add(new Vector3(-s.w * 1.15f, (s.deck + s.keel) * 0.5f, s.z));
                verts.Add(new Vector3(0f, s.keel, s.z));
                verts.Add(new Vector3(s.w * 1.15f, (s.deck + s.keel) * 0.5f, s.z));
                verts.Add(new Vector3(s.w, s.deck, s.z));
            }

            int ring = 5;
            for (int s = 0; s < sections.Length - 1; s++)
            {
                int a = s * ring;
                int b = (s + 1) * ring;
                for (int i = 0; i < ring - 1; i++)
                {
                    tris.AddRange(new[] { a + i, b + i, a + i + 1 });
                    tris.AddRange(new[] { a + i + 1, b + i, b + i + 1 });
                }
                // Deck (top) quad closing the ring.
                tris.AddRange(new[] { a + ring - 1, b + ring - 1, a });
                tris.AddRange(new[] { a, b + ring - 1, b });
            }

            var mesh = new Mesh { name = "KayakHull" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Low-poly cone (for fire, tree tops, arch decorations).</summary>
        public static Mesh BuildCone(float radius, float height, int segments = 10)
        {
            var verts = new List<Vector3> { Vector3.zero, Vector3.up * height };
            var tris = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < segments; i++)
            {
                int cur = 2 + i;
                int next = 2 + (i + 1) % segments;
                tris.AddRange(new[] { 1, cur, next }); // side
                tris.AddRange(new[] { 0, next, cur }); // base
            }
            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
