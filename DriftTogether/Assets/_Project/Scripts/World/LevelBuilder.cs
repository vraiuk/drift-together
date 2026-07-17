using System.Collections.Generic;
using DriftTogether.Core;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>
    /// Builds the whole «Туманный лес» river level procedurally at scene load:
    /// four spline branches (start, quiet channel, noisy stream, final stretch),
    /// water, banks, obstacles, mushrooms, campfire pier, fork gates, finish.
    /// </summary>
    public sealed class LevelBuilder : MonoBehaviour
    {
        public RiverFlow Flow { get; private set; }
        public RiverSpline UpperSpline { get; private set; }
        public RiverSpline QuietSpline { get; private set; }
        public RiverSpline NoisySpline { get; private set; }
        public RiverSpline LowerSpline { get; private set; }

        public Vector3 KayakStartPosition => new Vector3(0f, 0f, 4f);
        public Quaternion KayakStartRotation => Quaternion.identity;

        public Campfire Campfire { get; private set; }
        public FinishZone Finish { get; private set; }
        public NervousZone NoisyZone { get; private set; }
        public readonly List<RouteGate> RouteGates = new List<RouteGate>();
        public readonly List<CheckpointZone> CheckpointZones = new List<CheckpointZone>();
        public readonly List<Collectible> Mushrooms = new List<Collectible>();
        public Vector3 CampfireRespawnPosition { get; private set; }
        public Quaternion CampfireRespawnRotation { get; private set; }

        static readonly Vector3 ForkPoint = new Vector3(0f, 0f, 230f);
        static readonly Vector3 JoinPoint = new Vector3(0f, 0f, 480f);

        const float UpperHalfWidth = 7f;
        const float QuietHalfWidth = 4.5f;
        const float NoisyHalfWidth = 8f;
        const float LowerHalfWidth = 7f;

        Transform _root;

        public void Build()
        {
            _root = new GameObject("Level").transform;
            _root.SetParent(transform, false);

            BuildSplinesAndFlow();
            BuildWaterAndBanks();
            BuildForkIsland();
            BuildObstacles();
            BuildMushrooms();
            BuildRouteGates();
            BuildCheckpoints();
            BuildCampfire();
            BuildFinish();
            BuildTrees();
            BuildLighting();
        }

        void BuildSplinesAndFlow()
        {
            UpperSpline = new RiverSpline(new[]
            {
                new Vector3(0, 0, -25), new Vector3(0, 0, 0), new Vector3(0, 0, 45),
                new Vector3(8, 0, 85), new Vector3(-6, 0, 130), new Vector3(9, 0, 180),
                new Vector3(2, 0, 212), ForkPoint
            });

            QuietSpline = new RiverSpline(new[]
            {
                ForkPoint, new Vector3(-14, 0, 258), new Vector3(-28, 0, 298),
                new Vector3(-24, 0, 338), new Vector3(-33, 0, 378), new Vector3(-20, 0, 420),
                new Vector3(-8, 0, 455), JoinPoint
            });

            NoisySpline = new RiverSpline(new[]
            {
                ForkPoint, new Vector3(13, 0, 256), new Vector3(24, 0, 292),
                new Vector3(17, 0, 330), new Vector3(28, 0, 370), new Vector3(14, 0, 412),
                new Vector3(6, 0, 452), JoinPoint
            });

            LowerSpline = new RiverSpline(new[]
            {
                JoinPoint, new Vector3(3, 0, 512), new Vector3(6, 0, 535),
                new Vector3(0, 0, 562), new Vector3(-8, 0, 600), new Vector3(6, 0, 640),
                new Vector3(-5, 0, 680), new Vector3(2, 0, 715), new Vector3(0, 0, 750),
                new Vector3(0, 0, 780)
            });

            Flow = new RiverFlow();
            Flow.AddBranch(new FlowBranch { Spline = UpperSpline, BaseCurrent = 1.1f, HalfWidth = UpperHalfWidth });
            Flow.AddBranch(new FlowBranch { Spline = QuietSpline, BaseCurrent = 0.75f, HalfWidth = QuietHalfWidth });
            var noisy = new FlowBranch { Spline = NoisySpline, BaseCurrent = 2.0f, HalfWidth = NoisyHalfWidth };
            noisy.FastZones.Add((40f, 200f, 1.35f));
            Flow.AddBranch(noisy);
            var lower = new FlowBranch { Spline = LowerSpline, BaseCurrent = 1.2f, HalfWidth = LowerHalfWidth };
            lower.FastZones.Add((95f, 245f, 2.0f)); // final rapids
            Flow.AddBranch(lower);
        }

        void BuildWaterAndBanks()
        {
            CreateWater("WaterUpper", UpperSpline, UpperHalfWidth);
            CreateWater("WaterQuiet", QuietSpline, QuietHalfWidth);
            CreateWater("WaterNoisy", NoisySpline, NoisyHalfWidth);
            CreateWater("WaterLower", LowerSpline, LowerHalfWidth);

            CreateBanksAndWalls(UpperSpline, UpperHalfWidth);
            CreateBanksAndWalls(QuietSpline, QuietHalfWidth);
            CreateBanksAndWalls(NoisySpline, NoisyHalfWidth);
            CreateBanksAndWalls(LowerSpline, LowerHalfWidth);

            // Dark forest floor far below the fog.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ForestFloor";
            ground.transform.SetParent(_root, false);
            ground.transform.position = new Vector3(0f, -0.5f, 380f);
            ground.transform.localScale = new Vector3(60f, 1f, 110f);
            GameMaterials.ApplyTo(ground, "ForestFloor");
            Destroy(ground.GetComponent<Collider>());
        }

        void CreateWater(string name, RiverSpline spline, float halfWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.mesh = MeshFactory.BuildRibbon(spline.Samples, halfWidth + 1.2f, 0f);
            renderer.sharedMaterial = GameMaterials.Get("Water");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<WaterSurface>();
        }

        void CreateBanksAndWalls(RiverSpline spline, float halfWidth)
        {
            foreach (bool right in new[] { false, true })
            {
                var bank = new GameObject(right ? "BankR" : "BankL");
                bank.transform.SetParent(_root, false);
                var filter = bank.AddComponent<MeshFilter>();
                var renderer = bank.AddComponent<MeshRenderer>();
                filter.mesh = MeshFactory.BuildBankStrip(spline.Samples, halfWidth + 0.7f, 9f, 2.2f, right);
                renderer.sharedMaterial = GameMaterials.Get("Bank");
            }

            // Invisible soft wall colliders that keep the kayak inside the river.
            var walls = new GameObject("Walls");
            walls.transform.SetParent(_root, false);
            var samples = spline.Samples;
            for (int i = 0; i < samples.Count - 2; i += 2)
            {
                Vector3 a = samples[i];
                Vector3 b = samples[Mathf.Min(i + 2, samples.Count - 1)];
                Vector3 mid = (a + b) * 0.5f;
                if ((mid - ForkPoint).magnitude < 18f || (mid - JoinPoint).magnitude < 18f)
                    continue;

                Vector3 fwd = b - a;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f)
                    continue;
                Quaternion rot = Quaternion.LookRotation(fwd.normalized);
                float len = fwd.magnitude + 1.2f;
                Vector3 rightDir = Vector3.Cross(Vector3.up, fwd.normalized);

                foreach (float side in new[] { -1f, 1f })
                {
                    var wall = new GameObject("Wall");
                    wall.transform.SetParent(walls.transform, false);
                    wall.transform.SetPositionAndRotation(
                        mid + rightDir * side * (halfWidth + 1.1f) + Vector3.up * 0.8f, rot);
                    var box = wall.AddComponent<BoxCollider>();
                    box.size = new Vector3(1.2f, 3f, len);
                    wall.AddComponent<SoftSurface>();
                }
            }
        }

        void BuildForkIsland()
        {
            // A cluster of mossy rocks splitting the river at the fork.
            var island = new GameObject("ForkIsland");
            island.transform.SetParent(_root, false);
            Vector3 basePos = new Vector3(0f, 0f, 248f);
            (Vector3 offset, Vector3 scale)[] rocks =
            {
                (new Vector3(0f, 0.2f, 0f), new Vector3(6f, 3.2f, 9f)),
                (new Vector3(-2.5f, 0f, 4f), new Vector3(3.5f, 2.2f, 4f)),
                (new Vector3(2.5f, 0f, 5f), new Vector3(3f, 2f, 4.5f)),
                (new Vector3(0f, 0f, -4f), new Vector3(4f, 1.8f, 3f))
            };
            foreach (var (offset, scale) in rocks)
                SpawnRock(island.transform, basePos + offset, scale, softTop: false);

            // Route sign posts with glowing hints.
            SpawnSign(new Vector3(-3.5f, 0f, 240f), "SignQuiet", new Color(0.45f, 0.95f, 0.75f));
            SpawnSign(new Vector3(3.5f, 0f, 240f), "SignNoisy", new Color(0.95f, 0.75f, 0.4f));
        }

        void SpawnSign(Vector3 pos, string name, Color glow)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = name;
            post.transform.SetParent(_root, false);
            post.transform.position = pos + new Vector3(0f, 0.8f, 0f);
            post.transform.localScale = new Vector3(0.16f, 0.8f, 0.16f);
            GameMaterials.ApplyTo(post, "Wood");
            Destroy(post.GetComponent<Collider>());

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = name + "Lamp";
            lamp.transform.SetParent(post.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            lamp.transform.localScale = new Vector3(1.6f, 0.32f, 1.6f);
            GameMaterials.ApplyTo(lamp, name == "SignQuiet" ? "MushroomCap" : "FireGlow");
            Destroy(lamp.GetComponent<Collider>());

            var lightGo = new GameObject("SignLight");
            lightGo.transform.SetParent(post.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = glow;
            light.intensity = 1.6f;
            light.range = 7f;
        }

        void SpawnRock(Transform parent, Vector3 pos, Vector3 scale, bool softTop)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, false);
            rock.transform.position = new Vector3(pos.x, pos.y + scale.y * 0.18f, pos.z);
            rock.transform.rotation = Quaternion.Euler(
                Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            rock.transform.localScale = scale;
            GameMaterials.ApplyTo(rock, "Rock");
            if (softTop)
                rock.AddComponent<SoftSurface>();
        }

        void SpawnLog(Vector3 pos, float yaw, float length)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Log";
            log.transform.SetParent(_root, false);
            log.transform.position = new Vector3(pos.x, 0.12f, pos.z);
            log.transform.rotation = Quaternion.Euler(0f, yaw, 90f);
            log.transform.localScale = new Vector3(0.55f, length * 0.5f, 0.55f);
            GameMaterials.ApplyTo(log, "Wood");
        }

        void SpawnShallow(Vector3 pos, float radius)
        {
            var shallow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shallow.name = "Shallow";
            shallow.transform.SetParent(_root, false);
            shallow.transform.position = new Vector3(pos.x, -0.04f, pos.z);
            shallow.transform.localScale = new Vector3(radius * 2f, 0.09f, radius * 2f);
            GameMaterials.ApplyTo(shallow, "Sand");
            shallow.AddComponent<SoftSurface>();
        }

        void BuildObstacles()
        {
            // Upper river: gentle introduction.
            SpawnRock(_root, new Vector3(3f, 0f, 96f), new Vector3(2f, 1.6f, 2.2f), false);
            SpawnRock(_root, new Vector3(-5f, 0f, 122f), new Vector3(2.6f, 1.4f, 2.4f), false);
            SpawnRock(_root, new Vector3(6f, 0f, 152f), new Vector3(2.2f, 1.8f, 2f), false);
            SpawnRock(_root, new Vector3(-2f, 0f, 188f), new Vector3(1.8f, 1.3f, 1.8f), false);
            SpawnShallow(new Vector3(5.8f, 0f, 112f), 2.6f);
            SpawnShallow(new Vector3(-6.2f, 0f, 170f), 2.2f);

            // Quiet channel: logs and hanging roots, fewer dangers.
            SpawnLog(new Vector3(-21f, 0f, 312f), 25f, 5.5f);
            SpawnLog(new Vector3(-31f, 0f, 362f), -20f, 5f);
            SpawnLog(new Vector3(-16f, 0f, 436f), 40f, 4.5f);
            for (int i = 0; i < 7; i++)
            {
                float d = 40f + i * 30f;
                Vector3 p = QuietSpline.PointAtDistance(d);
                Vector3 tangent = QuietSpline.TangentAt(QuietSpline.ClosestSampleIndex(p));
                Vector3 side = Vector3.Cross(Vector3.up, tangent) * (i % 2 == 0 ? 1f : -1f);
                SpawnRoot(p + side * (QuietHalfWidth - 0.4f));
            }

            // Noisy stream: rocks and mini rapids.
            SpawnRock(_root, new Vector3(14f, 0f, 272f), new Vector3(2.2f, 1.7f, 2f), false);
            SpawnRock(_root, new Vector3(23f, 0f, 288f), new Vector3(2.8f, 2f, 2.4f), false);
            SpawnRock(_root, new Vector3(19f, 0f, 312f), new Vector3(2f, 1.5f, 2.2f), false);
            SpawnRock(_root, new Vector3(27f, 0f, 348f), new Vector3(2.4f, 1.8f, 2f), false);
            SpawnRock(_root, new Vector3(23f, 0f, 384f), new Vector3(2.6f, 1.6f, 2.6f), false);
            SpawnRock(_root, new Vector3(16f, 0f, 398f), new Vector3(1.8f, 1.4f, 1.8f), false);
            SpawnRock(_root, new Vector3(9f, 0f, 432f), new Vector3(2.2f, 1.5f, 2f), false);
            SpawnShallow(new Vector3(28f, 0f, 332f), 2.8f);

            // Final rapids.
            SpawnRock(_root, new Vector3(3f, 0f, 588f), new Vector3(2.2f, 1.7f, 2.2f), false);
            SpawnRock(_root, new Vector3(-6f, 0f, 612f), new Vector3(2.6f, 1.9f, 2.2f), false);
            SpawnRock(_root, new Vector3(5f, 0f, 636f), new Vector3(2f, 1.5f, 2.4f), false);
            SpawnRock(_root, new Vector3(-3f, 0f, 662f), new Vector3(2.4f, 1.7f, 2f), false);
            SpawnRock(_root, new Vector3(2f, 0f, 694f), new Vector3(1.9f, 1.4f, 2f), false);
            SpawnShallow(new Vector3(6.5f, 0f, 624f), 2.4f);
        }

        void SpawnRoot(Vector3 pos)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Root";
            root.transform.SetParent(_root, false);
            root.transform.position = new Vector3(pos.x, 1.6f, pos.z);
            root.transform.rotation = Quaternion.Euler(Random.Range(35f, 60f),
                Random.Range(0f, 360f), 0f);
            root.transform.localScale = new Vector3(0.3f, 2.2f, 0.3f);
            GameMaterials.ApplyTo(root, "Wood");
            root.AddComponent<SoftSurface>();
        }

        void BuildMushrooms()
        {
            (int id, Vector3 pos)[] mushrooms =
            {
                (1, new Vector3(1.5f, 0f, 62f)),
                (2, new Vector3(-4f, 0f, 148f)),
                (3, new Vector3(3f, 0f, 206f)),
                (4, new Vector3(-26f, 0f, 306f)),
                (5, new Vector3(-30f, 0f, 374f)),
                (6, new Vector3(22f, 0f, 298f)),
                (7, new Vector3(25f, 0f, 362f))
            };
            foreach (var (id, pos) in mushrooms)
                Mushrooms.Add(SpawnMushroom(id, pos));
        }

        Collectible SpawnMushroom(int id, Vector3 pos)
        {
            var holder = new GameObject($"Mushroom_{id}");
            holder.transform.SetParent(_root, false);
            holder.transform.position = new Vector3(pos.x, 0.55f, pos.z);

            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.transform.SetParent(holder.transform, false);
            stem.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            stem.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
            GameMaterials.ApplyTo(stem, "MushroomStem");
            Destroy(stem.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.transform.SetParent(holder.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            cap.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            GameMaterials.ApplyTo(cap, "MushroomCap");
            Destroy(cap.GetComponent<Collider>());

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 1f, 0.8f);
            light.intensity = 2.2f;
            light.range = 6.5f;

            var trigger = holder.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.6f;

            var collectible = holder.AddComponent<Collectible>();
            collectible.Id = id;
            return collectible;
        }

        void BuildRouteGates()
        {
            RouteGates.Add(SpawnRouteGate(RiverRoute.QuietChannel, new Vector3(-16f, 0f, 265f),
                Quaternion.LookRotation(new Vector3(-0.4f, 0f, 1f)), QuietHalfWidth * 2f + 4f));
            RouteGates.Add(SpawnRouteGate(RiverRoute.NoisyStream, new Vector3(15f, 0f, 262f),
                Quaternion.LookRotation(new Vector3(0.35f, 0f, 1f)), NoisyHalfWidth * 2f + 4f));
        }

        RouteGate SpawnRouteGate(RiverRoute route, Vector3 pos, Quaternion rot, float width)
        {
            var go = new GameObject($"RouteGate_{route}");
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 1f, rot);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, 4f, 2f);
            var gate = go.AddComponent<RouteGate>();
            gate.Route = route;
            return gate;
        }

        void BuildCheckpoints()
        {
            SpawnCheckpoint(new Vector3(0f, 0f, 208f), UpperSpline);
            SpawnCheckpoint(new Vector3(-24f, 0f, 340f), QuietSpline);
            SpawnCheckpoint(new Vector3(18f, 0f, 334f), NoisySpline);
            SpawnCheckpoint(new Vector3(0f, 0f, 490f), LowerSpline);
            SpawnCheckpoint(new Vector3(0f, 0f, 566f), LowerSpline);
        }

        void SpawnCheckpoint(Vector3 pos, RiverSpline spline)
        {
            int idx = spline.ClosestSampleIndex(pos);
            Vector3 tangent = spline.TangentAt(idx);
            Vector3 center = spline.PointAt(idx);

            var go = new GameObject($"Checkpoint_{CheckpointZones.Count}");
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 1f, center.z),
                Quaternion.LookRotation(tangent));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(24f, 5f, 2.5f);

            var zone = go.AddComponent<CheckpointZone>();
            zone.RespawnPosition = new Vector3(center.x, 0f, center.z);
            zone.RespawnRotation = Quaternion.LookRotation(tangent);
            CheckpointZones.Add(zone);
        }

        void BuildCampfire()
        {
            // Pier on the east bank shortly after the routes join.
            Vector3 dockRoot = new Vector3(8.5f, 0.35f, 534f);
            var pier = new GameObject("Pier");
            pier.transform.SetParent(_root, false);

            for (int i = 0; i < 4; i++)
            {
                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.transform.SetParent(pier.transform, false);
                plank.transform.position = dockRoot + new Vector3(-i * 1.15f, 0f, 0f);
                plank.transform.localScale = new Vector3(1.05f, 0.14f, 3.2f);
                GameMaterials.ApplyTo(plank, "Wood");
                plank.AddComponent<SoftSurface>();
            }

            // Campfire on the bank.
            Vector3 firePos = new Vector3(11.5f, 0.9f, 534f);
            var fire = new GameObject("Campfire");
            fire.transform.SetParent(_root, false);
            fire.transform.position = firePos;

            for (int i = 0; i < 5; i++)
            {
                var logPiece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                logPiece.transform.SetParent(fire.transform, false);
                logPiece.transform.localPosition = Vector3.zero;
                logPiece.transform.localRotation = Quaternion.Euler(65f, i * 72f, 0f);
                logPiece.transform.localScale = new Vector3(0.18f, 0.7f, 0.18f);
                GameMaterials.ApplyTo(logPiece, "Wood");
                Destroy(logPiece.GetComponent<Collider>());
            }

            var flame = new GameObject("Flame");
            flame.transform.SetParent(fire.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            var flameFilter = flame.AddComponent<MeshFilter>();
            flameFilter.mesh = MeshFactory.BuildCone(0.45f, 1.1f, 8);
            var flameRenderer = flame.AddComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = GameMaterials.Get("FireGlow");

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(fire.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.intensity = 3f;
            light.range = 18f;
            lightGo.AddComponent<FlickerLight>();

            // Interaction zone covering the pier and nearby water.
            var zone = new GameObject("CampfireZone");
            zone.transform.SetParent(_root, false);
            zone.transform.position = new Vector3(5.5f, 1f, 534f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(9f, 4f, 8f);
            Campfire = zone.AddComponent<Campfire>();

            CampfireRespawnPosition = new Vector3(4.5f, 0f, 534f);
            CampfireRespawnRotation = Quaternion.LookRotation(Vector3.forward);
        }

        void BuildFinish()
        {
            Vector3 pos = new Vector3(0f, 0f, 752f);

            foreach (float side in new[] { -1f, 1f })
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "FinishPillar";
                pillar.transform.SetParent(_root, false);
                pillar.transform.position = new Vector3(side * (LowerHalfWidth + 0.5f), 2.2f, pos.z);
                pillar.transform.localScale = new Vector3(0.7f, 2.2f, 0.7f);
                GameMaterials.ApplyTo(pillar, "Wood");
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "FinishBeam";
            beam.transform.SetParent(_root, false);
            beam.transform.position = new Vector3(0f, 4.6f, pos.z);
            beam.transform.localScale = new Vector3((LowerHalfWidth + 1f) * 2f, 0.5f, 0.6f);
            GameMaterials.ApplyTo(beam, "FinishGlow");
            Destroy(beam.GetComponent<Collider>());

            var lightGo = new GameObject("FinishLight");
            lightGo.transform.SetParent(_root, false);
            lightGo.transform.position = new Vector3(0f, 4f, pos.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.5f);
            light.intensity = 3.2f;
            light.range = 22f;

            var zone = new GameObject("FinishZone");
            zone.transform.SetParent(_root, false);
            zone.transform.position = new Vector3(0f, 1f, pos.z + 2f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(LowerHalfWidth * 2f + 4f, 5f, 3f);
            Finish = zone.AddComponent<FinishZone>();

            // Nervous zone covering the whole noisy stream.
            var nz = new GameObject("NervousZone");
            nz.transform.SetParent(_root, false);
            nz.transform.position = new Vector3(20f, 1f, 355f);
            var nbox = nz.AddComponent<BoxCollider>();
            nbox.isTrigger = true;
            nbox.size = new Vector3(38f, 6f, 200f);
            NoisyZone = nz.AddComponent<NervousZone>();
        }

        void BuildTrees()
        {
            var trees = new GameObject("Trees");
            trees.transform.SetParent(_root, false);
            var splines = new (RiverSpline spline, float halfWidth)[]
            {
                (UpperSpline, UpperHalfWidth), (QuietSpline, QuietHalfWidth),
                (NoisySpline, NoisyHalfWidth), (LowerSpline, LowerHalfWidth)
            };

            var rng = new System.Random(777);
            foreach (var (spline, halfWidth) in splines)
            {
                for (float d = 6f; d < spline.Length - 6f; d += 13f)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (rng.NextDouble() < 0.25)
                            continue;
                        Vector3 p = spline.PointAtDistance(d);
                        int idx = spline.ClosestSampleIndex(p);
                        Vector3 rightDir = Vector3.Cross(Vector3.up, spline.TangentAt(idx));
                        float off = halfWidth + 4f + (float)rng.NextDouble() * 7f;
                        Vector3 pos = p + rightDir * side * off;
                        SpawnTree(trees.transform, new Vector3(pos.x, 0f, pos.z), rng);
                    }
                }
            }
        }

        void SpawnTree(Transform parent, Vector3 pos, System.Random rng)
        {
            float h = 3.5f + (float)rng.NextDouble() * 3f;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree";
            trunk.transform.SetParent(parent, false);
            trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.4f, h * 0.5f, 0.4f);
            GameMaterials.ApplyTo(trunk, "TreeTrunk");
            Destroy(trunk.GetComponent<Collider>());

            int blobs = 2 + rng.Next(2);
            for (int i = 0; i < blobs; i++)
            {
                var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blob.transform.SetParent(trunk.transform, false);
                float s = 4.2f + (float)rng.NextDouble() * 2.2f;
                blob.transform.localPosition = new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * 1.6f,
                    0.85f + i * 0.35f,
                    ((float)rng.NextDouble() - 0.5f) * 1.6f);
                blob.transform.localScale = new Vector3(s, s * 0.7f, s);
                GameMaterials.ApplyTo(blob, "Foliage");
                Destroy(blob.GetComponent<Collider>());
            }
        }

        void BuildLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.026f;
            RenderSettings.fogColor = new Color(0.16f, 0.24f, 0.26f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.48f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.3f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.14f, 0.16f);

            var sun = new GameObject("Moon");
            sun.transform.SetParent(_root, false);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.75f, 0.8f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.Soft;
        }
    }
}
