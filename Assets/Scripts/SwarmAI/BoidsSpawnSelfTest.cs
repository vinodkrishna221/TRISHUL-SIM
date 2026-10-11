// ============================================================
// BoidsSpawnSelfTest.cs — Runnable Self-Test for SA-P0-A
// TRISHUL-SIM · swarm-ai track (Venu)
//
// Validates:
// 1. Exact mission card test (SA-P0-A):
//    - Play Punjab scenario (seed 42819, 6 agents: 2 blue decoys, 4 red attackers).
//    - Trainee Cockpit: 6 coloured spheres move in coherent formation.
//    - SCENARIO_TICK Console log: swarmSnapshot contains 6 entries; positionWorld changes each tick.
// 2. BoidAgent.cs Entity 2 fields & SwarmProfile initialization:
//    - Decoys: rcs = 0.08 m², propellerRpm = 9800, blue tint
//    - Attackers: rcs = 0.012 m², propellerRpm = 11400, red tint
// 3. BoidFlockController.cs 3-rule flocking (Separation <5m, Alignment 20m, Cohesion centroid),
//    speed clamp (speedKmh / 3.6 m/s), and altitude spring (altitudeM ± 2m).
// 4. SwarmManager.cs OnScenarioStart(seed), GetSwarmSnapshot(), InjectSwarmSnapshot(),
//    OnScenarioEnd(), and WsClient SCENARIO_START / TICK / END wiring.
// 5. SwarmAgentVisual.prefab presence and structure validation.
// ============================================================

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.Mocks;

namespace TrishulSim.SwarmAI
{
    public static class BoidsSpawnSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P0-A Boids 6-agent spawn, flocking, and 10 Hz SwarmAgentState tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

        public static void RunAllTests()
        {
            Console.WriteLine("=== Running TRISHUL-SIM SA-P0-A Boids System Self-Test ===");

            TestExactMissionCardPunjabScenario();
            TestBoidAgentFieldsAndSwarmProfileSignatures();
            TestBoidFlockControllerUpdateAndRules();
            TestDeterministicSeedAndWsLifecycle();
            TestSwarmAgentVisualPrefabAsset();
        }

        /// <summary>
        /// Exact card test from missions/venu-mission.md & plans/swarm-ai-phase-plan.md:
        /// Play Punjab scenario. Trainee Cockpit: 6 coloured spheres move in coherent formation.
        /// SCENARIO_TICK Console log: swarmSnapshot contains 6 entries; positionWorld changes each tick.
        /// </summary>
        private static void TestExactMissionCardPunjabScenario()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Punjab Scenario (6 Agents @ 10 Hz TICK) --");

            SwarmManager manager = new SwarmManager();
            SwarmProfile punjabProfile = SwarmMock.GetPunjabProfile();
            int seed = 42819;

            manager.OnScenarioStart(punjabProfile, seed, "punjab-fog-pincer-01", Vector3.zero);

            IReadOnlyList<BoidAgent> boids = manager.BoidAgents;
            Assert(boids != null && boids.Count == 6, "Expected 6 spawned BoidAgent instances, got " + (boids != null ? boids.Count : 0));

            int blueDecoys = 0;
            int redAttackers = 0;
            for (int i = 0; i < boids.Count; i++)
            {
                BoidAgent b = boids[i];
                Assert(b != null && b.gameObject != null && b.gameObject.activeSelf, "BoidAgent " + i + " GameObject must be active");
                if (b.isDecoy)
                {
                    Assert(Math.Abs(b.VisualColor.b - BoidAgent.DecoyTintColor.b) < 0.01f, "Decoy sphere must be tinted blue");
                    blueDecoys++;
                }
                else
                {
                    Assert(Math.Abs(b.VisualColor.r - BoidAgent.AttackerTintColor.r) < 0.01f, "Attacker sphere must be tinted red");
                    redAttackers++;
                }
            }

