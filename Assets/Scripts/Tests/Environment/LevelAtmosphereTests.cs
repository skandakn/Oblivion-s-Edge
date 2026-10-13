using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OblivionsEdge.Environment;
using OblivionsEdge.Core;

namespace OblivionsEdge.Tests.Environment
{
    [TestFixture]
    public class LevelAtmosphereTests
    {
        private GameObject _testRoot;

        [SetUp]
        public void SetUp()
        {
            _testRoot = new GameObject("TestRoot_Environment");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                Object.DestroyImmediate(_testRoot);
            }
        }

        [Test]
        public void TC01_SevenZonesDefinitionAndConnectivity()
        {
            var zoneTypes = new List<SpaceshipZoneType>
            {
                SpaceshipZoneType.StartingCorridor,
                SpaceshipZoneType.WeaponNoteLocation,
                SpaceshipZoneType.EdgePuzzleRoom,
                SpaceshipZoneType.HandlePuzzleRoom,
                SpaceshipZoneType.AlienNestArena,
                SpaceshipZoneType.CoreExtractionPoint,
                SpaceshipZoneType.ForgeArea
            };

            var createdZones = new List<SpaceshipZone>();
            for (int i = 0; i < zoneTypes.Count; i++)
            {
                var go = new GameObject($"Zone_{zoneTypes[i]}");
                go.transform.SetParent(_testRoot.transform);
                var zone = go.AddComponent<SpaceshipZone>();

                SetField(zone, "_zoneType", zoneTypes[i]);
                SetField(zone, "_zoneName", zoneTypes[i].ToString());
                SetField(zone, "_zoneSize", new Vector2(10f, 6f));
                createdZones.Add(zone);
            }

            Assert.AreEqual(7, createdZones.Count, "All 7 spaceship zones must be present.");
            foreach (var zone in createdZones)
            {
                Assert.IsNotNull(zone.ZoneName);
                Assert.IsTrue(zone.ZoneSize.x > 0 && zone.ZoneSize.y > 0);
            }
        }

        [Test]
        public void TC02_CoverObstaclesAndRigidbodyCollision()
        {
            var coverGo = new GameObject("Cover_Bulkhead");
            coverGo.layer = GameConstants.LayerEnvironmentIndex; // Layer 8
            coverGo.transform.SetParent(_testRoot.transform);

            var boxCol = coverGo.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(2f, 2f);
            boxCol.isTrigger = false;

            var cover = coverGo.AddComponent<CoverObstacle>();
            SetField(cover, "_physicalCollider", boxCol);
            SetField(cover, "_coverId", "HeavyBulkheadCrate");

            Assert.IsFalse(boxCol.isTrigger, "Cover obstacle must have a non-trigger collider to block Rigidbody2D entities.");
            Assert.AreEqual("HeavyBulkheadCrate", cover.CoverId);

            var playerGo = new GameObject("Player");
            playerGo.tag = GameConstants.TagPlayer;
            playerGo.transform.SetParent(_testRoot.transform);
            var playerCol = playerGo.AddComponent<CircleCollider2D>();

            InvokeMethod(cover, "OnTriggerEnter2D", playerCol);
            Assert.IsTrue(cover.IsPlayerTakingCover, "Player taking cover should be flagged as active.");

            InvokeMethod(cover, "OnTriggerExit2D", playerCol);
            Assert.IsFalse(cover.IsPlayerTakingCover, "Leaving cover should clear the active cover flag.");
        }

        [Test]
        public void TC03_CompactArenaDistanceConstraints()
        {
            var arenaGo = new GameObject("ArenaValidator");
            arenaGo.transform.SetParent(_testRoot.transform);
            var validator = arenaGo.AddComponent<LevelRouteValidator>();

            var pBoss = new GameObject("BossSpawn").transform;
            pBoss.position = new Vector3(15f, 0f, 0f);
            pBoss.SetParent(_testRoot.transform);

            var pCore = new GameObject("CorePoint").transform;
            pCore.position = new Vector3(8f, 3f, 0f);
            pCore.SetParent(_testRoot.transform);

            var pForge = new GameObject("ForgePoint").transform;
            pForge.position = new Vector3(22f, -3f, 0f);
            pForge.SetParent(_testRoot.transform);

            float coreToForge = Vector2.Distance(pCore.position, pForge.position);
            float bossToForge = Vector2.Distance(pBoss.position, pForge.position);

            Assert.Less(coreToForge, 20f, "Core to Forge must be less than 20 units for accessible 60s crafting.");
            Assert.Less(bossToForge, 20f, "Boss to Forge must be less than 20 units to maintain combat tension.");
        }

