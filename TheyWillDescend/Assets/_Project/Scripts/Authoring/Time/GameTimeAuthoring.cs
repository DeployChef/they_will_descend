using TheyWillDescend.Simulation.Time;
using Unity.Entities;
using UnityEngine;

namespace TheyWillDescend.Authoring.Time
{
    public class GameTimeAuthoring : MonoBehaviour
    {
        [SerializeField] private float dayDuration = 5;

        // Вложенный класс — просто чтобы файл был один.
        // Unity сама находит все Baker<> при bake.
        class GameTimeBaker : Baker<GameTimeAuthoring>
        {
            public override void Bake(GameTimeAuthoring authoring)
            {
                // 1) Взять entity, которая соответствует этому GameObject
                //    None = нам не нужен LocalTransform (время — не куб в мире)
                var entity = GetEntity(TransformUsageFlags.None);
                // 2) Повесить runtime-данные, скопировав числа из Inspector
                AddComponent(entity, new GameTime
                {
                    Day = 0,
                    ElapsedInDay = 0f,
                    DayDuration = authoring.dayDuration
                });
            }
        }
    }
}
