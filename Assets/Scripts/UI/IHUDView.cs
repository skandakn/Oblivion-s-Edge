namespace OblivionsEdge.UI
{
    /// <summary>
    /// Contract defining player Heads-Up Display (HUD) presentation and readouts.
    /// To be implemented by Member 6 (UI / Audio presentation).
    /// </summary>
    public interface IHUDView
    {
        void UpdateHealth(float current, float max);
        void UpdateStamina(float current, float max);
        void UpdateSanity(float current, float max);
        void UpdateOxygen(float current, float max);

        void UpdateAmmo(int currentMagazine, int totalReserve);
        void UpdateEquippedWeapon(string weaponDisplayName);

        void ShowInteractionPrompt(string promptText);
        void HideInteractionPrompt();

        void ShowBossHealthBar(string bossName, float currentHealth, float maxHealth);
        void UpdateBossHealth(float currentHealth, float maxHealth);
        void HideBossHealthBar();
    }
}
