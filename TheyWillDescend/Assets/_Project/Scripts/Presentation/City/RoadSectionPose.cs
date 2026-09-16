using Unity.Mathematics;
using UnityEngine;

namespace TheyWillDescend.Presentation.City
{
    public static class RoadSectionPose
    {
        public static void Apply(Transform visual, float groundY, float2 a, float2 b, float width)
        {
            var delta = b - a;
            var length = math.length(delta);
            if (length < 0.02f)
            {
                visual.gameObject.SetActive(false);
                return;
            }

            visual.gameObject.SetActive(true);
            var mid = (a + b) * 0.5f;
            var yaw = math.atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            visual.SetPositionAndRotation(
                new Vector3(mid.x, groundY, mid.y),
                Quaternion.Euler(90f, yaw, 0f));
            visual.localScale = new Vector3(
                math.max(0.05f, width),
                math.max(0.05f, length),
                1f);
        }
    }
}
