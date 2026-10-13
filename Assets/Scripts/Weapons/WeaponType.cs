namespace OblivionsEdge.Weapons
{
    /// <summary>
    /// Archetypes of weapons available in the survival horror arsenal.
    /// </summary>
    public enum WeaponType
    {
        MeleeTool,
        Pistol,
        Shotgun,
        PlasmaCutter,
        Flamethrower,
        StunBaton,
        ShockRifle
    }

    /// <summary>
    /// Firing modes supported by projectile/energy weapons.
    /// </summary>
    public enum FireMode
    {
        SemiAuto,
        Burst,
        FullAuto,
        Charged,
        ContinuousBeam
    }
}
