using System.Collections;
using UnityEngine;
using OblivionsEdge.Core;
using OblivionsEdge.Player;
using OblivionsEdge.Weapons;
using OblivionsEdge.Enemies;
using OblivionsEdge.UI;

namespace OblivionsEdge.Core
{
    /// <summary>
    /// Runtime bootstrapper that guarantees an immediate, visually rich, and fully playable
    /// experience whenever Unity enters Play Mode, even if the scene was empty or not yet rebuilt.
    /// </summary>
    public static class RuntimeBootstrapper
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializePlayMode()
        {
            if (_initialized) return;
            _initialized = true;

            EnsureCamera();
            EnsureEnvironment();
            GameObject playerObj = EnsurePlayer();
            EnsureAnimalCompanion(playerObj);
            EnsureCombatTargets();
            EnsureHUD(playerObj);

            Debug.Log("<color=#00ffcc>[RuntimeBootstrapper]</color> Playable environment initialized successfully!");
        }

        private static void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = GameConstants.TagMainCamera;
                camObj.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.1f, 1f); // Dark deep-space void
            cam.clearFlags = CameraClearFlags.SolidColor;

            if (cam.GetComponent<CameraFollow>() == null)
            {
                cam.gameObject.AddComponent<CameraFollow>();
            }
        }

        private static void EnsureEnvironment()
        {
            if (GameObject.Find("Environment_Floor") != null) return;

            GameObject envRoot = new GameObject("Environment_Deck");

            // Main Deck Floor
            CreatePlatform(envRoot.transform, "Environment_Floor", new Vector2(0f, -3.8f), new Vector2(36f, 1.2f), new Color(0.12f, 0.16f, 0.22f));

            // Left Elevated Platform
            CreatePlatform(envRoot.transform, "Platform_Left", new Vector2(-7.5f, -1.2f), new Vector2(6f, 0.5f), new Color(0.16f, 0.22f, 0.3f));

            // Right Elevated Platform
            CreatePlatform(envRoot.transform, "Platform_Right", new Vector2(7.5f, -1.2f), new Vector2(6f, 0.5f), new Color(0.16f, 0.22f, 0.3f));

            // High Center Observation Reactor Platform
            CreatePlatform(envRoot.transform, "Platform_Reactor", new Vector2(0f, 1.2f), new Vector2(5f, 0.5f), new Color(0.2f, 0.28f, 0.38f));

            // Left Wall Barrier
            CreatePlatform(envRoot.transform, "Wall_Left", new Vector2(-18f, 2f), new Vector2(1.2f, 14f), new Color(0.08f, 0.1f, 0.15f));

            // Right Wall Barrier
            CreatePlatform(envRoot.transform, "Wall_Right", new Vector2(18f, 2f), new Vector2(1.2f, 14f), new Color(0.08f, 0.1f, 0.15f));
        }

        private static void CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.layer = GameConstants.LayerEnvironmentIndex;

            var col = obj.AddComponent<BoxCollider2D>();
            col.size = size;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sortingLayerName = "Environment";
            sr.sortingOrder = 0;

            Texture2D tex = CreateGridTexture(32, 32, color);
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
        }

        private static GameObject EnsurePlayer()
        {
            GameObject player = GameObject.FindWithTag(GameConstants.TagPlayer);
            if (player == null)
            {
                player = GameObject.Find("Player");
            }

            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = GameConstants.TagPlayer;
                player.layer = GameConstants.LayerPlayerIndex;
                player.transform.position = new Vector3(0f, -2.5f, 0f);

                var rb = player.AddComponent<Rigidbody2D>();
                rb.mass = 1f;
                rb.gravityScale = 3f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                var col = player.AddComponent<CapsuleCollider2D>();
                col.size = new Vector2(0.8f, 1.8f);

                var health = player.AddComponent<PlayerHealth>();
                var controller = player.AddComponent<PlayerController>();
                var gun = player.AddComponent<GunController>();

                // Visuals Child
                GameObject visuals = new GameObject("Visuals");
                visuals.transform.SetParent(player.transform, false);
                var sr = visuals.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 5;
                sr.sprite = CreateSurvivorSprite();
            }
            else
            {
                // Ensure existing player has a visible sprite
                var sr = player.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && (sr.sprite == null || sr.color.a < 0.1f))
                {
                    sr.sprite = CreateSurvivorSprite();
                    sr.color = Color.white;
                }

                // Ensure gun is attached
                if (player.GetComponent<GunController>() == null)
                {
                    player.AddComponent<GunController>();
                }
            }

            // Bind CameraFollow
            Camera cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                {
                    follow.Target = player.transform;
                }
            }

            return player;
        }

        private static GameObject EnsureAnimalCompanion(GameObject player)
        {
            if (Object.FindAnyObjectByType<AnimalCharacter>() != null)
            {
                return GameObject.Find("Animal_CyberHound");
            }

            GameObject animalObj = new GameObject("Animal_CyberHound");
            animalObj.tag = GameConstants.TagPlayer;
            animalObj.layer = GameConstants.LayerPlayerIndex;
            animalObj.transform.position = player != null ? player.transform.position + new Vector3(2.5f, 0f, 0f) : new Vector3(2.5f, -2.5f, 0f);

            var rb = animalObj.AddComponent<Rigidbody2D>();
            rb.mass = 2.5f;
            rb.gravityScale = 3f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var col = animalObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 1.1f);
            col.offset = new Vector2(0f, 0.05f);

            var animal = animalObj.AddComponent<AnimalCharacter>();
            if (player != null)
            {
                animal.TargetPlayer = player.transform;
            }

            // Visuals Child with cyber animal sprite
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(animalObj.transform, false);
            var sr = visuals.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Characters";
            sr.sortingOrder = 4;
            sr.sprite = CreateCyberAnimalSprite();

            return animalObj;
        }

        private static void EnsureCombatTargets()
        {
            if (Object.FindAnyObjectByType<TargetDrone>() != null) return;

            GameObject targetsRoot = new GameObject("Target_Drones");

            CreateTargetDrone(targetsRoot.transform, "BioDrone_Alpha", new Vector3(7.5f, 0.2f, 0f));
            CreateTargetDrone(targetsRoot.transform, "BioDrone_Beta", new Vector3(-7.5f, 0.2f, 0f));
            CreateTargetDrone(targetsRoot.transform, "BioDrone_Central", new Vector3(0f, 2.6f, 0f));
        }

        private static void CreateTargetDrone(Transform parent, string name, Vector3 position)
        {
            GameObject drone = new GameObject(name);
            drone.transform.SetParent(parent);
            drone.transform.position = position;
            drone.layer = GameConstants.LayerEnemyIndex;
            drone.tag = GameConstants.TagEnemy;

            var rb = drone.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = drone.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var sr = drone.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 4;
            sr.sprite = CreateDroneSprite();

            drone.AddComponent<TargetDrone>();
        }

        private static void EnsureHUD(GameObject player)
        {
            if (GameObject.Find("InGameHUD_Canvas") != null) return;

            GameObject canvasObj = new GameObject("InGameHUD_Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Ensure EventSystem
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Floating Controls Hint & Vitals
            var hud = canvasObj.AddComponent<RuntimeHUDOverlay>();
            hud.BindPlayer(player);
        }

        private static Sprite CreateSurvivorSprite()
        {
            int w = 24, h = 48;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color suitColor = new Color(0.18f, 0.28f, 0.4f, 1f);
            Color armorColor = new Color(0.35f, 0.45f, 0.6f, 1f);
            Color visorColor = new Color(0.1f, 0.95f, 0.95f, 1f);
            Color bootColor = new Color(0.1f, 0.12f, 0.16f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            // Head & Visor
            for (int y = 36; y < 46; y++)
            {
                for (int x = 7; x < 17; x++)
                {
                    if (y >= 39 && y <= 42 && x >= 11 && x <= 16)
                    {
                        tex.SetPixel(x, y, visorColor); // Glowing cyan visor
                    }
                    else
                    {
                        tex.SetPixel(x, y, armorColor);
                    }
                }
            }

            // Torso & Cyber Armor
            for (int y = 18; y < 36; y++)
            {
                for (int x = 5; x < 19; x++)
                {
                    tex.SetPixel(x, y, (y >= 26 && y <= 33 && x >= 7 && x <= 17) ? armorColor : suitColor);
                }
            }

            // Legs & Boots
            for (int y = 0; y < 18; y++)
            {
                for (int x = 5; x < 10; x++) tex.SetPixel(x, y, y < 6 ? bootColor : suitColor);
                for (int x = 14; x < 19; x++) tex.SetPixel(x, y, y < 6 ? bootColor : suitColor);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
        }

        private static Sprite CreateDroneSprite()
        {
            int w = 24, h = 24;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color shellColor = new Color(0.55f, 0.12f, 0.15f, 1f);
            Color coreColor = new Color(1f, 0.25f, 0.25f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 11.5f) / 10f;
                    float dy = (y - 11.5f) / 10f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 0.45f)
                    {
                        tex.SetPixel(x, y, coreColor);
                    }
                    else if (d <= 1f)
                    {
                        tex.SetPixel(x, y, shellColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
        }

        private static Texture2D CreateGridTexture(int w, int h, Color baseColor)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color borderColor = baseColor * 1.35f;
            borderColor.a = 1f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                    {
                        tex.SetPixel(x, y, borderColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, baseColor);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static Sprite CreateCyberAnimalSprite()
        {
            int w = 64, h = 48;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color cArmorDark = new Color(0.12f, 0.16f, 0.22f, 1f);
            Color cArmorMid = new Color(0.22f, 0.28f, 0.38f, 1f);
            Color cArmorLight = new Color(0.35f, 0.45f, 0.59f, 1f);
            Color cArmorHighlight = new Color(0.63f, 0.73f, 0.86f, 1f);
            Color cCyan = new Color(0f, 0.96f, 1f, 1f);
            Color cOrange = new Color(1f, 0.59f, 0.08f, 1f);
            Color cClaw = new Color(0.06f, 0.07f, 0.09f, 1f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, Color.clear);

            // Torso & Body
            for (int y = 18; y < 34; y++)
            {
                for (int x = 18; x < 48; x++)
                {
                    float dx = (x - 33) / 15f;
                    float dy = (y - 26) / 8f;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        Color c = y <= 21 ? cArmorLight : (y >= 29 ? cArmorDark : cArmorMid);
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            // Power Reactor Core
            for (int y = 24; y <= 28; y++)
                for (int x = 30; x <= 35; x++)
                    tex.SetPixel(x, y, cArmorDark);
            for (int y = 25; y <= 27; y++)
                for (int x = 31; x <= 34; x++)
                    tex.SetPixel(x, y, cOrange);

            // Neck & Muscular Chest
            for (int y = 15; y < 28; y++)
            {
                for (int x = 42; x < 53; x++)
                {
                    float dx = (x - 47) / 6f;
                    float dy = (y - 21) / 7f;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        tex.SetPixel(x, y, y <= 19 ? cArmorLight : cArmorMid);
                    }
                }
            }

            // Head & Snout
            for (int y = 10; y < 19; y++)
                for (int x = 48; x < 57; x++)
                    if ((x - 52) * (x - 52) + (y - 14) * (y - 14) <= 20)
                        tex.SetPixel(x, y, cArmorLight);

            // Ears
            for (int y = 6; y <= 10; y++)
                for (int x = 48; x <= 50; x++)
                    tex.SetPixel(x, y, cArmorHighlight);

            // Jaws
            for (int y = 14; y <= 18; y++)
                for (int x = 54; x <= 61; x++)
                    tex.SetPixel(x, y, y <= 16 ? cArmorLight : cArmorMid);
            for (int y = 18; y <= 20; y++)
                for (int x = 55; x <= 59; x++)
                    tex.SetPixel(x, y, cArmorDark);

            // Cyan Optical Visor / Sensor Eyes
            for (int x = 52; x <= 56; x++)
            {
                tex.SetPixel(x, 13, cCyan);
                tex.SetPixel(x, 14, cCyan);
            }

            // Cyber Spine Ridge
            for (int x = 22; x <= 42; x += 4)
            {
                tex.SetPixel(x, 18, cArmorHighlight);
                tex.SetPixel(x + 1, 18, cCyan);
            }

            // Tail
            for (int i = 0; i < 14; i++)
            {
                int tx = 20 - i;
                int ty = 23 - (int)(6 * Mathf.Pow(i / 13f, 1.3f));
                tex.SetPixel(tx, ty, cArmorMid);
                tex.SetPixel(tx, ty + 1, (i % 3 == 0) ? cCyan : cArmorLight);
            }

            // Legs & Claws
            // Back Left Leg
            for (int y = 24; y <= 33; y++) for (int x = 17; x <= 25; x++) tex.SetPixel(x, y, cArmorMid);
            for (int y = 33; y <= 41; y++) for (int x = 15; x <= 19; x++) tex.SetPixel(x, y, cArmorDark);
            for (int y = 41; y <= 44; y++) for (int x = 14; x <= 19; x++) tex.SetPixel(x, y, cClaw);

            // Back Right Leg
            for (int y = 25; y <= 34; y++) for (int x = 23; x <= 29; x++) tex.SetPixel(x, y, cArmorLight);
            for (int y = 34; y <= 42; y++) for (int x = 22; x <= 26; x++) tex.SetPixel(x, y, cArmorMid);
            for (int y = 42; y <= 44; y++) for (int x = 21; x <= 27; x++) tex.SetPixel(x, y, cClaw);

            // Front Left Leg
            for (int y = 26; y <= 35; y++) for (int x = 41; x <= 46; x++) tex.SetPixel(x, y, cArmorDark);
            for (int y = 35; y <= 42; y++) for (int x = 42; x <= 45; x++) tex.SetPixel(x, y, cArmorMid);
            for (int y = 42; y <= 44; y++) for (int x = 41; x <= 46; x++) tex.SetPixel(x, y, cClaw);

            // Front Right Leg
            for (int y = 25; y <= 34; y++) for (int x = 47; x <= 53; x++) tex.SetPixel(x, y, cArmorLight);
            for (int y = 34; y <= 42; y++) for (int x = 47; x <= 51; x++) tex.SetPixel(x, y, cArmorMid);
            for (int y = 42; y <= 44; y++) for (int x = 46; x <= 52; x++) tex.SetPixel(x, y, cClaw);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
        }
    }
}
