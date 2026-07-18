using DriftTogether.World;
using UnityEngine;

namespace DriftTogether.Player
{
    /// <summary>Builds the low-poly co-op character visual (no networking, no physics).</summary>
    public static class CharacterFactory
    {
        public static readonly Color[] TeamColors =
        {
            new Color(0.85f, 0.4f, 0.25f),   // оранжевый
            new Color(0.35f, 0.65f, 0.9f),   // голубой
            new Color(0.55f, 0.8f, 0.4f),    // зелёный
            new Color(0.85f, 0.7f, 0.3f)     // жёлтый
        };

        public static Transform BuildVisual(Transform parent, Color teamColor)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);

            var bodyMat = TintedMaterial("KayakTrim", teamColor * 0.9f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            Object.Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(visual, false);
            head.transform.localPosition = new Vector3(0f, 1.12f, 0f);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            head.GetComponent<MeshRenderer>().sharedMaterial = GameMaterials.Get("MushroomStem");
            Object.Destroy(head.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cap";
            cap.transform.SetParent(visual, false);
            cap.transform.localPosition = new Vector3(0f, 1.24f, 0.02f);
            cap.transform.localScale = new Vector3(0.34f, 0.16f, 0.34f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = TintedMaterial("KayakHull", teamColor);
            Object.Destroy(cap.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.SetParent(visual, false);
            nose.transform.localPosition = new Vector3(0f, 1.1f, 0.15f);
            nose.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            Object.Destroy(nose.GetComponent<Collider>());

            return visual;
        }

        static Material TintedMaterial(string baseName, Color color)
        {
            var mat = new Material(GameMaterials.Get(baseName));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            return mat;
        }
    }
}