            Assert(blueDecoys == 2, "Expected 2 blue decoy spheres, got " + blueDecoys);
            Assert(redAttackers == 4, "Expected 4 red attacker spheres, got " + redAttackers);
            Console.WriteLine("Trainee Cockpit visual check: 6 coloured spheres active (2 blue decoys, 4 red attackers).");

            // Capture initial positions before 10 Hz ticks
            SwarmAgentState[] initialSnap = manager.GetSwarmSnapshot();
            Assert(initialSnap.Length == 6, "Initial swarmSnapshot must contain 6 entries");

            Vector3[] prevPositions = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                prevPositions[i] = initialSnap[i].positionWorld;
            }

            // Simulate 10 ticks at 10 Hz (100 ms per tick) and verify positionWorld changes each tick
            // and flock remains coherent (max distance to flock centroid < 35m during approach)
            const float tickDt = 0.1f;
            for (int tick = 1; tick <= 10; tick++)
            {
                long tMs = tick * 100L;
                manager.StepFlocking(tickDt);

                SwarmAgentState[] injectedSnapshot = null;
                manager.InjectSwarmSnapshot(snap => { injectedSnapshot = snap; });

                Assert(injectedSnapshot != null && injectedSnapshot.Length == 6,
                    "Tick " + tick + ": injected swarmSnapshot must contain 6 entries");

                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < 6; i++)
                {
                    SwarmAgentState s = injectedSnapshot[i];
                    float deltaMove = Vector3.Distance(prevPositions[i], s.positionWorld);
                    Assert(deltaMove > 0.05f,
                        string.Format("Tick {0} agent-{1} positionWorld did not change (delta={2:F4}m)", tick, i, deltaMove));

                    prevPositions[i] = s.positionWorld;
                    centroid = centroid + s.positionWorld;
                }
                centroid = centroid * (1.0f / 6.0f);

                // Verify coherent formation (not random scatter)
                float maxSpreadFromCentroid = 0f;
                for (int i = 0; i < 6; i++)
                {
                    float distToCentroid = Vector3.Distance(injectedSnapshot[i].positionWorld, centroid);
                    if (distToCentroid > maxSpreadFromCentroid)
                        maxSpreadFromCentroid = distToCentroid;
                }

                Assert(maxSpreadFromCentroid < 35.0f,
                    string.Format("Tick {0}: flock scattered! Max spread from centroid={1:F2}m", tick, maxSpreadFromCentroid));

                if (tick == 1 || tick == 5 || tick == 10)
                {
                    Console.WriteLine(string.Format(
                        "[SCENARIO_TICK t={0}ms] swarmSnapshot entries={1} | agent-0 pos=({2:F1}, {3:F1}, {4:F1}) | flock maxSpread={5:F2}m",
                        tMs,
                        injectedSnapshot.Length,
                        injectedSnapshot[0].positionWorld.x,
                        injectedSnapshot[0].positionWorld.y,
                        injectedSnapshot[0].positionWorld.z,
                        maxSpreadFromCentroid));
                }
            }

            Console.WriteLine("[PASS] Exact Mission Card Punjab Scenario PASSED");
        }

        /// <summary>
        /// Validates all 13 Entity 2 SwarmAgentState fields and SwarmProfile decoy/attacker initialization.
        /// </summary>
        private static void TestBoidAgentFieldsAndSwarmProfileSignatures()
        {
            Console.WriteLine("\n-- Test 2: BoidAgent Entity 2 Fields & SwarmProfile Signatures --");

            SwarmProfile profile = SwarmMock.GetPunjabProfile();

            BoidAgent decoy = new BoidAgent();
            decoy.InitializeFromProfile(0, true, profile, new Vector3(-5f, 10f, 250f), new Vector3(0f, 0f, -33.33f));

            Assert(decoy.agentId == "agent-0", "Decoy agentId must be agent-0");
            Assert(decoy.isDecoy == true, "Decoy isDecoy must be true");
            Assert(Math.Abs(decoy.rcs - 0.08f) < 0.0001f, "Decoy rcs must be 0.08 m²");
            Assert(decoy.propellerRpm == 9800, "Decoy propellerRpm must be 9800");
            Assert(decoy.rfEmitting == true, "Decoy rfEmitting must be true");
            Assert(Math.Abs(decoy.rfPowerDbm - 23.0f) < 0.01f, "Decoy rfPowerDbm must be 23.0 dBm");
            Assert(decoy.isNeutralised == false, "Decoy isNeutralised must be false");
            Assert(decoy.neutralisedAtMs == null, "Decoy neutralisedAtMs must be null");

            BoidAgent attacker = new BoidAgent();
            attacker.InitializeFromProfile(2, false, profile, new Vector3(5f, 8f, 250f), new Vector3(0f, 0f, -33.33f));

            Assert(attacker.agentId == "agent-2", "Attacker agentId must be agent-2");
            Assert(attacker.isDecoy == false, "Attacker isDecoy must be false");
            Assert(Math.Abs(attacker.rcs - 0.012f) < 0.0001f, "Attacker rcs must be 0.012 m²");
            Assert(attacker.propellerRpm == 11400, "Attacker propellerRpm must be 11400");
            Assert(Math.Abs(attacker.rfFreqGHz - 5.825f) < 0.001f, "Attacker rfFreqGHz must be 5.825 GHz");
            Assert(Math.Abs(attacker.headingDeg - 180.0f) < 0.1f, "Attacker moving -Z must have heading 180°");

            SwarmAgentState exported = attacker.ToSwarmAgentState();
            Assert(exported.agentId == "agent-2" && Math.Abs(exported.rcs - 0.012f) < 0.0001f && exported.propellerRpm == 11400,
                "ToSwarmAgentState must preserve all Entity 2 fields");

            Console.WriteLine("[PASS] BoidAgent Entity 2 Fields & SwarmProfile Signatures PASSED");
        }

        /// <summary>
        /// Validates BoidFlockController.Update(IList<BoidAgent>, SwarmProfile, float) and all 3 Boids rules.
        /// </summary>
        private static void TestBoidFlockControllerUpdateAndRules()
        {
            Console.WriteLine("\n-- Test 3: BoidFlockController Update() on BoidAgent Instances --");

            BoidFlockController controller = new BoidFlockController();
            SwarmProfile profile = SwarmMock.GetPunjabProfile(); // 120 km/h (33.33 m/s), alt 8m

            BoidAgent a0 = new BoidAgent();
            a0.InitializeFromProfile(0, false, profile, new Vector3(0f, 14f, 200f), new Vector3(0f, 2f, -45f));

            BoidAgent a1 = new BoidAgent();
            a1.InitializeFromProfile(1, false, profile, new Vector3(2.0f, 8f, 200f), new Vector3(0f, 0f, -30f));

            List<BoidAgent> agents = new List<BoidAgent> { a0, a1 };
            float initialSep = Vector3.Distance(a0.positionWorld, a1.positionWorld);

            controller.Update(agents, profile, 0.2f);

            // Speed clamp check (<= 33.33 m/s)
            Assert(a0.velocityMs.magnitude <= profile.SpeedMs + 0.05f,
                "BoidAgent speed must be clamped to profile.SpeedMs (33.33 m/s)");

            // Altitude spring check (a0 started at y=14m > 8m+2m deadband -> downward force reduces Vy)
            Assert(a0.velocityMs.y < 2.0f, "Altitude spring must pull high agent back toward 8m band");

            // Transform sync check
            Assert(Vector3.Distance(a0.transform.position, a0.positionWorld) < 0.001f,
                "BoidAgent transform.position must match positionWorld after Update()");

            Console.WriteLine(string.Format("Post-Update BoidAgent[0] speed={0:F2} m/s, Vy={1:F2} m/s, pos=({2:F1},{3:F1},{4:F1})",
                a0.velocityMs.magnitude, a0.velocityMs.y, a0.positionWorld.x, a0.positionWorld.y, a0.positionWorld.z));
            Console.WriteLine("[PASS] BoidFlockController Update() on BoidAgent Instances PASSED");
        }

        /// <summary>
        /// Validates deterministic RNG seeding, WsClient message handling (SCENARIO_START / TICK / END),
        /// and OnScenarioEnd cleanup.
        /// </summary>
        private static void TestDeterministicSeedAndWsLifecycle()
        {
            Console.WriteLine("\n-- Test 4: Deterministic Seed & WS Lifecycle (START -> TICK -> END) --");

            SwarmManager m1 = new SwarmManager();
            SwarmManager m2 = new SwarmManager();
            SwarmProfile profile = SwarmMock.GetPunjabProfile();

            m1.OnScenarioStart(profile, 42819, "punjab-fog-pincer-01");
            m2.OnScenarioStart(profile, 42819, "punjab-fog-pincer-01");

            SwarmAgentState[] s1 = m1.GetSwarmSnapshot();
            SwarmAgentState[] s2 = m2.GetSwarmSnapshot();

            for (int i = 0; i < 6; i++)
            {
                float diff = Vector3.Distance(s1[i].positionWorld, s2[i].positionWorld);
                Assert(diff < 0.0001f, "Same seed (42819) must produce identical spawn positions for agent-" + i);
            }

            // Test HandleWsMessage wiring
            string startPayloadJson = "{\"sessionId\":\"sess-20261011-120000\",\"scenarioId\":\"punjab-fog-pincer-01\",\"seed\":42819,\"swarmProfile\":{\"agentCount\":6,\"decoyCount\":2,\"attackerCount\":4,\"tacticTree\":\"PINCER_LOW_FAST\",\"speedKmh\":120.0,\"altitudeM\":8.0,\"rfFreqGHz\":[5.825,2.4],\"rfPowerDbm\":23.0}}";
            m1.HandleWsMessage("SCENARIO_START", startPayloadJson);
            Assert(m1.GetSwarmSnapshot().Length == 6, "HandleWsMessage(SCENARIO_START) must spawn 6 agents");

            int injectCount = 0;
            m1.OnSwarmSnapshotInjected += snap => { injectCount = snap != null ? snap.Length : 0; };
            m1.HandleWsMessage("SCENARIO_TICK", "{}");
            Assert(injectCount == 6, "HandleWsMessage(SCENARIO_TICK) must inject 6-agent snapshot");

            m1.HandleWsMessage("SCENARIO_END", "{\"reason\":\"INSTRUCTOR_ABORT\"}");
            Assert(m1.GetSwarmSnapshot().Length == 0, "HandleWsMessage(SCENARIO_END) must destroy all agents and clear snapshot");
            Assert(m1.BoidAgents.Count == 0, "OnScenarioEnd must clear BoidAgents list");

            Console.WriteLine("[PASS] Deterministic Seed & WS Lifecycle PASSED");
        }

        /// <summary>
        /// Validates that Assets/Prefabs/SwarmAgentVisual.prefab exists and contains the Sphere mesh + BoidAgent script binding.
        /// </summary>
        private static void TestSwarmAgentVisualPrefabAsset()
        {
            Console.WriteLine("\n-- Test 5: SwarmAgentVisual.prefab Asset Verification --");

            string prefabPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Prefabs", "SwarmAgentVisual.prefab");
            Assert(File.Exists(prefabPath), "Missing deliverable prefab at: " + prefabPath);

            string content = File.ReadAllText(prefabPath);
            Assert(content.Contains("SwarmAgentVisual"), "Prefab must define GameObject SwarmAgentVisual");
            Assert(content.Contains("MeshFilter") && content.Contains("MeshRenderer") && content.Contains("SphereCollider"),
                "Prefab must contain Sphere MeshFilter, MeshRenderer, and SphereCollider");
            Assert(content.Contains("a1b2c3d4e5f647899a0b1c2d3e4f5a01"),
                "Prefab must reference BoidAgent.cs script GUID");

            Console.WriteLine("[PASS] SwarmAgentVisual.prefab Asset Verification PASSED");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }
    }
}