        [Test]
        public void TC04_VisiblePuzzleInteractionsAndPickups()
        {
            var edgeStationGo = new GameObject("EdgeStation");
            edgeStationGo.transform.SetParent(_testRoot.transform);
            var station = edgeStationGo.AddComponent<PuzzlePedestalStation>();

            SetField(station, "_roomType", PuzzleRoomType.EdgeResonatorRoom);
            SetField(station, "_componentItemId", "deathstorm_edge");
            SetField(station, "_componentDisplayName", "DeathStorm Edge Fragment");

            Assert.IsTrue(station.CanInteract(null), "Station should be interactable prior to retrieval.");
            Assert.IsFalse(station.IsFragmentRetrieved);
            Assert.IsTrue(station.InteractionPromptText.Contains("Solve Alignment Lock"));

            bool collected = false;
            station.OnFragmentCollected += (id) => {
                if (id == "deathstorm_edge") collected = true;
            };

            station.Interact(null);

            Assert.IsTrue(station.IsFragmentRetrieved, "Fragment must be retrieved after interaction.");
            Assert.IsTrue(collected, "OnFragmentCollected must fire with correct itemId.");
            Assert.IsFalse(station.CanInteract(null), "Empty pedestal cannot be re-interacted.");
        }

        [Test]
        public void TC05_HorrorAtmosphericLightingModes()
        {
            var lightGo = new GameObject("HorrorLight");
            lightGo.transform.SetParent(_testRoot.transform);
            var light = lightGo.AddComponent<Light>();
            var atmos = lightGo.AddComponent<AtmosphericLighting>();

            SetField(atmos, "_targetLight", light);

            atmos.ActiveMode = LightingMode.DarkHorror;
            Assert.AreEqual(LightingMode.DarkHorror, atmos.ActiveMode);

            atmos.ActiveMode = LightingMode.RedEmergencyStrobe;
            Assert.AreEqual(LightingMode.RedEmergencyStrobe, atmos.ActiveMode);

            atmos.ActiveMode = LightingMode.BlueVioletDeathStorm;
            Assert.AreEqual(LightingMode.BlueVioletDeathStorm, atmos.ActiveMode);
        }

        [Test]
        public void TC06_AudioAmbienceControls()
        {
            var audioGo = new GameObject("AudioAmbience");
            audioGo.transform.SetParent(_testRoot.transform);
            var controller = audioGo.AddComponent<AudioAmbienceController>();

            controller.TriggerAttackWarning(true);
            Assert.IsTrue(controller.IsAlarmActive);

            controller.TriggerAttackWarning(false);
            Assert.IsFalse(controller.IsAlarmActive);
        }

        [Test]
        public void TC07_TransmissionAndWeaponLoreNarrative()
        {
            var transGo = new GameObject("CommsTerminal");
            transGo.transform.SetParent(_testRoot.transform);
            var trans = transGo.AddComponent<TransmissionTerminal>();

            bool transmissionFired = false;
            trans.OnTransmissionPlayed += (sender, msg) => {
                transmissionFired = true;
            };
            trans.Interact(null);

            Assert.IsTrue(trans.HasPlayedTransmission);
            Assert.IsTrue(transmissionFired);

            var noteGo = new GameObject("WeaponNote");
            noteGo.transform.SetParent(_testRoot.transform);
            var note = noteGo.AddComponent<WeaponLoreNote>();

            bool noteInspected = false;
            note.OnNoteInspected += (title, body) => {
                noteInspected = true;
            };
            note.Interact(null);

            Assert.IsTrue(note.IsRead);
            Assert.IsTrue(noteInspected);
        }

