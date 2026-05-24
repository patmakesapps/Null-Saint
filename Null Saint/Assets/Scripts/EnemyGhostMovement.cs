using System.Collections.Generic;
using UnityEngine;

public class EnemyGhostMovement : MonoBehaviour
{
    private static readonly List<EnemyGhostMovement> ActiveEnemies = new List<EnemyGhostMovement>();

    [Header("Awareness")]
    public float awarenessRange = 14f;
    public float disengageRange = 18f;
    public float preferredFollowDistance = 6f;
    public float followDeadZone = 0.75f;
    public float verticalFollowOffset = 0.5f;
    public bool requireLineOfSight;
    public LayerMask lineOfSightBlockingLayers = ~0;

    [Header("Patrol")]
    public bool matchPlayerMovementAxis = true;
    public PlayerMovement.SideMovementAxis movementAxis = PlayerMovement.SideMovementAxis.WorldZ;
    public float horizontalPatrolDistance = 2.5f;
    public float verticalPatrolDistance = 1.1f;
    public float patrolSpeed = 0.8f;
    public float returnSpeed = 1.6f;

    [Header("Follow")]
    public float followSpeed = 2.4f;
    public float acceleration = 7f;
    public float homeLeashDistance = 12f;
    public float verticalLeashDistance = 4f;
    public bool followPlayerDepth;
    public float maxDepthOffset = 2f;

    [Header("Spacing")]
    public float separationRadius = 1.25f;
    public float separationStrength = 0.75f;

    [Header("Facing")]
    public bool facePlayer = true;
    public float facingTurnSpeed = 360f;
    public float faceForwardYRotation = 0f;
    public float faceBackwardYRotation = 180f;

    private Transform player;
    private EnemyGhostCombat combat;
    private Vector3 homePosition;
    private Vector3 velocity;
    private float patrolPhase;
    private bool awareOfPlayer;

    public bool IsAwareOfPlayer => awareOfPlayer;

