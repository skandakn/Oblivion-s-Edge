namespace OblivionsEdge.Core
{
    /// <summary>
    /// Centralized project-wide constants for tags, layers, sorting layers, and input mappings.
    /// Maintain this file as the authoritative contract for game engine identifiers.
    /// </summary>
    public static class GameConstants
    {
        // -------------------------------------------------------------
        // Unity Tags
        // -------------------------------------------------------------
        public const string TagUntagged = "Untagged";
        public const string TagMainCamera = "MainCamera";
        public const string TagPlayer = "Player";
        public const string TagGameController = "GameController";
        public const string TagEnemy = "Enemy";
        public const string TagBoss = "Boss";
        public const string TagWeapon = "Weapon";
        public const string TagProjectile = "Projectile";
        public const string TagInteractable = "Interactable";
        public const string TagItemPickup = "ItemPickup";
        public const string TagSaveStation = "SaveStation";
        public const string TagHazard = "Hazard";

        // -------------------------------------------------------------
        // Unity Layer Names
        // -------------------------------------------------------------
        public const string LayerNameDefault = "Default";
        public const string LayerNameTransparentFX = "TransparentFX";
        public const string LayerNameIgnoreRaycast = "Ignore Raycast";
        public const string LayerNameWater = "Water";
        public const string LayerNameUI = "UI";
        public const string LayerNamePlayer = "Player";
        public const string LayerNameEnemy = "Enemy";
        public const string LayerNameEnvironment = "Environment";
        public const string LayerNameInteractable = "Interactable";
        public const string LayerNamePlayerProjectile = "PlayerProjectile";
        public const string LayerNameEnemyProjectile = "EnemyProjectile";
        public const string LayerNameItemPickup = "ItemPickup";
        public const string LayerNameTriggerZone = "TriggerZone";

        // -------------------------------------------------------------
        // Unity Layer Indices
        // -------------------------------------------------------------
        public const int LayerDefault = 0;
        public const int LayerTransparentFX = 1;
        public const int LayerIgnoreRaycast = 2;
        public const int LayerWater = 4;
        public const int LayerUI = 5;
        public const int LayerPlayerIndex = 6;
        public const int LayerEnemyIndex = 7;
        public const int LayerEnvironmentIndex = 8;
        public const int LayerInteractableIndex = 9;
        public const int LayerPlayerProjectileIndex = 10;
        public const int LayerEnemyProjectileIndex = 11;
        public const int LayerItemPickupIndex = 12;
        public const int LayerTriggerZoneIndex = 13;

        // -------------------------------------------------------------
        // 2D Sorting Layer Names
        // -------------------------------------------------------------
        public const string SortingDefault = "Default";
        public const string SortingBackground = "Background";
        public const string SortingEnvironment = "Environment";
        public const string SortingItems = "Items";
        public const string SortingCharacters = "Characters";
        public const string SortingProjectiles = "Projectiles";
        public const string SortingForeground = "Foreground";
        public const string SortingUI = "UI";

        // -------------------------------------------------------------
        // Input Axis / Button Names
        // -------------------------------------------------------------
        public const string InputHorizontal = "Horizontal";
        public const string InputVertical = "Vertical";
        public const string InputFirePrimary = "Fire1";
        public const string InputFireSecondary = "Fire2";
        public const string InputInteract = "Interact";
        public const string InputReload = "Reload";
        public const string InputInventory = "Inventory";
        public const string InputCrafting = "Crafting";
    }
}