        [Test]
        public void TC08_FourSectorsDerelictVesselLayout()
        {
            // Sector 1: Crew Quarters
            var s1 = new GameObject("Sector1_CrewQuarters");
            s1.transform.SetParent(_testRoot.transform);
            var z1 = s1.AddComponent<SpaceshipZone>();
            SetField(z1, "_zoneType", SpaceshipZoneType.StartingCorridor);
            SetField(z1, "_zoneName", "Sector 1: Derelict Crew Quarters");

            // Sector 2: Optics Lab
            var s2 = new GameObject("Sector2_OpticsLab");
            s2.transform.SetParent(_testRoot.transform);
            var z2 = s2.AddComponent<SpaceshipZone>();
            SetField(z2, "_zoneType", SpaceshipZoneType.EdgePuzzleRoom);
            SetField(z2, "_zoneName", "Sector 2: Optics Lab (Edge Puzzle)");

            // Sector 3: Engineering Bay
            var s3 = new GameObject("Sector3_EngineeringBay");
            s3.transform.SetParent(_testRoot.transform);
            var z3 = s3.AddComponent<SpaceshipZone>();
            SetField(z3, "_zoneType", SpaceshipZoneType.HandlePuzzleRoom);
            SetField(z3, "_zoneName", "Sector 3: Engineering Bay (Handle Puzzle)");

            // Sector 4: Bio-Containment Nest
            var s4 = new GameObject("Sector4_BioContainmentNest");
            s4.transform.SetParent(_testRoot.transform);
            var z4 = s4.AddComponent<SpaceshipZone>();
            SetField(z4, "_zoneType", SpaceshipZoneType.AlienNestArena);
            SetField(z4, "_zoneName", "Sector 4: Bio-Containment Nest (Boss Arena & Forge)");

            Assert.AreEqual("Sector 1: Derelict Crew Quarters", z1.ZoneName);
            Assert.AreEqual("Sector 2: Optics Lab (Edge Puzzle)", z2.ZoneName);
            Assert.AreEqual("Sector 3: Engineering Bay (Handle Puzzle)", z3.ZoneName);
            Assert.AreEqual("Sector 4: Bio-Containment Nest (Boss Arena & Forge)", z4.ZoneName);
        }

        [Test]
        public void TC09_FloorCeilingWallCollidersLayer8()
        {
            // Verify that floors, ceilings, and walls strictly reside on Layer 8 (Environment)
            string[] surfaces = { "Floor_Optics", "Ceiling_Optics", "Wall_Left_Quarters", "Wall_Right_Nest" };
            foreach (var name in surfaces)
            {
                var surfaceGo = new GameObject(name);
                surfaceGo.layer = GameConstants.LayerEnvironmentIndex; // Layer 8
                surfaceGo.transform.SetParent(_testRoot.transform);

                var boxCol = surfaceGo.AddComponent<BoxCollider2D>();
                boxCol.size = new Vector2(4f, 1f);
                boxCol.isTrigger = false;

                Assert.AreEqual(GameConstants.LayerEnvironmentIndex, surfaceGo.layer, $"{name} must be on Layer 8 (Environment).");
                Assert.IsFalse(boxCol.isTrigger, $"{name} must have a non-trigger BoxCollider2D.");
            }
        }

        [Test]
        public void TC10_FlashlightAndLaserReflectionPuzzles()
        {
            // Flashlight in Sector 1
            var flashGo = new GameObject("FlashlightStation");
            flashGo.transform.SetParent(_testRoot.transform);
            var flash = flashGo.AddComponent<FlashlightStation>();

            Assert.IsFalse(flash.IsCollected);
            Assert.IsTrue(flash.CanInteract(null));
            flash.Interact(null);
            Assert.IsTrue(flash.IsCollected);
            Assert.IsFalse(flash.CanInteract(null));

            // Laser Reflection in Sector 2
            var laserGo = new GameObject("OpticsLaserStation");
            laserGo.transform.SetParent(_testRoot.transform);
            var laser = laserGo.AddComponent<OpticsLaserStation>();

            Assert.IsFalse(laser.IsAligned);
            Assert.IsFalse(laser.IsFragmentRetrieved);

            // Step 1: Align mirrors
            laser.Interact(null);
            Assert.IsTrue(laser.IsAligned);
            Assert.IsFalse(laser.IsFragmentRetrieved);

            // Step 2: Retrieve Edge fragment
            bool edgeRetrieved = false;
            laser.OnEdgeCollected += (id) => {
                if (id == "deathstorm_edge") edgeRetrieved = true;
            };
            laser.Interact(null);
            Assert.IsTrue(laser.IsFragmentRetrieved);
            Assert.IsTrue(edgeRetrieved);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void InvokeMethod(object target, string methodName, params object[] parameters)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(target, parameters);
        }
    }
}
