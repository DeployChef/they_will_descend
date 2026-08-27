using TheyWillDescend.Infrastructure.Logging;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    /// <summary>
    /// Drains one-shot place-reject facts into the log. Not a view board.
    /// </summary>
    public sealed class BuildingRejectLog : MonoBehaviour
    {
        void LateUpdate()
        {
            if (!SimWorld.TryGet(out var em, out var bag) || !em.HasBuffer<BuildingRejectedEvent>(bag))
                return;

            var rejected = em.GetBuffer<BuildingRejectedEvent>(bag);
            for (var i = 0; i < rejected.Length; i++)
            {
                var row = rejected[i];
                GameLog.Warning(
                    $"Building rejected ({ReasonText(row.Reason)}) c={row.AnchorCluster} r={row.AnchorRadial}.");
            }

            rejected.Clear();
        }

        static string ReasonText(byte reason)
        {
            return reason switch
            {
                BuildingRejectedEvent.UnknownType => "unknown type",
                BuildingRejectedEvent.InvalidCell => "invalid cell",
                BuildingRejectedEvent.Overlap => "overlap",
                BuildingRejectedEvent.Unaffordable => "not enough resources",
                _ => "rejected"
            };
        }
    }
}
