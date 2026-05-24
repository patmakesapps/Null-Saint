using UnityEngine;

public class SideViewCameraFollow : MonoBehaviour
{
    public enum FollowAxis
    {
        WorldX,
        WorldZ
    }

    [Header("Follow")]
    public Transform target;
    public FollowAxis horizontalAxis = FollowAxis.WorldZ;
    public bool preserveStartingPosition = true;
    public Vector3 offset = new Vector3(12f, 2.2f, 0f);
    public float horizontalSmoothTime = 0.22f;
    public float verticalSmoothTime = 0.32f;
    public float depthSmoothTime = 0.18f;
    public float lookAheadDistance = 2.25f;
    public float lookAheadSpeed = 4f;

    [Header("Adventure Zoom")]
    public bool enableDynamicZoom = true;
    public bool preserveStartingFieldOfView = true;
    public float idleFieldOfView = 47f;
    public float runFieldOfView = 54f;
    public float jumpFieldOfView = 57f;
    public float zoomSmoothTime = 0.45f;
    public float pulseZoomAmount = 5f;
    public float pulseReturnSpeed = 2.5f;
    public float ambientPulseInterval = 7f;
    public float ambientPulseAmount = 1.5f;

    [Header("Bounds")]
    public bool useBounds;
    public Vector2 minPosition = new Vector2(-100f, -20f);
    public Vector2 maxPosition = new Vector2(100f, 50f);

    private Camera followCamera;
    private PlayerMovement playerMovement;
    private float horizontalVelocity;
    private float verticalVelocity;
    private float depthVelocity;
    private float zoomVelocity;
    private Vector3 previousTargetPosition;
    private float lookAhead;
    private float pulseZoom;
    private float ambientPulseTimer;

    void Start()
    {
        followCamera = GetComponent<Camera>();

        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();

            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target == null)
        {
            return;
        }

        playerMovement = target.GetComponent<PlayerMovement>();
        previousTargetPosition = target.position;
        ambientPulseTimer = ambientPulseInterval;

        if (preserveStartingPosition)
        {
            offset = transform.position - target.position;
        }
        else
        {
            transform.position = GetDesiredPosition();
        }

        if (followCamera != null && preserveStartingFieldOfView)
        {
            idleFieldOfView = followCamera.fieldOfView;
            runFieldOfView = Mathf.Max(runFieldOfView, idleFieldOfView + 7f);
            jumpFieldOfView = Mathf.Max(jumpFieldOfView, idleFieldOfView + 10f);
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float targetDelta = horizontalAxis == FollowAxis.WorldZ
            ? target.position.z - previousTargetPosition.z
            : target.position.x - previousTargetPosition.x;

        if (Mathf.Abs(targetDelta) > 0.001f)
        {
            float targetLookAhead = Mathf.Sign(targetDelta) * lookAheadDistance;
            lookAhead = Mathf.MoveTowards(lookAhead, targetLookAhead, lookAheadSpeed * Time.deltaTime);
        }
        else
        {
            lookAhead = Mathf.MoveTowards(lookAhead, 0f, lookAheadSpeed * Time.deltaTime);
        }

        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = SmoothDampByAxis(transform.position, desiredPosition);
        UpdateZoom(targetDelta);
        previousTargetPosition = target.position;
    }

    public void PulseZoom()
    {
        pulseZoom = Mathf.Max(pulseZoom, pulseZoomAmount);
    }

    private Vector3 GetDesiredPosition()
    {
        Vector3 desiredPosition = target.position + offset + GetHorizontalAxis() * lookAhead;

        if (useBounds)
        {
            if (horizontalAxis == FollowAxis.WorldZ)
            {
                desiredPosition.z = Mathf.Clamp(desiredPosition.z, minPosition.x, maxPosition.x);
            }
            else
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minPosition.x, maxPosition.x);
            }

            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minPosition.y, maxPosition.y);
        }

        return desiredPosition;
    }

    private Vector3 GetHorizontalAxis()
    {
        return horizontalAxis == FollowAxis.WorldZ ? Vector3.forward : Vector3.right;
    }

    private Vector3 SmoothDampByAxis(Vector3 currentPosition, Vector3 desiredPosition)
    {
        Vector3 result = currentPosition;

        if (horizontalAxis == FollowAxis.WorldZ)
        {
            result.z = Mathf.SmoothDamp(currentPosition.z, desiredPosition.z, ref horizontalVelocity, horizontalSmoothTime);
            result.x = Mathf.SmoothDamp(currentPosition.x, desiredPosition.x, ref depthVelocity, depthSmoothTime);
        }
        else
        {
            result.x = Mathf.SmoothDamp(currentPosition.x, desiredPosition.x, ref horizontalVelocity, horizontalSmoothTime);
            result.z = Mathf.SmoothDamp(currentPosition.z, desiredPosition.z, ref depthVelocity, depthSmoothTime);
        }

        result.y = Mathf.SmoothDamp(currentPosition.y, desiredPosition.y, ref verticalVelocity, verticalSmoothTime);
        return result;
    }

    private void UpdateZoom(float targetDelta)
    {
        if (!enableDynamicZoom || followCamera == null)
        {
            return;
        }

        ambientPulseTimer -= Time.deltaTime;

        if (ambientPulseTimer <= 0f && Mathf.Abs(targetDelta) < 0.02f)
        {
            pulseZoom = Mathf.Max(pulseZoom, ambientPulseAmount);
            ambientPulseTimer = ambientPulseInterval;
        }

        bool isMovingFast = Mathf.Abs(targetDelta) / Mathf.Max(Time.deltaTime, 0.0001f) > 5f;
        bool isAirborne = playerMovement != null && !playerMovement.IsGroundedForCamera;
        float targetFieldOfView = idleFieldOfView;

        if (isAirborne)
        {
            targetFieldOfView = jumpFieldOfView;
        }
        else if (isMovingFast)
        {
            targetFieldOfView = runFieldOfView;
        }

        pulseZoom = Mathf.MoveTowards(pulseZoom, 0f, pulseReturnSpeed * Time.deltaTime);
        targetFieldOfView -= pulseZoom;
        followCamera.fieldOfView = Mathf.SmoothDamp(followCamera.fieldOfView, targetFieldOfView, ref zoomVelocity, zoomSmoothTime);
    }
}
