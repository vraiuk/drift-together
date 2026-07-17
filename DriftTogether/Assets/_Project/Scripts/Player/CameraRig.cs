using DriftTogether.Core;
using UnityEngine;

namespace DriftTogether.Player
{
    /// <summary>
    /// Smooth third-person follow camera: slightly above and behind the kayak,
    /// avoids clipping through large objects, supports short collision shakes.
    /// Smoothing is driven by the settings menu.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Target;

        const float Distance = 8.5f;
        const float Height = 4.6f;
        const float LookAhead = 4f;

        Vector3 _velocity;
        float _shake;
        Vector3 _smoothedTargetPos;
        Quaternion _smoothedYaw = Quaternion.identity;

        public void Shake(float amount) => _shake = Mathf.Clamp01(_shake + amount);

        public void SnapBehindTarget()
        {
            if (Target == null)
                return;
            _smoothedTargetPos = Target.position;
            _smoothedYaw = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            transform.position = DesiredPosition();
            transform.rotation = Quaternion.LookRotation(
                (LookPoint() - transform.position).normalized);
        }

        Vector3 DesiredPosition()
        {
            return _smoothedTargetPos + _smoothedYaw * new Vector3(0f, Height, -Distance);
        }

        Vector3 LookPoint()
        {
            return _smoothedTargetPos + _smoothedYaw * new Vector3(0f, 1.1f, LookAhead);
        }

        void LateUpdate()
        {
            if (Target == null)
                return;

            // 0 = lazy/smooth, 1 = tight.
            float tightness = Mathf.Lerp(2.2f, 9f, GameSettings.CameraSmoothing);
            float dt = Time.deltaTime;

            _smoothedTargetPos = Vector3.Lerp(_smoothedTargetPos, Target.position,
                1f - Mathf.Exp(-tightness * dt));
            Quaternion targetYaw = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            _smoothedYaw = Quaternion.Slerp(_smoothedYaw, targetYaw,
                1f - Mathf.Exp(-tightness * 0.75f * dt));

            Vector3 desired = DesiredPosition();

            // Keep the camera out of big geometry.
            Vector3 pivot = _smoothedTargetPos + Vector3.up * 1.4f;
            Vector3 dir = desired - pivot;
            if (Physics.SphereCast(pivot, 0.45f, dir.normalized, out RaycastHit hit,
                    dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == null)
                    desired = pivot + dir.normalized * Mathf.Max(hit.distance - 0.25f, 2f);
            }

            // Collision shake, quickly decaying.
            _shake = Mathf.Max(0f, _shake - dt * 2.6f);
            Vector3 shakeOffset = Random.insideUnitSphere * _shake * 0.35f;

            transform.position = desired + shakeOffset;
            Quaternion lookRot = Quaternion.LookRotation((LookPoint() - desired).normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot,
                1f - Mathf.Exp(-tightness * dt));
        }
    }
}
