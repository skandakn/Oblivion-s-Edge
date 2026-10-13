using UnityEngine;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Identifiers for the 7 primary functional zones of the spaceship level.
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public enum SpaceshipZoneType
    {
        StartingCorridor = 0,
        WeaponNoteLocation = 1,
        EdgePuzzleRoom = 2,
        HandlePuzzleRoom = 3,
        AlienNestArena = 4,
        CoreExtractionPoint = 5,
        ForgeArea = 6
    }

    /// <summary>
    /// Defines a distinct room or sector within the 2D spaceship.
    /// Assists in navigation validation, camera tracking, and environmental audio gating.
    /// </summary>
    [SelectionBase]
    public class SpaceshipZone : MonoBehaviour
    {
        [Header("Zone Identification")]
        [SerializeField] private SpaceshipZoneType _zoneType;
        [SerializeField] private string _zoneName = "Spaceship Sector";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "Section of the derelict starship.";

        [Header("Spatial Dimensions")]
        [SerializeField] private Vector2 _zoneCenter = Vector2.zero;
        [SerializeField] private Vector2 _zoneSize = new Vector2(10f, 6f);

        [Header("Connected Adjacent Zones")]
        [SerializeField] private SpaceshipZone[] _connectedZones;

        public SpaceshipZoneType ZoneType => _zoneType;
        public string ZoneName => _zoneName;
        public string Description => _description;
        public Vector2 ZoneCenter => (Vector2)transform.position + _zoneCenter;
        public Vector2 ZoneSize => _zoneSize;
        public SpaceshipZone[] ConnectedZones => _connectedZones;

        /// <summary>
        /// Checks whether a given 2D world position lies inside this zone's rectangular boundary.
        /// </summary>
        public bool ContainsPoint(Vector2 worldPosition)
        {
            Vector2 min = ZoneCenter - _zoneSize * 0.5f;
            Vector2 max = ZoneCenter + _zoneSize * 0.5f;
            return worldPosition.x >= min.x && worldPosition.x <= max.x &&
                   worldPosition.y >= min.y && worldPosition.y <= max.y;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GetZoneColor(_zoneType);
            Gizmos.DrawWireCube(ZoneCenter, _zoneSize);
        }

        private static Color GetZoneColor(SpaceshipZoneType type)
        {
            switch (type)
            {
                case SpaceshipZoneType.StartingCorridor:     return Color.cyan;
                case SpaceshipZoneType.WeaponNoteLocation:   return Color.yellow;
                case SpaceshipZoneType.EdgePuzzleRoom:       return new Color(0.2f, 0.8f, 0.2f);
                case SpaceshipZoneType.HandlePuzzleRoom:     return new Color(0.2f, 0.6f, 0.9f);
                case SpaceshipZoneType.AlienNestArena:       return new Color(0.9f, 0.2f, 0.2f);
                case SpaceshipZoneType.CoreExtractionPoint:  return new Color(1f, 0.5f, 0f);
                case SpaceshipZoneType.ForgeArea:            return new Color(0.6f, 0.2f, 1f); // Blue-violet
                default:                                     return Color.white;
            }
        }
    }
}
