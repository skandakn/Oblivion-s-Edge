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
        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _controlsPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _craftingPanel;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _puzzlePanel;
        [SerializeField] private GameObject _deathPanel;
        [SerializeField] private GameObject _victoryPanel;

        [Header("HUD Readouts")]
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private TMP_Text _weaponText;
        [SerializeField] private TMP_Text _edgeIndicator;
        [SerializeField] private TMP_Text _handleIndicator;
        [SerializeField] private TMP_Text _coreIndicator;
        [SerializeField] private GameObject _bossBarPanel;
        [SerializeField] private TMP_Text _bossNameText;
        [SerializeField] private Image _bossHealthFill;
        [SerializeField] private Image _bossArmorFill;
        [SerializeField] private TMP_Text _bossArmorText;
        [SerializeField] private TMP_Text _interactionPromptText;
        [SerializeField] private GameObject _interactionPromptPanel;

        [Header("Crafting and Puzzle")]
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private Image _countdownFill;
        [SerializeField] private TMP_Text _puzzleInstructionText;
        [SerializeField] private TMP_Text _puzzleFeedbackText;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _notificationText;
        [SerializeField] private TMP_Text _deathReasonText;
        [SerializeField] private TMP_Text _victoryText;

        [Header("Gameplay Event Sources")]
        [Tooltip("Component implementing IDamageable, normally the player health component.")]
        [SerializeField] private MonoBehaviour _playerHealthSource;
        [Tooltip("Component implementing IWeapon or IWeaponHandler.")]
        [SerializeField] private MonoBehaviour _weaponSource;
        [Tooltip("Component implementing IInventory.")]
        [SerializeField] private MonoBehaviour _inventorySource;
        [Tooltip("Component implementing IBossController and IDamageable.")]
        [SerializeField] private MonoBehaviour _bossSource;
        [Tooltip("Component implementing IBossArmorSource.")]
        [SerializeField] private MonoBehaviour _bossArmorSource;
        [Tooltip("Component implementing IPuzzleController.")]
        [SerializeField] private MonoBehaviour _puzzleSource;
        [Tooltip("Component implementing ICraftingCountdownSource.")]
        [SerializeField] private MonoBehaviour _craftingSource;

        [Header("Component Item IDs")]
        [SerializeField] private string _edgeItemId = "deathstorm_edge";
        [SerializeField] private string _handleItemId = "deathstorm_handle";
        [SerializeField] private string _coreItemId = "deathstorm_core";
        [SerializeField] private string _gameplaySceneName = "MainScene";

        private IDamageable _playerHealth;
        private IWeapon _weapon;
        private IWeaponHandler _weaponHandler;
        private IInventory _inventory;
        private IDamageable _bossHealth;
        private IBossController _bossController;
        private IBossArmorSource _bossArmor;
        private IPuzzleController _puzzle;
        private ICraftingCountdownSource _crafting;
        private GameManager _gameManager;
        private Coroutine _notificationRoutine;

        public IHUDView HUD => this;
        public UIPanelType ActivePanel { get; private set; }
        public bool IsAnyModalOpen => ActivePanel != UIPanelType.None;

        public event Action<UIPanelType> OnPanelOpened;
        public event Action<UIPanelType> OnPanelClosed;

        private void Awake()
        {
            _playerHealth = _playerHealthSource as IDamageable;
            _weapon = _weaponSource as IWeapon;
            _weaponHandler = _weaponSource as IWeaponHandler;
            _inventory = _inventorySource as IInventory;
            _bossHealth = _bossSource as IDamageable;
            _bossController = _bossSource as IBossController;
            _bossArmor = _bossArmorSource as IBossArmorSource;
            _puzzle = _puzzleSource as IPuzzleController;
            _crafting = _craftingSource as ICraftingCountdownSource;
            _gameManager = GameManager.Instance;

            ValidateReferences();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RefreshInitialTelemetry();
        }

        private void Start()
        {
            // GameManager is persistent and can initialize after an enabled UI prefab.
            if (_gameManager == null && GameManager.Instance != null)
            {
                _gameManager = GameManager.Instance;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void StartGame()
        {
            SetActive(_mainMenuPanel, false);
            SetActive(_hudPanel, true);
            _gameManager?.SetState(GameState.InGame);
        }

        public void OpenControls()
        {
            SetActive(_controlsPanel, true);
        }

        public void CloseControls()
        {
            SetActive(_controlsPanel, false);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void RestartScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadGameplayScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_gameplaySceneName);
        }

        public void UpdateHealth(float current, float max)
        {
            float normalized = Normalize(current, max);
            SetText(_healthText, $"VITALS  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}");
            SetFill(_healthFill, normalized);
        }

        public void UpdateStamina(float current, float max) { }
        public void UpdateSanity(float current, float max) { }
        public void UpdateOxygen(float current, float max) { }

        public void UpdateAmmo(int currentMagazine, int totalReserve)
        {
            SetText(_ammoText, $"AMMO  {currentMagazine} / {totalReserve}");
        }

        public void UpdateEquippedWeapon(string weaponDisplayName)
        {
            SetText(_weaponText, string.IsNullOrWhiteSpace(weaponDisplayName) ? "UNARMED" : weaponDisplayName.ToUpperInvariant());
        }

        public void ShowInteractionPrompt(string promptText)
        {
            SetText(_interactionPromptText, promptText);
            SetActive(_interactionPromptPanel, !string.IsNullOrWhiteSpace(promptText));
        }

        public void HideInteractionPrompt()
        {
            SetActive(_interactionPromptPanel, false);
        }

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

        public void UpdateBossArmor(float currentArmor, float maxArmor)
        {
            SetFill(_bossArmorFill, Normalize(currentArmor, maxArmor));
            SetText(_bossArmorText, $"ARMOR  {Mathf.CeilToInt(currentArmor)} / {Mathf.CeilToInt(maxArmor)}");
        }

        public void ShowPuzzleInstruction(string instruction)
        {
            SetText(_puzzleInstructionText, instruction);
            SetActive(_puzzlePanel, true);
            OpenPanel(UIPanelType.PuzzleTerminal);
        }

        /// <summary>Displays a readable weapon note in the same contextual terminal panel as puzzle guidance.</summary>
        public void ShowWeaponNote(string note)
        {
            ShowPuzzleInstruction(note);
        }

        public void ShowPuzzleSuccess(string message = "PUZZLE SOLVED")
        {
            SetText(_puzzleFeedbackText, message);
        }

        public void ShowPuzzleFailure(string message = "PUZZLE FAILED")
        {
            SetText(_puzzleFeedbackText, message);
        }

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
            OpenPanel(UIPanelType.PauseMenu);
            _gameManager?.PauseGame();
        }

        public void ClosePauseMenu()
        {
            ClosePanel(UIPanelType.PauseMenu);
            _gameManager?.ResumeGame();
        }

        public void ShowNotification(string message, float durationSeconds = 3f)
        {
            if (_notificationRoutine != null)
            {
                StopCoroutine(_notificationRoutine);
            }

            _notificationRoutine = StartCoroutine(NotificationRoutine(message, durationSeconds));
        }

        public void CloseAllModals()
        {
            SetActive(_controlsPanel, false);
            SetActive(_craftingPanel, false);
            SetActive(_inventoryPanel, false);
            SetActive(_puzzlePanel, false);
            if (ActivePanel != UIPanelType.None)
            {
                UIPanelType closedPanel = ActivePanel;
                ActivePanel = UIPanelType.None;
                OnPanelClosed?.Invoke(closedPanel);
            }
        }

        private void SubscribeEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged += UpdateHealth;
                _playerHealth.OnDied += HandlePlayerDied;
            }

            if (_weapon != null)
            {
                _weapon.OnAmmoChanged += UpdateAmmo;
            }

            if (_weaponHandler != null)
            {
                _weaponHandler.OnWeaponEquipped += HandleWeaponEquipped;
                _weaponHandler.OnWeaponHolstered += HandleWeaponHolstered;
            }

            if (_inventory != null)
            {
                _inventory.OnItemAdded += HandleInventoryItemAdded;
                _inventory.OnItemRemoved += HandleInventoryItemRemoved;
                _inventory.OnSlotChanged += HandleInventorySlotChanged;
            }

            if (_bossHealth != null)
            {
                _bossHealth.OnHealthChanged += UpdateBossHealth;
            }

            if (_bossController != null)
            {
                _bossController.OnBossDefeated += HandleBossDefeated;
            }

            if (_bossArmor != null)
            {
                _bossArmor.OnArmorChanged += UpdateBossArmor;
                _bossArmor.OnArmorDepleted += HandleArmorDepleted;
            }

            if (_puzzle != null)
            {
                _puzzle.OnPuzzleStateChanged += HandlePuzzleStateChanged;
                _puzzle.OnPuzzleSolved += HandlePuzzleSolved;
                _puzzle.OnPuzzleFailed += HandlePuzzleFailed;
            }

            if (_crafting != null)
            {
                _crafting.OnTimerTick += UpdateCraftingCountdown;
                _crafting.OnInstructionChanged += ShowPuzzleInstruction;
                _crafting.OnCraftingSucceeded += HandleCraftingSucceeded;
                _crafting.OnCountdownExpired += HandleCountdownExpired;
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdateHealth;
                _playerHealth.OnDied -= HandlePlayerDied;
            }

            if (_weapon != null) _weapon.OnAmmoChanged -= UpdateAmmo;
            if (_weaponHandler != null)
            {
                _weaponHandler.OnWeaponEquipped -= HandleWeaponEquipped;
                _weaponHandler.OnWeaponHolstered -= HandleWeaponHolstered;
            }

            if (_inventory != null)
            {
                _inventory.OnItemAdded -= HandleInventoryItemAdded;
                _inventory.OnItemRemoved -= HandleInventoryItemRemoved;
                _inventory.OnSlotChanged -= HandleInventorySlotChanged;
            }

            if (_bossHealth != null) _bossHealth.OnHealthChanged -= UpdateBossHealth;
            if (_bossController != null) _bossController.OnBossDefeated -= HandleBossDefeated;
            if (_bossArmor != null)
            {
                _bossArmor.OnArmorChanged -= UpdateBossArmor;
                _bossArmor.OnArmorDepleted -= HandleArmorDepleted;
            }

            if (_puzzle != null)
            {
                _puzzle.OnPuzzleStateChanged -= HandlePuzzleStateChanged;
                _puzzle.OnPuzzleSolved -= HandlePuzzleSolved;
                _puzzle.OnPuzzleFailed -= HandlePuzzleFailed;
            }

            if (_crafting != null)
            {
                _crafting.OnTimerTick -= UpdateCraftingCountdown;
                _crafting.OnInstructionChanged -= ShowPuzzleInstruction;
                _crafting.OnCraftingSucceeded -= HandleCraftingSucceeded;
                _crafting.OnCountdownExpired -= HandleCountdownExpired;
            }

            if (_gameManager != null) _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void RefreshInitialTelemetry()
        {
            if (_playerHealth != null) UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            if (_weapon != null)
            {
                UpdateAmmo(_weapon.CurrentAmmo, _weapon.MagazineCapacity);
                UpdateEquippedWeapon(_weapon.DisplayName);
            }

            if (_inventory != null) RefreshComponentIndicators();
            if (_bossHealth != null) ShowBossHealthBar("ALIEN", _bossHealth.CurrentHealth, _bossHealth.MaxHealth);
            if (_bossArmor != null) UpdateBossArmor(_bossArmor.CurrentArmor, _bossArmor.MaxArmor);
            if (_crafting != null)
            {
                UpdateCraftingCountdown(_crafting.CountdownRemaining, _crafting.CountdownTotal);
                ShowPuzzleInstruction(_crafting.CurrentInstruction);
            }
        }

        private void RefreshComponentIndicators()
        {
            SetText(_edgeIndicator, FormatComponent("EDGE", _inventory.GetItemCount(_edgeItemId)));
            SetText(_handleIndicator, FormatComponent("HANDLE", _inventory.GetItemCount(_handleItemId)));
            SetText(_coreIndicator, FormatComponent("CORE", _inventory.GetItemCount(_coreItemId)));
        }

        private void UpdateCraftingCountdown(float remaining, float total)
        {
            SetText(_countdownText, $"DEATHSTORM SYNTHESIS  {Mathf.CeilToInt(Mathf.Max(0f, remaining)):00}s");
            SetFill(_countdownFill, Normalize(remaining, total));
            SetActive(_craftingPanel, total > 0f);
        }

        private void HandlePlayerDied(DamageInfo fatalDamage)
        {
            SetText(_deathReasonText, "LIFE SIGNS LOST");
            SetActive(_deathPanel, true);
            _gameManager?.SetState(GameState.GameOver);
            OpenPanel(UIPanelType.GameOverModal);
        }

        private void HandleBossDefeated()
        {
            SetText(_victoryText, "DEATHSTORM STRIKE CONFIRMED\nTHE VOID HAS FALLEN SILENT.");
            SetActive(_victoryPanel, true);
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

        private void HandlePuzzleSolved()
        {
            ShowPuzzleSuccess();
        }

        private void HandlePuzzleFailed()
        {
            ShowPuzzleFailure();
        }

        private void HandleCraftingSucceeded()
        {
            ShowPuzzleSuccess("DEATHSTORM FORGED");
            ShowNotification("DEATHSTORM ONLINE", 4f);
        }

        private void HandleCountdownExpired(bool firstTimeout)
        {
            ShowPuzzleFailure(firstTimeout ? "SYNTHESIS TIMEOUT — ENRAGE DETECTED" : "SYNTHESIS TIMEOUT");
            ShowNotification(firstTimeout ? "VOID POUNCE ENABLED" : "CRAFTING WINDOW LOST", 4f);
        }

        private void HandleWeaponEquipped(IWeapon weapon)
        {
            if (weapon == null) return;
            UpdateEquippedWeapon(weapon.DisplayName);
            UpdateAmmo(weapon.CurrentAmmo, weapon.MagazineCapacity);
        }

        private void HandleWeaponHolstered()
        {
            UpdateEquippedWeapon("UNARMED");
        }

        private void HandleInventoryItemAdded(IItem item) => RefreshComponentIndicators();
        private void HandleInventoryItemRemoved(string itemId, int quantity) => RefreshComponentIndicators();
        private void HandleInventorySlotChanged(int slot, IItem item) => RefreshComponentIndicators();

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver) SetActive(_deathPanel, true);
            if (state == GameState.Victory) SetActive(_victoryPanel, true);
        }

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

        private IEnumerator NotificationRoutine(string message, float duration)
        {
            SetText(_notificationText, message);
            SetActive(_notificationText == null ? null : _notificationText.gameObject, true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            SetActive(_notificationText == null ? null : _notificationText.gameObject, false);
            _notificationRoutine = null;
        }

        private void ValidateReferences()
        {
            WarnMissing(_healthText, "Health Text");
            WarnMissing(_ammoText, "Ammo Text");
            WarnMissing(_edgeIndicator, "Edge Indicator");
            WarnMissing(_handleIndicator, "Handle Indicator");
            WarnMissing(_coreIndicator, "Core Indicator");
            WarnMissing(_countdownText, "Countdown Text");
            WarnMissing(_playerHealthSource, "Player Health Source");
            WarnMissing(_inventorySource, "Inventory Source");
        }

        private void WarnMissing(UnityEngine.Object reference, string label)
        {
            if (reference == null) Debug.LogWarning($"[OblivionUIController] Missing Inspector reference: {label}.", this);
        }

        private static float Normalize(float value, float max) => max <= 0f ? 0f : Mathf.Clamp01(value / max);
        private static string FormatComponent(string name, int quantity) => quantity > 0 ? $"{name}  [ONLINE]" : $"{name}  [MISSING]";
        private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }
        private static void SetFill(Image image, float value) { if (image != null) image.fillAmount = value; }
        private static void SetActive(GameObject target, bool active) { if (target != null) target.SetActive(active); }
    }
}
