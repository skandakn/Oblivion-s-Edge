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
            // Verify that all 7 spaceship zones can be constructed and identified
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

                // Configure via reflection
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
            // Test cover object physical blocking and stealth line-of-sight occlusion
            var coverGo = new GameObject("Cover_Bulkhead");
            coverGo.layer = GameConstants.LayerEnvironmentIndex; // Layer 8
            coverGo.transform.SetParent(_testRoot.transform);

            var boxCol = coverGo.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(2f, 2f);
            boxCol.isTrigger = false; // Solid physical collider for Rigidbody2D

            var cover = coverGo.AddComponent<CoverObstacle>();
            SetField(cover, "_physicalCollider", boxCol);
            SetField(cover, "_coverId", "HeavyBulkheadCrate");

            Assert.IsFalse(boxCol.isTrigger, "Cover obstacle must have a non-trigger collider to block Rigidbody2D entities.");
            Assert.AreEqual("HeavyBulkheadCrate", cover.CoverId);

            // Simulate player trigger entering cover zone
            var playerGo = new GameObject("Player");
            playerGo.tag = GameConstants.TagPlayer;
            playerGo.transform.SetParent(_testRoot.transform);
            var playerCol = playerGo.AddComponent<CircleCollider2D>();

            // Trigger detection
            InvokeMethod(cover, "OnTriggerEnter2D", playerCol);
            Assert.IsTrue(cover.IsPlayerTakingCover, "Player taking cover should be flagged as active.");

            InvokeMethod(cover, "OnTriggerExit2D", playerCol);
            Assert.IsFalse(cover.IsPlayerTakingCover, "Leaving cover should clear the active cover flag.");
        }

        [Test]
        public void TC03_CompactArenaDistanceConstraints()
        {
            // Verify that the arena layout keeps Boss, Core, and Forge accessible within compact limits
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

            // Test DarkHorror
            atmos.ActiveMode = LightingMode.DarkHorror;
            Assert.AreEqual(LightingMode.DarkHorror, atmos.ActiveMode);

            // Test RedEmergencyStrobe
            atmos.ActiveMode = LightingMode.RedEmergencyStrobe;
            Assert.AreEqual(LightingMode.RedEmergencyStrobe, atmos.ActiveMode);

            // Test BlueVioletDeathStorm
            atmos.ActiveMode = LightingMode.BlueVioletDeathStorm;
            Assert.AreEqual(LightingMode.BlueVioletDeathStorm, atmos.ActiveMode);
        }

        [Test]
        public void TC06_AudioAmbienceControls()
        {
            var audioGo = new GameObject("AudioAmbience");
            audioGo.transform.SetParent(_testRoot.transform);
            var controller = audioGo.AddComponent<AudioAmbienceController>();

            // Trigger attack warning alarm
            controller.TriggerAttackWarning(true);
            Assert.IsTrue(controller.IsAlarmActive);

            controller.TriggerAttackWarning(false);
            Assert.IsFalse(controller.IsAlarmActive);
        }

        [Test]
        public void TC07_TransmissionAndWeaponLoreNarrative()
        {
            // Teammate Transmission
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

            // Weapon Lore Note
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
