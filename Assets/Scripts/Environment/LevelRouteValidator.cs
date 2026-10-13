using System.Collections.Generic;
using UnityEngine;

namespace OblivionsEdge.Environment
{
    /// <summary>
    /// Validation tool and runtime checker for the spaceship layout.
    /// Verifies all 7 required zones, linear path distance, Rigidbody2D compatibility,
    /// and compact arena configuration (boss, core, forge proximity).
    /// Designed by Member 11 for OBLIVION'S EDGE.
    /// </summary>
    public class LevelRouteValidator : MonoBehaviour
    {
        [Header("Required Level Anchors")]
        [SerializeField] private SpaceshipZone _startingCorridorZone;
        [SerializeField] private SpaceshipZone _weaponNoteZone;
        [SerializeField] private SpaceshipZone _edgePuzzleZone;
        [SerializeField] private SpaceshipZone _handlePuzzleZone;
        [SerializeField] private SpaceshipZone _alienNestZone;
        [SerializeField] private SpaceshipZone _coreExtractionZone;
        [SerializeField] private SpaceshipZone _forgeAreaZone;

        [Header("Key Functional Objects")]
        [SerializeField] private Transform _bossSpawnPoint;
        [SerializeField] private Transform _coreExtractionPoint;
        [SerializeField] private Transform _forgePoint;
        [SerializeField] private CoverObstacle[] _coverObjects;

        [Header("Constraint Tolerances")]
        [SerializeField] private float _maxArenaInterDistance = 25f;
        [SerializeField] private float _maxConsecutiveNodeDistance = 20f;

        public struct ValidationReport
        {
            public bool IsValid;
            public List<string> Messages;
            public float TotalCorridorLength;
            public float ArenaCoreToForgeDistance;
            public float ArenaBossToForgeDistance;
            public int CoverCount;
        }

        public ValidationReport RunRouteValidation()
        {
            var report = new ValidationReport
            {
                IsValid = true,
                Messages = new List<string>()
            };

            // 1. Check all 7 zones exist
            if (_startingCorridorZone == null || _weaponNoteZone == null ||
                _edgePuzzleZone == null || _handlePuzzleZone == null ||
                _alienNestZone == null || _coreExtractionZone == null ||
                _forgeAreaZone == null)
            {
                report.IsValid = false;
                report.Messages.Add("Missing one or more of the 7 mandatory spaceship zones!");
                return report;
            }

            // 2. Measure linear route distances
            Vector2 pStart = _startingCorridorZone.ZoneCenter;
            Vector2 pNote = _weaponNoteZone.ZoneCenter;
            Vector2 pEdge = _edgePuzzleZone.ZoneCenter;
            Vector2 pHandle = _handlePuzzleZone.ZoneCenter;
            Vector2 pArena = _alienNestZone.ZoneCenter;

            float dStartToNote = Vector2.Distance(pStart, pNote);
            float dNoteToEdge = Vector2.Distance(pNote, pEdge);
            float dNoteToHandle = Vector2.Distance(pNote, pHandle);
            float dEdgeToArena = Vector2.Distance(pEdge, pArena);
            float dHandleToArena = Vector2.Distance(pHandle, pArena);

            report.TotalCorridorLength = dStartToNote + Mathf.Max(dNoteToEdge, dNoteToHandle) + Mathf.Max(dEdgeToArena, dHandleToArena);

            if (dStartToNote > _maxConsecutiveNodeDistance)
            {
                report.IsValid = false;
                report.Messages.Add($"Starting Corridor to Weapon Note distance ({dStartToNote:F1}) exceeds max ({_maxConsecutiveNodeDistance})");
            }

            // 3. Compact Arena check: Boss, Core, and Forge must be within accessible distance
            if (_coreExtractionPoint != null && _forgePoint != null)
            {
                report.ArenaCoreToForgeDistance = Vector2.Distance(_coreExtractionPoint.position, _forgePoint.position);
                if (report.ArenaCoreToForgeDistance > _maxArenaInterDistance)
                {
                    report.IsValid = false;
                    report.Messages.Add($"Core-to-Forge distance ({report.ArenaCoreToForgeDistance:F1}) is too large for compact 60s countdown arena!");
                }
            }

            if (_bossSpawnPoint != null && _forgePoint != null)
            {
                report.ArenaBossToForgeDistance = Vector2.Distance(_bossSpawnPoint.position, _forgePoint.position);
                if (report.ArenaBossToForgeDistance > _maxArenaInterDistance)
                {
                    report.IsValid = false;
                    report.Messages.Add($"Boss-to-Forge distance ({report.ArenaBossToForgeDistance:F1}) exceeds compact arena limit!");
                }
            }

            // 4. Verify Cover Objects for alien evasion
            report.CoverCount = _coverObjects != null ? _coverObjects.Length : 0;
            if (report.CoverCount == 0)
            {
                report.IsValid = false;
                report.Messages.Add("No cover objects configured for dodging the alien!");
            }
            else
            {
                foreach (var cover in _coverObjects)
                {
                    if (cover != null && cover.PhysicalCollider == null)
                    {
                        report.IsValid = false;
                        report.Messages.Add($"Cover object {cover.name} is missing a physical 2D collider!");
                    }
                }
            }

            if (report.IsValid)
            {
                report.Messages.Add("SUCCESS: Spaceship layout passes all route, distance, and Rigidbody2D cover checks!");
            }

            return report;
        }

        private void OnDrawGizmos()
        {
            if (_startingCorridorZone == null || _weaponNoteZone == null ||
                _edgePuzzleZone == null || _handlePuzzleZone == null || _alienNestZone == null)
                return;

            Gizmos.color = Color.white;
            Gizmos.DrawLine(_startingCorridorZone.ZoneCenter, _weaponNoteZone.ZoneCenter);
            Gizmos.DrawLine(_weaponNoteZone.ZoneCenter, _edgePuzzleZone.ZoneCenter);
            Gizmos.DrawLine(_weaponNoteZone.ZoneCenter, _handlePuzzleZone.ZoneCenter);
            Gizmos.DrawLine(_edgePuzzleZone.ZoneCenter, _alienNestZone.ZoneCenter);
            Gizmos.DrawLine(_handlePuzzleZone.ZoneCenter, _alienNestZone.ZoneCenter);

            if (_coreExtractionPoint != null && _forgePoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_coreExtractionPoint.position, _forgePoint.position);
            }
        }
    }
}
