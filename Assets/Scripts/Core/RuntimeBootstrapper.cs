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

        private static void EnsureCombatTargets()
        {
            if (Object.FindFirstObjectByType<TargetDrone>() != null) return;

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
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
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
    }
}
