using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Weapons
{
    /// <summary>
    /// Contract defining weapon behavior, ammunition management, and firing execution.
    /// To be implemented by Member 2 (Combat & Weapons mechanics).
    /// </summary>
    public interface IWeapon
    {
        string WeaponId { get; }
        string DisplayName { get; }
        WeaponType Type { get; }
        FireMode Mode { get; }

        int CurrentAmmo { get; }
        int MagazineCapacity { get; }
        float FireRate { get; }
        float ReloadDuration { get; }
        bool CanFire { get; }
        bool IsReloading { get; }

        bool TryFire(Vector2 origin, Vector2 direction);
        bool StartReload();
        void CancelReload();
        void AddAmmo(int amount);

        event System.Action OnFired;
        event System.Action<int, int> OnAmmoChanged; // (currentClip, magazineCapacity)
        event System.Action OnReloadStarted;
        event System.Action OnReloadFinished;
        event System.Action OnDryFire; // Attempted fire with empty mag
    }
}
