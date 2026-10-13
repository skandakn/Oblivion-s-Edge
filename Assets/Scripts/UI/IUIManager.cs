using OblivionsEdge.Crafting;

namespace OblivionsEdge.UI
{
    /// <summary>
    /// Type of modal / panel currently active in UI.
    /// </summary>
    public enum UIPanelType
    {
        None,
        Inventory,
        Crafting,
        PauseMenu,
        PuzzleTerminal,
        AudioLogReader,
        GameOverModal
    }

    /// <summary>
    /// Contract defining global UI navigation, panel stacking, and HUD mediation.
    /// To be implemented by Member 6 (UI / Audio presentation).
    /// </summary>
    public interface IUIManager
    {
        IHUDView HUD { get; }
        UIPanelType ActivePanel { get; }
        bool IsAnyModalOpen { get; }

        void OpenInventory();
        void CloseInventory();

        void OpenCrafting(ICraftingStation station);
        void CloseCrafting();

        void OpenPauseMenu();
        void ClosePauseMenu();

        void ShowNotification(string message, float durationSeconds = 3f);
        void CloseAllModals();

        event System.Action<UIPanelType> OnPanelOpened;
        event System.Action<UIPanelType> OnPanelClosed;
    }
}
