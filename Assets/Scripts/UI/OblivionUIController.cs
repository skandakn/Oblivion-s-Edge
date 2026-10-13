using System;
using System.Collections;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;
using OblivionsEdge.Crafting;
using OblivionsEdge.Inventory;
using OblivionsEdge.Puzzles;
using OblivionsEdge.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OblivionsEdge.UI
{
    // ─────────────────────────────────────────────────────────────────────────
    //  OblivionUIController.cs — Member 12 (UI, QA & Judge Demonstration)
    //
    //  Responsibilities:
    //    · Main Menu (Start, Controls, Quit).
    //    · In-game HUD: Health bar, Ammo readout, Component indicators
    //      (Edge, Handle, Core), Boss Armor/Health bar.
    //    · DeathStorm 60-second crafting countdown display with stage labels.
    //    · Death / Retry screen and Victory screen.
    //    · Event subscriptions to Member 2 (Health via IDamageable),
    //      Member 3 (Ammo via IWeapon/IWeaponHandler),
    //      Member 9 (Inventory via IInventory),
    //      Member 10 (Crafting via ICraftingCountdownSource).
    //    · Auto-discovers runtime sources if Inspector references are not wired.
    //
    //  Does NOT own: health values, ammo state, inventory items, boss AI,
    //                puzzle logic, or crafting timers.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Published adapter for the DeathStorm crafting controller's UI telemetry.</summary>
    public interface ICraftingCountdownSource
    {
        float CountdownRemaining { get; }
        float CountdownTotal { get; }
        string CurrentInstruction { get; }
        event Action<float, float> OnTimerTick;
        event Action<string> OnInstructionChanged;
        event Action OnCraftingSucceeded;
        event Action<bool> OnCountdownExpired;
    }

    /// <summary>Published adapter for a boss armor component owned by the encounter team.</summary>
    public interface IBossArmorSource
    {
        float CurrentArmor { get; }
        float MaxArmor { get; }
        event Action<float, float> OnArmorChanged;
        event Action OnArmorDepleted;
    }

    /// <summary>
    /// Event-driven HUD, menu, and encounter feedback view. It renders gameplay telemetry but
    /// never owns health, inventory, crafting, puzzle, or combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OblivionUIController : MonoBehaviour, IHUDView, IUIManager
    {
        // ── Panel References ──────────────────────────────────────────────
        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _controlsPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _craftingPanel;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _puzzlePanel;
        [SerializeField] private GameObject _deathPanel;
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private GameObject _pauseMenuPanel;

        // ── HUD Readout References ────────────────────────────────────────
        [Header("HUD Readouts")]
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private TMP_Text _weaponText;

        [Header("Component Indicators")]
        [SerializeField] private TMP_Text _edgeIndicator;
        [SerializeField] private TMP_Text _handleIndicator;
        [SerializeField] private TMP_Text _coreIndicator;

        [Header("Boss Bar")]
        [SerializeField] private GameObject _bossBarPanel;
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private Image _bossHealthFill;
        [SerializeField] private Image _bossArmorFill;
        [SerializeField] private TMP_Text _bossArmorText;
        [SerializeField] private TMP_Text _bossPhaseText;

        [Header("Interaction")]
        [SerializeField] private TMP_Text _interactionPromptText;
        [SerializeField] private GameObject _interactionPromptPanel;

        // ── Crafting / Puzzle References ──────────────────────────────────
        [Header("Crafting and Puzzle")]
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private Image _countdownFill;
        [SerializeField] private TMP_Text _puzzleInstructionText;
        [SerializeField] private TMP_Text _puzzleFeedbackText;
        [SerializeField] private TMP_Text _craftingStageText;

        // ── Feedback References ───────────────────────────────────────────
        [Header("Feedback")]
        [SerializeField] private TMP_Text _notificationText;
        [SerializeField] private TMP_Text _deathReasonText;
        [SerializeField] private TMP_Text _victoryText;

        // ── Event Sources (Inspector wiring) ─────────────────────────────
        [Header("Gameplay Event Sources")]
        [Tooltip("Component implementing IDamageable — normally the player health component (Member 2).")]
        [SerializeField] private MonoBehaviour _playerHealthSource;

        [Tooltip("Component implementing IWeapon or IWeaponHandler (Member 3).")]
        [SerializeField] private MonoBehaviour _weaponSource;

        [Tooltip("Component implementing IInventory (Member 9).")]
        [SerializeField] private MonoBehaviour _inventorySource;

        [Tooltip("Component implementing IBossController and IDamageable (Members 4/5).")]
        [SerializeField] private MonoBehaviour _bossSource;

        [Tooltip("Component implementing IBossArmorSource (Member 5).")]
        [SerializeField] private MonoBehaviour _bossArmorSource;

        [Tooltip("Component implementing IPuzzleController.")]
        [SerializeField] private MonoBehaviour _puzzleSource;

        [Tooltip("Component implementing ICraftingCountdownSource (Member 10).")]
        [SerializeField] private MonoBehaviour _craftingSource;

        // ── Component Item IDs ────────────────────────────────────────────
        [Header("Component Item IDs")]
        [SerializeField] private string _edgeItemId   = "deathstorm_edge";
        [SerializeField] private string _handleItemId = "deathstorm_handle";
        [SerializeField] private string _coreItemId   = "deathstorm_core";
        [SerializeField] private string _gameplaySceneName = "MainScene";

        // ── Runtime state ────────────────────────────────────────────────
        private IDamageable            _playerHealth;
        private IWeapon                _weapon;
        private IWeaponHandler         _weaponHandler;
        private IInventory             _inventory;
        private IDamageable            _bossHealth;
        private IBossController        _bossController;
        private IBossArmorSource       _bossArmor;
        private IPuzzleController      _puzzle;
        private ICraftingCountdownSource _crafting;
        private GameManager            _gameManager;
        private Coroutine              _notificationRoutine;

        // ── IUIManager implementation ─────────────────────────────────────
        public IHUDView    HUD           => this;
        public UIPanelType ActivePanel   { get; private set; }
        public bool        IsAnyModalOpen => ActivePanel != UIPanelType.None;

        public event Action<UIPanelType> OnPanelOpened;
        public event Action<UIPanelType> OnPanelClosed;

        // ─────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            ResolveReferences();
            ValidateReferences();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RefreshInitialTelemetry();
        }

        private void Start()
        {
            // GameManager singleton may initialise after UI prefab is enabled.
            if (_gameManager == null && GameManager.Instance != null)
            {
                _gameManager = GameManager.Instance;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }

            // Attempt auto-discovery of runtime sources that weren't Inspector-wired.
            TryAutoDiscoverSources();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Source Resolution
        // ─────────────────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            _playerHealth   = _playerHealthSource  as IDamageable;
            _weapon         = _weaponSource         as IWeapon;
            _weaponHandler  = _weaponSource         as IWeaponHandler;
            _inventory      = _inventorySource      as IInventory;
            _bossHealth     = _bossSource           as IDamageable;
            _bossController = _bossSource           as IBossController;
            _bossArmor      = _bossArmorSource      as IBossArmorSource;
            _puzzle         = _puzzleSource         as IPuzzleController;
            _crafting       = _craftingSource       as ICraftingCountdownSource;
            _gameManager    = GameManager.Instance;
        }

        /// <summary>
        /// Fallback discovery — finds runtime components by tag when Inspector wires are missing.
        /// Guarantees UI works even when a designer hasn't fully wired the scene.
        /// </summary>
        private void TryAutoDiscoverSources()
        {
            if (_playerHealth == null)
            {
                var playerGo = GameObject.FindWithTag(GameConstants.TagPlayer);
                if (playerGo != null)
                {
                    _playerHealth  = playerGo.GetComponentInChildren<IDamageable>();
                    _weapon        = playerGo.GetComponentInChildren<IWeapon>();
                    _weaponHandler = playerGo.GetComponentInChildren<IWeaponHandler>();
                    _inventory     = playerGo.GetComponentInChildren<IInventory>();
                    SubscribePlayerEvents();
                }
            }

            if (_bossHealth == null)
            {
                var bossGo = GameObject.FindWithTag(GameConstants.TagBoss);
                if (bossGo != null)
                {
                    _bossHealth     = bossGo.GetComponentInChildren<IDamageable>();
                    _bossController = bossGo.GetComponentInChildren<IBossController>();
                    _bossArmor      = bossGo.GetComponentInChildren<IBossArmorSource>();
                    SubscribeBossEvents();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Main Menu Actions
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Called by the Main Menu "START" button.</summary>
        public void StartGame()
        {
            SetActive(_mainMenuPanel, false);
            SetActive(_hudPanel, true);
            _gameManager?.SetState(GameState.InGame);
        }

        /// <summary>Called by the Main Menu "CONTROLS" button.</summary>
        public void OpenControls()
        {
            SetActive(_controlsPanel, true);
        }

        /// <summary>Called by the Controls panel "CLOSE" button.</summary>
        public void CloseControls()
        {
            SetActive(_controlsPanel, false);
        }

        /// <summary>Called by the Main Menu "QUIT" button.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        //  Scene / Session Management
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Restarts the active scene. Called by Death / Retry screen.</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Loads the named gameplay scene from the Main Menu.</summary>
        public void LoadGameplayScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_gameplaySceneName);
        }

        // ─────────────────────────────────────────────────────────────────
        //  IHUDView — Vitals
        // ─────────────────────────────────────────────────────────────────

        public void UpdateHealth(float current, float max)
        {
            float normalized = Normalize(current, max);
            SetText(_healthText, $"VITALS  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}");
            SetFill(_healthFill, normalized);

            // Pulse warning at <25 %
            if (_healthFill != null)
            {
                _healthFill.color = normalized < 0.25f
                    ? new Color(1f, 0.2f, 0.1f, 1f)   // danger red
                    : new Color(0.2f, 0.9f, 0.45f, 1f); // normal green
            }
        }

        // Stubs — additional vitals displayed on demand by Member 2 events
        public void UpdateStamina(float current, float max) { }
        public void UpdateSanity(float current, float max) { }
        public void UpdateOxygen(float current, float max) { }

        // ─────────────────────────────────────────────────────────────────
        //  IHUDView — Weapon / Ammo
        // ─────────────────────────────────────────────────────────────────

        public void UpdateAmmo(int currentMagazine, int totalReserve)
        {
            SetText(_ammoText, $"AMMO  {currentMagazine} / {totalReserve}");
        }

        public void UpdateEquippedWeapon(string weaponDisplayName)
        {
            SetText(_weaponText,
                string.IsNullOrWhiteSpace(weaponDisplayName)
                    ? "UNARMED"
                    : weaponDisplayName.ToUpperInvariant());
        }

        // ─────────────────────────────────────────────────────────────────
        //  IHUDView — Interaction Prompt
        // ─────────────────────────────────────────────────────────────────

        public void ShowInteractionPrompt(string promptText)
        {
            SetText(_interactionPromptText, promptText);
            SetActive(_interactionPromptPanel, !string.IsNullOrWhiteSpace(promptText));
        }

        public void HideInteractionPrompt()
        {
            SetActive(_interactionPromptPanel, false);
        }

        // ─────────────────────────────────────────────────────────────────
        //  IHUDView — Boss Bar
        // ─────────────────────────────────────────────────────────────────

        public void ShowBossHealthBar(string bossName, float currentHealth, float maxHealth)
        {
            SetText(_bossNameText, bossName.ToUpperInvariant());
            SetActive(_bossBarPanel, true);
            UpdateBossHealth(currentHealth, maxHealth);
        }

        public void UpdateBossHealth(float currentHealth, float maxHealth)
        {
            SetFill(_bossHealthFill, Normalize(currentHealth, maxHealth));
        }

        public void HideBossHealthBar()
        {
            SetActive(_bossBarPanel, false);
        }

        /// <summary>Updates the dual-layer armor bar (Member 5 contract).</summary>
        public void UpdateBossArmor(float currentArmor, float maxArmor)
        {
            SetFill(_bossArmorFill, Normalize(currentArmor, maxArmor));
            SetText(_bossArmorText, $"ARMOR  {Mathf.CeilToInt(currentArmor)} / {Mathf.CeilToInt(maxArmor)}");
        }

        // ─────────────────────────────────────────────────────────────────
        //  Crafting Countdown Display (Member 10 — IDeathStormCrafting)
        // ─────────────────────────────────────────────────────────────────

        private void UpdateCraftingCountdown(float remaining, float total)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            SetText(_countdownText, $"DEATHSTORM SYNTHESIS  {secs:00}s");
            SetFill(_countdownFill, Normalize(remaining, total));
            SetActive(_craftingPanel, total > 0f);

            // Colour the fill: green → amber → red as time drains
            if (_countdownFill != null)
            {
                float t = Normalize(remaining, total);
                _countdownFill.color = t > 0.5f
                    ? new Color(0.25f, 0.9f, 0.5f, 1f)
                    : t > 0.25f
                        ? new Color(1f, 0.75f, 0.1f, 1f)
                        : new Color(1f, 0.2f, 0.1f, 1f);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Puzzle / Instruction Panels
        // ─────────────────────────────────────────────────────────────────

        public void ShowPuzzleInstruction(string instruction)
        {
            SetText(_puzzleInstructionText, instruction);
            SetActive(_puzzlePanel, true);
            OpenPanel(UIPanelType.PuzzleTerminal);
        }

        public void ShowWeaponNote(string note) => ShowPuzzleInstruction(note);

        public void ShowPuzzleSuccess(string message = "PUZZLE SOLVED")
        {
            SetText(_puzzleFeedbackText, message);
        }

        public void ShowPuzzleFailure(string message = "PUZZLE FAILED")
        {
            SetText(_puzzleFeedbackText, message);
        }

        // ─────────────────────────────────────────────────────────────────
        //  IUIManager — Panel Routing
        // ─────────────────────────────────────────────────────────────────

        public void OpenInventory()
        {
            SetActive(_inventoryPanel, true);
            OpenPanel(UIPanelType.Inventory);
        }

        public void CloseInventory()
        {
            SetActive(_inventoryPanel, false);
            ClosePanel(UIPanelType.Inventory);
        }

        public void OpenCrafting(ICraftingStation station)
        {
            SetActive(_craftingPanel, true);
            OpenPanel(UIPanelType.Crafting);
        }

        public void CloseCrafting()
        {
            SetActive(_craftingPanel, false);
            ClosePanel(UIPanelType.Crafting);
        }

        public void OpenPauseMenu()
        {
            SetActive(_pauseMenuPanel, true);
            OpenPanel(UIPanelType.PauseMenu);
            _gameManager?.PauseGame();
        }

        public void ClosePauseMenu()
        {
            SetActive(_pauseMenuPanel, false);
            ClosePanel(UIPanelType.PauseMenu);
            _gameManager?.ResumeGame();
        }

        public void ShowNotification(string message, float durationSeconds = 3f)
        {
            if (_notificationRoutine != null)
                StopCoroutine(_notificationRoutine);
            _notificationRoutine = StartCoroutine(NotificationRoutine(message, durationSeconds));
        }

        public void CloseAllModals()
        {
            SetActive(_controlsPanel, false);
            SetActive(_craftingPanel, false);
            SetActive(_inventoryPanel, false);
            SetActive(_puzzlePanel, false);
            SetActive(_pauseMenuPanel, false);
            if (ActivePanel != UIPanelType.None)
            {
                UIPanelType closed = ActivePanel;
                ActivePanel = UIPanelType.None;
                OnPanelClosed?.Invoke(closed);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Event Subscriptions — Member 2/3/9/10 contract bridges
        // ─────────────────────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            SubscribePlayerEvents();
            SubscribeBossEvents();
            SubscribePuzzleEvents();
            SubscribeCraftingEvents();

            if (_gameManager != null)
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void SubscribePlayerEvents()
        {
            // Member 2 — Player health (IDamageable)
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged += UpdateHealth;
                _playerHealth.OnDied          += HandlePlayerDied;
            }

            // Member 3 — Weapon ammo (IWeapon)
            if (_weapon != null)
                _weapon.OnAmmoChanged += UpdateAmmo;

            // Member 3 — Weapon swap (IWeaponHandler)
            if (_weaponHandler != null)
            {
                _weaponHandler.OnWeaponEquipped  += HandleWeaponEquipped;
                _weaponHandler.OnWeaponHolstered += HandleWeaponHolstered;
            }

            // Member 9 — Inventory items (IInventory)
            if (_inventory != null)
            {
                _inventory.OnItemAdded    += HandleInventoryItemAdded;
                _inventory.OnItemRemoved  += HandleInventoryItemRemoved;
                _inventory.OnSlotChanged  += HandleInventorySlotChanged;
            }
        }

        private void SubscribeBossEvents()
        {
            // Members 4/5 — Boss health and phases
            if (_bossHealth != null)
                _bossHealth.OnHealthChanged += UpdateBossHealth;

            if (_bossController != null)
            {
                _bossController.OnPhaseChanged  += HandleBossPhaseChanged;
                _bossController.OnBossEnraged   += HandleBossEnraged;
                _bossController.OnBossDefeated  += HandleBossDefeated;
            }

            // Member 5 — Boss armor (IBossArmorSource)
            if (_bossArmor != null)
            {
                _bossArmor.OnArmorChanged  += UpdateBossArmor;
                _bossArmor.OnArmorDepleted += HandleArmorDepleted;
            }
        }

        private void SubscribePuzzleEvents()
        {
            if (_puzzle != null)
            {
                _puzzle.OnPuzzleStateChanged += HandlePuzzleStateChanged;
                _puzzle.OnPuzzleSolved       += HandlePuzzleSolved;
                _puzzle.OnPuzzleFailed       += HandlePuzzleFailed;
            }
        }

        private void SubscribeCraftingEvents()
        {
            // Member 10 — DeathStorm crafting countdown (ICraftingCountdownSource)
            if (_crafting != null)
            {
                _crafting.OnTimerTick         += UpdateCraftingCountdown;
                _crafting.OnInstructionChanged += HandleCraftingInstructionChanged;
                _crafting.OnCraftingSucceeded  += HandleCraftingSucceeded;
                _crafting.OnCountdownExpired   += HandleCountdownExpired;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdateHealth;
                _playerHealth.OnDied          -= HandlePlayerDied;
            }

            if (_weapon != null)         _weapon.OnAmmoChanged          -= UpdateAmmo;
            if (_weaponHandler != null)
            {
                _weaponHandler.OnWeaponEquipped  -= HandleWeaponEquipped;
                _weaponHandler.OnWeaponHolstered -= HandleWeaponHolstered;
            }

            if (_inventory != null)
            {
                _inventory.OnItemAdded   -= HandleInventoryItemAdded;
                _inventory.OnItemRemoved -= HandleInventoryItemRemoved;
                _inventory.OnSlotChanged -= HandleInventorySlotChanged;
            }

            if (_bossHealth != null)     _bossHealth.OnHealthChanged     -= UpdateBossHealth;
            if (_bossController != null)
            {
                _bossController.OnPhaseChanged  -= HandleBossPhaseChanged;
                _bossController.OnBossEnraged   -= HandleBossEnraged;
                _bossController.OnBossDefeated  -= HandleBossDefeated;
            }

            if (_bossArmor != null)
            {
                _bossArmor.OnArmorChanged  -= UpdateBossArmor;
                _bossArmor.OnArmorDepleted -= HandleArmorDepleted;
            }

            if (_puzzle != null)
            {
                _puzzle.OnPuzzleStateChanged -= HandlePuzzleStateChanged;
                _puzzle.OnPuzzleSolved       -= HandlePuzzleSolved;
                _puzzle.OnPuzzleFailed       -= HandlePuzzleFailed;
            }

            if (_crafting != null)
            {
                _crafting.OnTimerTick          -= UpdateCraftingCountdown;
                _crafting.OnInstructionChanged -= HandleCraftingInstructionChanged;
                _crafting.OnCraftingSucceeded  -= HandleCraftingSucceeded;
                _crafting.OnCountdownExpired   -= HandleCountdownExpired;
            }

            if (_gameManager != null)    _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Initial Telemetry Snapshot
        // ─────────────────────────────────────────────────────────────────

        private void RefreshInitialTelemetry()
        {
            if (_playerHealth != null)
                UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);

            if (_weapon != null)
            {
                UpdateAmmo(_weapon.CurrentAmmo, _weapon.MagazineCapacity);
                UpdateEquippedWeapon(_weapon.DisplayName);
            }

            if (_inventory != null)
                RefreshComponentIndicators();

            if (_bossHealth != null)
                ShowBossHealthBar("ALIEN", _bossHealth.CurrentHealth, _bossHealth.MaxHealth);

            if (_bossArmor != null)
                UpdateBossArmor(_bossArmor.CurrentArmor, _bossArmor.MaxArmor);

            if (_crafting != null)
            {
                UpdateCraftingCountdown(_crafting.CountdownRemaining, _crafting.CountdownTotal);
                if (!string.IsNullOrEmpty(_crafting.CurrentInstruction))
                    ShowPuzzleInstruction(_crafting.CurrentInstruction);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Component Indicators (Member 9 — IInventory)
        // ─────────────────────────────────────────────────────────────────

        private void RefreshComponentIndicators()
        {
            if (_inventory == null) return;
            SetText(_edgeIndicator,   FormatComponent("EDGE",   _inventory.GetItemCount(_edgeItemId)));
            SetText(_handleIndicator, FormatComponent("HANDLE", _inventory.GetItemCount(_handleItemId)));
            SetText(_coreIndicator,   FormatComponent("CORE",   _inventory.GetItemCount(_coreItemId)));
        }

        // ─────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────

        private void HandlePlayerDied(DamageInfo fatalDamage)
        {
            string reason = fatalDamage.Type switch
            {
                DamageType.Physical => "TRAUMA — HULL INTEGRITY LOST",
                DamageType.Toxic    => "BIOLOGICAL CONTAMINATION",
                DamageType.EMP      => "NEURAL SHUTDOWN — EMP BURST",
                _                   => "LIFE SIGNS LOST"
            };
            SetText(_deathReasonText, reason);
            SetActive(_deathPanel, true);
            _gameManager?.SetState(GameState.GameOver);
            OpenPanel(UIPanelType.GameOverModal);
        }

        private void HandleBossPhaseChanged(BossPhase phase)
        {
            string label = phase switch
            {
                BossPhase.Intro          => "ENTITY AWAKENING",
                BossPhase.Phase1         => "PHASE I — PREDATOR",
                BossPhase.Phase2         => "PHASE II — FRENZY",
                BossPhase.Phase3         => "PHASE III — APEX",
                BossPhase.VulnerableStun => "EXPOSED — STRIKE NOW!",
                BossPhase.Enraged        => "ENRAGED — VOID POUNCE ACTIVE!",
                BossPhase.Defeated       => "ENTITY NEUTRALIZED",
                _                        => string.Empty
            };
            if (!string.IsNullOrEmpty(label))
            {
                SetText(_bossPhaseText, label);
                ShowNotification(label, 3.5f);
            }
        }

        private void HandleBossEnraged()
        {
            ShowNotification("⚠  BOSS ENRAGED — VOID POUNCE ENABLED!", 5f);
        }

        private void HandleBossDefeated()
        {
            SetText(_victoryText, "DEATHSTORM STRIKE CONFIRMED\nTHE VOID HAS FALLEN SILENT.");
            SetActive(_victoryPanel, true);
            HideBossHealthBar();
            _gameManager?.SetState(GameState.Victory);
        }

        private void HandleArmorDepleted()
        {
            ShowNotification("ARMOR FRACTURED — CORE EXPOSED", 4f);
        }

        private void HandlePuzzleStateChanged(PuzzleState state)
        {
            ShowPuzzleInstruction($"PUZZLE STATUS: {state.ToString().ToUpperInvariant()}");
        }

        private void HandlePuzzleSolved() => ShowPuzzleSuccess();
        private void HandlePuzzleFailed() => ShowPuzzleFailure();

        private void HandleCraftingInstructionChanged(string instruction)
        {
            SetText(_craftingStageText, instruction);
        }

        private void HandleCraftingSucceeded()
        {
            ShowPuzzleSuccess("DEATHSTORM FORGED");
            ShowNotification("★  DEATHSTORM ONLINE — FINISH THE HUNT!", 5f);
            SetActive(_craftingPanel, false);
        }

        private void HandleCountdownExpired(bool firstTimeout)
        {
            ShowPuzzleFailure(firstTimeout ? "SYNTHESIS TIMEOUT — ENRAGE DETECTED" : "SYNTHESIS TIMEOUT");
            ShowNotification(firstTimeout ? "⚠  VOID POUNCE ENABLED" : "CRAFTING WINDOW LOST", 5f);
        }

        private void HandleWeaponEquipped(IWeapon weapon)
        {
            if (weapon == null) return;
            UpdateEquippedWeapon(weapon.DisplayName);
            UpdateAmmo(weapon.CurrentAmmo, weapon.MagazineCapacity);
        }

        private void HandleWeaponHolstered() => UpdateEquippedWeapon("UNARMED");

        private void HandleInventoryItemAdded(IItem item)    => RefreshComponentIndicators();
        private void HandleInventoryItemRemoved(string id, int qty) => RefreshComponentIndicators();
        private void HandleInventorySlotChanged(int slot, IItem item) => RefreshComponentIndicators();

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver) SetActive(_deathPanel, true);
            if (state == GameState.Victory)  SetActive(_victoryPanel, true);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Panel Management
        // ─────────────────────────────────────────────────────────────────

        private void OpenPanel(UIPanelType panel)
        {
            if (ActivePanel == panel) return;
            if (ActivePanel != UIPanelType.None) OnPanelClosed?.Invoke(ActivePanel);
            ActivePanel = panel;
            OnPanelOpened?.Invoke(panel);
        }

        private void ClosePanel(UIPanelType panel)
        {
            if (ActivePanel != panel) return;
            ActivePanel = UIPanelType.None;
            OnPanelClosed?.Invoke(panel);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Coroutines
        // ─────────────────────────────────────────────────────────────────

        private IEnumerator NotificationRoutine(string message, float duration)
        {
            SetText(_notificationText, message);
            SetActive(_notificationText?.gameObject, true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            SetActive(_notificationText?.gameObject, false);
            _notificationRoutine = null;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Validation
        // ─────────────────────────────────────────────────────────────────

        private void ValidateReferences()
        {
            WarnMissing(_healthText,          "Health Text");
            WarnMissing(_ammoText,            "Ammo Text");
            WarnMissing(_edgeIndicator,       "Edge Indicator");
            WarnMissing(_handleIndicator,     "Handle Indicator");
            WarnMissing(_coreIndicator,       "Core Indicator");
            WarnMissing(_countdownText,       "Countdown Text");
            WarnMissing(_playerHealthSource,  "Player Health Source (Member 2)");
            WarnMissing(_inventorySource,     "Inventory Source (Member 9)");
            WarnMissing(_craftingSource,      "Crafting Source (Member 10)");
        }

        private void WarnMissing(UnityEngine.Object reference, string label)
        {
            if (reference == null)
                Debug.LogWarning($"[OblivionUIController] Missing Inspector reference: {label}.", this);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Utility Helpers
        // ─────────────────────────────────────────────────────────────────

        private static float  Normalize(float value, float max) =>
            max <= 0f ? 0f : Mathf.Clamp01(value / max);

        private static string FormatComponent(string name, int qty) =>
            qty > 0 ? $"{name}  [■ ONLINE]" : $"{name}  [□ MISSING]";

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }

        private static void SetFill(Image image, float value)
        {
            if (image != null) image.fillAmount = value;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
