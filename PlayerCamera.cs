using UnityEngine;

namespace ActionRPG.Character
{
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Target & Framing")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private float followSmoothTime = 0.05f;

        [Header("Orbit & Sensitivity")]
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Scroll Zoom Settings")]
        [SerializeField] private float defaultZoom = 4.5f;
        [SerializeField] private float minZoom = 1.5f;
        [SerializeField] private float maxZoom = 8.0f;
        [SerializeField] private float zoomStepSize = 0.75f;
        [SerializeField] private float zoomSmoothTime = 0.15f;

        [Header("Collision & Clipping Prevention")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float collisionRadius = 0.25f;

        private PlayerInputReader input;
        private float yaw;
        private float pitch;

        private float targetZoomDistance;
        private float currentZoomDistance;
        private float zoomVelocity;

        private Vector3 currentAnchorPos;
        private Vector3 anchorVelocity;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            targetZoomDistance = defaultZoom;
            currentZoomDistance = defaultZoom;

            if (target != null)
            {
                input = target.GetComponent<PlayerInputReader>();
                currentAnchorPos = target.position + targetOffset;
                yaw = target.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (target == null || input == null) return;

            if (!input.GameplayInputBlocked)
            {
                HandleOrbitInput();
                HandleScrollZoom();
            }
            else
            {
                currentZoomDistance = Mathf.SmoothDamp(currentZoomDistance, targetZoomDistance, ref zoomVelocity, zoomSmoothTime);
            }

            UpdateCameraTransform();
        }

        private void HandleOrbitInput()
        {
            Vector2 look = input.LookInput;
            yaw += look.x * lookSensitivity;
            pitch -= look.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void HandleScrollZoom()
        {
            if (Mathf.Abs(input.ZoomInput) > 0.01f)
            {
                targetZoomDistance -= input.ZoomInput * zoomStepSize;
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoom, maxZoom);
            }

            currentZoomDistance = Mathf.SmoothDamp(
                currentZoomDistance,
                targetZoomDistance,
                ref zoomVelocity,
                zoomSmoothTime
            );
        }

        private void UpdateCameraTransform()
        {
            Vector3 desiredAnchorPos = target.position + targetOffset;
            currentAnchorPos = Vector3.SmoothDamp(
                currentAnchorPos,
                desiredAnchorPos,
                ref anchorVelocity,
                followSmoothTime
            );

            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 cameraDirection = orbitRotation * Vector3.back;

            float finalDistance = ResolveCameraCollision(currentAnchorPos, cameraDirection, currentZoomDistance);

            transform.position = currentAnchorPos + (cameraDirection * finalDistance);
            transform.rotation = orbitRotation;
        }

        private float ResolveCameraCollision(Vector3 anchor, Vector3 direction, float desiredDistance)
        {
            if (Physics.SphereCast(
                anchor,
                collisionRadius,
                direction,
                out RaycastHit hit,
                desiredDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
            {
                return Mathf.Clamp(hit.distance - 0.05f, 0.2f, desiredDistance);
            }

            return desiredDistance;
        }
    }
}