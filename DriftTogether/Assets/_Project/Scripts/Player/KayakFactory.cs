using DriftTogether.World;
using UnityEngine;

namespace DriftTogether.Player
{
    /// <summary>Assembles the kayak: hull mesh, paddler paddle, Тапок-Тим, physics, splashes.</summary>
    public static class KayakFactory
    {
        public static KayakController Create(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Kayak");
            root.transform.SetPositionAndRotation(position, rotation);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.1f, 0f);
            collider.size = new Vector3(0.95f, 0.5f, 3.3f);
            collider.material = new PhysicsMaterial("KayakSlick")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                bounciness = 0.15f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            // Visual child (bobbing is applied here, physics stays clean).
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var hull = new GameObject("Hull");
            hull.transform.SetParent(visual, false);
            var hullFilter = hull.AddComponent<MeshFilter>();
            hullFilter.mesh = MeshFactory.BuildKayakHull();
            var hullRenderer = hull.AddComponent<MeshRenderer>();
            hullRenderer.sharedMaterial = GameMaterials.Get("KayakHull");

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "CockpitRim";
            rim.transform.SetParent(visual, false);
            rim.transform.localPosition = new Vector3(0f, 0.28f, 0.1f);
            rim.transform.localScale = new Vector3(0.55f, 0.08f, 1.5f);
            GameMaterials.ApplyTo(rim, "KayakTrim");
            Object.Destroy(rim.GetComponent<Collider>());

            BuildPaddle(visual);
            BuildTim(visual, root);

            var splash = root.AddComponent<PaddleSplash>();
            splash.Visual = visual;

            var controller = root.AddComponent<KayakController>();
            return controller;
        }

        static void BuildPaddle(Transform visual)
        {
            var paddle = new GameObject("Paddle");
            paddle.transform.SetParent(visual, false);
            paddle.transform.localPosition = new Vector3(0f, 0.55f, 0.35f);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.transform.SetParent(paddle.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            shaft.transform.localScale = new Vector3(0.05f, 0.85f, 0.05f);
            GameMaterials.ApplyTo(shaft, "Wood");
            Object.Destroy(shaft.GetComponent<Collider>());

            foreach (float side in new[] { -1f, 1f })
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blade.transform.SetParent(paddle.transform, false);
                blade.transform.localPosition = new Vector3(side * 0.9f, 0f, 0f);
                blade.transform.localScale = new Vector3(0.12f, 0.34f, 0.22f);
                GameMaterials.ApplyTo(blade, "KayakTrim");
                Object.Destroy(blade.GetComponent<Collider>());
            }

            paddle.AddComponent<PaddleSwing>();
        }

        static void BuildTim(Transform visual, GameObject root)
        {
            // Тапок-Тим: an old slipper sitting behind the cockpit.
            var tim = new GameObject("Tim");
            tim.transform.SetParent(visual, false);
            tim.transform.localPosition = new Vector3(0f, 0.32f, -0.85f);
            tim.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // faces backward? no — looks forward past the player

            var sole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sole.name = "Sole";
            sole.transform.SetParent(tim.transform, false);
            sole.transform.localPosition = Vector3.zero;
            sole.transform.localScale = new Vector3(0.28f, 0.09f, 0.62f);
            GameMaterials.ApplyTo(sole, "Slipper");
            Object.Destroy(sole.GetComponent<Collider>());

            var toe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            toe.name = "Toe";
            toe.transform.SetParent(tim.transform, false);
            toe.transform.localPosition = new Vector3(0f, 0.1f, 0.18f);
            toe.transform.localScale = new Vector3(0.26f, 0.2f, 0.34f);
            GameMaterials.ApplyTo(toe, "Slipper");
            Object.Destroy(toe.GetComponent<Collider>());

            foreach (float side in new[] { -1f, 1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye";
                eye.transform.SetParent(tim.transform, false);
                eye.transform.localPosition = new Vector3(side * 0.07f, 0.24f, 0.16f);
                eye.transform.localScale = new Vector3(0.09f, 0.11f, 0.09f);
                GameMaterials.ApplyTo(eye, "EyeWhite");
                Object.Destroy(eye.GetComponent<Collider>());

                var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.name = "Pupil";
                pupil.transform.SetParent(eye.transform, false);
                pupil.transform.localPosition = new Vector3(0f, 0.05f, 0.35f);
                pupil.transform.localScale = new Vector3(0.42f, 0.38f, 0.42f);
                GameMaterials.ApplyTo(pupil, "EyePupil");
                Object.Destroy(pupil.GetComponent<Collider>());
            }

            root.AddComponent<PassengerTim>().TimTransform = tim.transform;
        }
    }

    /// <summary>Swings the paddle left/right while thrusting.</summary>
    public sealed class PaddleSwing : MonoBehaviour
    {
        BoatInput _input;
        float _phase;

        void Start()
        {
            _input = GetComponentInParent<BoatInput>();
        }

        void Update()
        {
            if (_input == null)
                return;
            float speed = Mathf.Abs(_input.Thrust) > 0.2f ? 3.6f : 0.7f;
            _phase += Time.deltaTime * speed;
            float roll = Mathf.Sin(_phase) * 32f * Mathf.Clamp(Mathf.Abs(_input.Thrust) + 0.25f, 0.25f, 1f);
            float pitch = Mathf.Cos(_phase) * 12f;
            transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }

    /// <summary>Wobbles Тапок-Тим, extra hard after collisions.</summary>
    public sealed class PassengerTim : MonoBehaviour
    {
        public Transform TimTransform;
        float _shake;

        public void ReactToHit() => _shake = 1f;

        void Update()
        {
            if (TimTransform == null)
                return;
            _shake = Mathf.Max(0f, _shake - Time.deltaTime * 1.4f);
            float idleWobble = Mathf.Sin(Time.time * 1.9f) * 3f;
            float panic = Mathf.Sin(Time.time * 26f) * 22f * _shake;
            TimTransform.localRotation = Quaternion.Euler(
                Mathf.Sin(Time.time * 1.4f) * 2.5f + panic * 0.4f,
                180f + idleWobble * 0.5f,
                idleWobble + panic);
            TimTransform.localPosition = new Vector3(0f, 0.32f + Mathf.Abs(Mathf.Sin(Time.time * 13f)) * 0.08f * _shake, -0.85f);
        }
    }

    /// <summary>Simple water splash particles at the paddle while thrusting.</summary>
    public sealed class PaddleSplash : MonoBehaviour
    {
        public Transform Visual;
        ParticleSystem _particles;
        BoatInput _input;

        void Start()
        {
            _input = GetComponent<BoatInput>();

            var go = new GameObject("SplashParticles");
            go.transform.SetParent(Visual != null ? Visual : transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0.4f);
            _particles = go.AddComponent<ParticleSystem>();

            var main = _particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.7f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new Color(0.75f, 0.9f, 0.95f, 0.8f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = _particles.emission;
            emission.rateOverTime = 0f;

            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GameMaterials.Get("WaterSplash");

            _particles.Play();
        }

        void Update()
        {
            if (_particles == null || _input == null)
                return;
            var emission = _particles.emission;
            emission.rateOverTime = Mathf.Abs(_input.Thrust) > 0.2f ? 26f : 4f;
        }
    }
}
