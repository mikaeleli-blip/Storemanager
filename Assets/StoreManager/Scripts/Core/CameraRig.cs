using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Top-down camera. Follows the manager on the floor; free-flies (WASD) when the
    /// manager sits at the office desk. Runs on unscaled time so it works while paused.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Vector3 Focus;
        public float Yaw;
        public float Pitch = 55f;
        public float Distance = 22f;
        public bool FreeMode;
        public Transform FollowTarget;
        public Vector2 BoundsMin, BoundsMax;

        public const float MinDistance = 7f;
        public const float MaxDistance = 85f;

        public Vector3 FlatForward { get { return Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward; } }
        public Vector3 FlatRight { get { return Quaternion.Euler(0f, Yaw, 0f) * Vector3.right; } }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (GameInput.Held(GameKey.Q)) Yaw += 90f * dt;
            if (GameInput.Held(GameKey.E)) Yaw -= 90f * dt;

            float scroll = GameInput.Scroll;
            if (Mathf.Abs(scroll) > 0.001f && !Hud.PointerOverUI)
                Distance = Mathf.Clamp(Distance * (1f - scroll * 0.12f), MinDistance, MaxDistance);

            if (FreeMode)
            {
                Vector3 move = Vector3.zero;
                if (GameInput.Held(GameKey.W)) move += FlatForward;
                if (GameInput.Held(GameKey.S)) move -= FlatForward;
                if (GameInput.Held(GameKey.D)) move += FlatRight;
                if (GameInput.Held(GameKey.A)) move -= FlatRight;
                if (move.sqrMagnitude > 0f)
                    Focus += move.normalized * (8f + Distance * 0.9f) * dt;
            }
            else if (FollowTarget != null)
            {
                Focus = Vector3.Lerp(Focus, FollowTarget.position, 1f - Mathf.Exp(-8f * dt));
            }

            Focus.x = Mathf.Clamp(Focus.x, BoundsMin.x, BoundsMax.x);
            Focus.z = Mathf.Clamp(Focus.z, BoundsMin.y, BoundsMax.y);
            Focus.y = 0f;

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.position = Focus - rot * Vector3.forward * Distance;
            transform.rotation = rot;
        }
    }
}