    private void Awake()
    {
        combat = GetComponent<EnemyGhostCombat>();
        homePosition = transform.position;
        patrolPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (combat != null && combat.IsDead)
        {
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        if (player != null && matchPlayerMovementAxis)
        {
            PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();

            if (playerMovement != null)
            {
                movementAxis = playerMovement.movementAxis;
            }
        }

        Vector3 targetPosition = GetPatrolTarget();
        float targetSpeed = patrolSpeed;

        UpdateAwareness();

        if (awareOfPlayer && player != null)
        {
            targetPosition = GetFollowTarget();
            targetSpeed = followSpeed;
        }
        else if (Vector3.Distance(transform.position, homePosition) > horizontalPatrolDistance + 0.5f)
        {
            targetSpeed = returnSpeed;
        }

        targetPosition += GetSeparationOffset();
        targetPosition = ConstrainTarget(targetPosition);

        MoveToward(targetPosition, targetSpeed);
        UpdateFacing();
    }

    private void FindPlayer()
    {
        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        player = playerMovement != null ? playerMovement.transform : null;
    }

    private void UpdateAwareness()
    {
        if (player == null)
        {
            awareOfPlayer = false;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float activeRange = awareOfPlayer ? disengageRange : awarenessRange;
        bool playerInRange = distanceToPlayer <= activeRange;

        if (playerInRange && requireLineOfSight)
        {
            Vector3 origin = GetLookPoint(transform);
            Vector3 target = GetLookPoint(player);
            Vector3 direction = target - origin;

            playerInRange = !Physics.Raycast(origin, direction.normalized, direction.magnitude, lineOfSightBlockingLayers, QueryTriggerInteraction.Ignore);
        }

        awareOfPlayer = playerInRange && IsWithinHomeLeash(player.position);
    }

    private Vector3 GetPatrolTarget()
    {
        Vector3 moveAxis = GetMoveAxis();
        float time = Time.time * Mathf.Max(0.01f, patrolSpeed);
        float horizontalOffset = Mathf.Sin(time + patrolPhase) * horizontalPatrolDistance;
        float verticalOffset = Mathf.Sin(time * 1.37f + patrolPhase * 0.7f) * verticalPatrolDistance;

        return homePosition + moveAxis * horizontalOffset + Vector3.up * verticalOffset;
    }

    private Vector3 GetFollowTarget()
    {
        Vector3 moveAxis = GetMoveAxis();
        Vector3 depthAxis = GetDepthAxis();
        float signedDistance = Vector3.Dot(player.position - transform.position, moveAxis);

        if (Mathf.Abs(signedDistance) <= followDeadZone)
        {
            signedDistance = Vector3.Dot(player.position - homePosition, moveAxis);
        }

        float side = Mathf.Abs(signedDistance) > 0.01f ? Mathf.Sign(signedDistance) : 1f;
        Vector3 target = player.position - moveAxis * side * preferredFollowDistance;
        target.y = player.position.y + verticalFollowOffset;

        if (!followPlayerDepth)
        {
            float depthDelta = Vector3.Dot(target - homePosition, depthAxis);
            target -= depthAxis * depthDelta;
        }

        return target;
    }

    private Vector3 ConstrainTarget(Vector3 target)
    {
        Vector3 moveAxis = GetMoveAxis();
        Vector3 depthAxis = GetDepthAxis();
        Vector3 offset = target - homePosition;

        float moveOffset = Mathf.Clamp(Vector3.Dot(offset, moveAxis), -homeLeashDistance, homeLeashDistance);
        float depthOffset = followPlayerDepth ? Mathf.Clamp(Vector3.Dot(offset, depthAxis), -maxDepthOffset, maxDepthOffset) : 0f;
        float verticalOffset = Mathf.Clamp(offset.y, -verticalLeashDistance, verticalLeashDistance);

        return homePosition + moveAxis * moveOffset + depthAxis * depthOffset + Vector3.up * verticalOffset;
    }

    private bool IsWithinHomeLeash(Vector3 position)
    {
        Vector3 moveAxis = GetMoveAxis();
        Vector3 offset = position - homePosition;
        return Mathf.Abs(Vector3.Dot(offset, moveAxis)) <= homeLeashDistance + preferredFollowDistance;
    }

    private Vector3 GetSeparationOffset()
    {
        if (separationRadius <= 0f || separationStrength <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 offset = Vector3.zero;

        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            EnemyGhostMovement other = ActiveEnemies[i];

            if (other == null || other == this || !other.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            float distance = away.magnitude;

            if (distance <= 0.001f || distance > separationRadius)
            {
                continue;
            }

            offset += away.normalized * ((separationRadius - distance) / separationRadius);
        }

        return offset * separationStrength;
    }

    private void MoveToward(Vector3 targetPosition, float speed)
    {
        Vector3 toTarget = targetPosition - transform.position;
        Vector3 desiredVelocity = toTarget.sqrMagnitude > 0.0025f ? toTarget.normalized * speed : Vector3.zero;

        velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
    }

    private void UpdateFacing()
    {
        if (!facePlayer && velocity.sqrMagnitude < 0.01f)
        {
            return;
        }

        Vector3 reference = facePlayer && player != null ? player.position - transform.position : velocity;
        float signedDirection = Vector3.Dot(reference, GetMoveAxis());

        if (Mathf.Abs(signedDirection) < 0.01f)
        {
            return;
        }

        float targetYRotation = signedDirection >= 0f ? faceForwardYRotation : faceBackwardYRotation;
        Quaternion targetRotation = Quaternion.Euler(0f, targetYRotation, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, facingTurnSpeed * Time.deltaTime);
    }

    private Vector3 GetLookPoint(Transform target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : target.position + Vector3.up * 0.8f;
    }

    private Vector3 GetMoveAxis()
    {
        return movementAxis == PlayerMovement.SideMovementAxis.WorldZ ? Vector3.forward : Vector3.right;
    }

    private Vector3 GetDepthAxis()
    {
        return movementAxis == PlayerMovement.SideMovementAxis.WorldZ ? Vector3.right : Vector3.forward;
    }
}
