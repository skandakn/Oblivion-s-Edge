namespace OblivionsEdge.Weapons
{
    /// <summary>
    /// Contract for character/entity that holds, equips, and fires weapons.
    /// Bridges input and the currently equipped weapon.
    /// </summary>
    public interface IWeaponHandler
    {
        IWeapon CurrentWeapon { get; }
        bool HasWeaponEquipped { get; }

        bool EquipWeapon(IWeapon weapon);
        void HolsterCurrentWeapon();
        void TriggerPrimaryFire();
        void TriggerReload();

        event System.Action<IWeapon> OnWeaponEquipped;
        event System.Action OnWeaponHolstered;
    }
}
