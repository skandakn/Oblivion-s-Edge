using UnityEngine;
using OblivionsEdge.Player;
using OblivionsEdge.Weapons;

namespace OblivionsEdge.UI
{
    /// <summary>
    /// Robust runtime HUD overlay rendered via OnGUI.
    /// Works immediately without any prefabs, font assets, or Canvas dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public class RuntimeHUDOverlay : MonoBehaviour
    {
        private PlayerHealth _health;
        private PlayerController _controller;
        private GunController _gun;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private Texture2D _hpTex;
        private Texture2D _staminaTex;
        private Texture2D _bgTex;

        public void BindPlayer(GameObject player)
        {
            if (player != null)
            {
                _health = player.GetComponent<PlayerHealth>();
                _controller = player.GetComponent<PlayerController>();
                _gun = player.GetComponent<GunController>();
            }
        }

        private void Awake()
        {
            _hpTex = MakeColorTex(new Color(0.15f, 0.85f, 0.45f, 0.9f));
            _staminaTex = MakeColorTex(new Color(1f, 0.75f, 0.2f, 0.9f));
            _bgTex = MakeColorTex(new Color(0.04f, 0.07f, 0.12f, 0.85f));
        }

        private Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _titleStyle.normal.textColor = new Color(0.2f, 0.9f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.9f, 0.95f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _bgTex;
        }

        private void OnGUI()
        {
            InitStyles();

            // Lazy locate if not bound
            if (_health == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) BindPlayer(p);
            }

            // Top Status Banner
            GUI.Box(new Rect(10, 10, 320, 115), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(20, 15, 300, 20), "OBLIVION'S EDGE // SURVIVOR STATUS", _titleStyle);

            // Health Bar
            float curHp = _health != null ? _health.CurrentHealth : 100f;
            float maxHp = _health != null ? _health.MaxHealth : 100f;
            GUI.Label(new Rect(20, 38, 120, 18), $"INTEGRITY: {Mathf.CeilToInt(curHp)} / {maxHp}", _labelStyle);
            GUI.DrawTexture(new Rect(150, 42, 160, 12), _bgTex);
            GUI.DrawTexture(new Rect(150, 42, 160 * Mathf.Clamp01(curHp / maxHp), 12), _hpTex);

            // Dash / Stamina Bar
            float dashCdRem = _controller != null ? _controller.DashCooldownRemaining : 0f;
            float dashTotal = _controller != null ? _controller.DashCooldown : 1f;
            float staminaPct = dashTotal > 0f ? Mathf.Clamp01(1f - (dashCdRem / dashTotal)) : 1f;
            GUI.Label(new Rect(20, 58, 120, 18), $"EVADE (DASH): {(staminaPct >= 1f ? "READY" : "RECHARGING")}", _labelStyle);
            GUI.DrawTexture(new Rect(150, 62, 160, 12), _bgTex);
            GUI.DrawTexture(new Rect(150, 62, 160 * staminaPct, 12), _staminaTex);

            // Pulse Pistol Ammo
            int ammo = _gun != null ? _gun.CurrentAmmo : 12;
            int cap = _gun != null ? _gun.MagazineCapacity : 12;
            int res = _gun != null ? _gun.ReserveAmmo : 48;
            string ammoStr = (_gun != null && _gun.IsReloading) ? "RELOADING..." : $"{ammo} / {res} [PULSE PISTOL]";
            GUI.Label(new Rect(20, 80, 290, 20), $"WEAPON: {ammoStr}", _titleStyle);

            // Bottom Controls Bar
            float barW = 620f;
            float barX = (Screen.width - barW) * 0.5f;
            GUI.Box(new Rect(barX, Screen.height - 45, barW, 35), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(barX + 15, Screen.height - 40, barW - 30, 25),
                "[A / D] Move  •  [SPACE] Jump  •  [LEFT CLICK] Shoot  •  [R] Reload  •  [ALT/SHIFT] Dash",
                _labelStyle);
        }
    }
}
