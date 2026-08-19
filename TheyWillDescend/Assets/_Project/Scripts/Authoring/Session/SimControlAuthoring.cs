using TheyWillDescend.Simulation.Agents;
using TheyWillDescend.Simulation.City;
using TheyWillDescend.Simulation.Io;
using TheyWillDescend.Simulation.Session;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Authoring.Session
{
    /// <summary>
    /// Bakes the session singleton: clock, grid, command buffers, entity stamps.
    /// </summary>
    public sealed class SimControlAuthoring : MonoBehaviour
    {
        [SerializeField] RadialGridConfig cityGrid = RadialGridConfig.Default;
        [SerializeField] float constructionDuration = 8f;
        [SerializeField] GameObject house6x2;
        [SerializeField] GameObject house2x2;

        class SimControlBaker : Baker<SimControlAuthoring>
        {
            public override void Bake(SimControlAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SimControl
                {
                    Mode = SimRunMode.Off,
                    Speed = 1,
                    DeltaTime = 0f
                });
                AddComponent(entity, new SimBridge());
                var config = authoring.cityGrid.IsValid ? authoring.cityGrid : RadialGridConfig.Default;
                AddComponent(entity, new CityGrid
                {
                    Config = config,
                    Center = float3.zero,
                    Ready = 1,
                    NextBuildingId = 1,
                    ConstructionDuration = authoring.constructionDuration > 0.001f
                        ? authoring.constructionDuration
                        : 8f
                });
                AddBuffer<SpawnAgentCommand>(entity);
                AddBuffer<PlaceBuildingCommand>(entity);
                AddBuffer<AssignWorkerCommand>(entity);
                AddBuffer<UnassignWorkerCommand>(entity);
                AddBuffer<BuildingRejectedEvent>(entity);
                AddBuffer<OccupiedCell>(entity);

                // Dynamic keeps LocalTransform. None lets TransformBakingSystem strip it,
                // then Instantiate has no pose and SetComponentData throws.
                var agentPrototype = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddComponent<Prefab>(agentPrototype);
                AddComponent(agentPrototype, new AgentLocomotion { Speed = 2f });
                AddComponent<AgentAssignment>(agentPrototype);
                AddComponent<AgentPlazaIdle>(agentPrototype);
                AddComponent(agentPrototype, new AgentId());
                AddComponent(agentPrototype, new AgentType { Kind = AgentKind.Worker });

                var house6 = BakeHouse(authoring.house6x2);
                var house2 = BakeHouse(authoring.house2x2);
                AddComponent(entity, new SimPrototypes
                {
                    Agent = agentPrototype,
                    House6x2 = house6,
                    House2x2 = house2,
                    House6x2MeshSize = HorizontalSize(authoring.house6x2),
                    House2x2MeshSize = HorizontalSize(authoring.house2x2)
                });
            }

            Entity BakeHouse(GameObject prefab)
            {
                if (prefab == null)
                    return Entity.Null;
                DependsOn(prefab);
                return GetEntity(prefab, TransformUsageFlags.Dynamic);
            }

            static float HorizontalSize(GameObject prefab)
            {
                if (prefab == null)
                    return 1f;
                var size = 0f;
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                for (var i = 0; i < filters.Length; i++)
                {
                    var mesh = filters[i].sharedMesh;
                    if (mesh == null)
                        continue;
                    var s = mesh.bounds.size;
                    var ls = filters[i].transform.localScale;
                    size = math.max(
                        size,
                        math.max(s.x * math.abs(ls.x), s.z * math.abs(ls.z)));
                }

                return size > 0.001f ? size : 1f;
            }
        }
    }
}
