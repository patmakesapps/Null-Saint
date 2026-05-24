using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public enum SideMovementAxis
    {
        WorldX,
        WorldZ
    }

    [Header("Movement")]
    public SideMovementAxis movementAxis = SideMovementAxis.WorldZ;
    public float walkSpeed = 4f;
    public float runSpeed = 7.5f;
    public float acceleration = 45f;
    public float deceleration = 60f;
    public float airControl = 1f;
    public bool lockToStartingDepth = false;
    public float depthSpeed = 2.5f;
    public float depthMin = -3f;
    public float depthMax = 3f;
    public float depthCorrectionSpeed = 20f;
    public float facingTurnSpeed = 900f;
    public float faceRightYRotation = 0f;
    public float faceLeftYRotation = 180f;
    public float jumpHeight = 2.2f;
    public float doubleJumpHeight = 2.6f;
    public int maxJumps = 2;
    public float gravity = -32f;
    public float groundedGravity = -4f;
    public float groundProbeDistance = 0.75f;
    public float groundProbeRadiusPadding = 0.08f;
    public float groundStickForce = -18f;
    public bool snapToGround = true;
    public float groundSnapSpeed = 35f;
    public LayerMask groundLayers = ~0;

    [Header("Actions")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode slashSpinKey = KeyCode.Q;
    public KeyCode powerKey = KeyCode.E;
    public KeyCode blockKey = KeyCode.LeftControl;
    public KeyCode crawlKey = KeyCode.C;
    public float startupActionInputLock = 0.35f;

    [Header("Camera")]
    public bool autoConfigureMainCamera = true;
    public bool preserveCurrentCameraPosition = true;
    public Vector3 sideViewCameraOffset = new Vector3(12f, 2.2f, 0f);
    public Vector3 sideViewCameraEuler = new Vector3(0f, -90f, 0f);

    [Header("References")]
    public Animator animator;
    public Transform visualRoot;
    public bool alignVisualRootToControllerFeet = true;
    public float visualGroundOffset = 0.02f;

    [Header("Feedback")]
    public AudioSource audioSource;
    public GameplayFeedback walkFeedback;
    public GameplayFeedback runFeedback;
    public GameplayFeedback jumpFeedback;
    public GameplayFeedback slashFeedback;
    public GameplayFeedback slashSpinFeedback;
    public GameplayFeedback powerFeedback;
    public GameplayFeedback blockStartFeedback;
    public float walkStepInterval = 0.45f;
    public float runStepInterval = 0.28f;
    public float actionAnimationTimeout = 1.15f;

    private CharacterController controller;
    private float verticalVelocity;
    private float lastGroundedY;
    private int jumpsRemaining;
    private float horizontalVelocity;
    private float lockedDepth;
    private int facingDirection = 1;
    private SideViewCameraFollow cameraFollow;
    private bool groundedForCamera = true;
    private Collider[] ownColliders;
    private int visualAlignmentFramesRemaining;
    private float nextStepFeedbackTime;
    private bool wasBlocking;
    private float actionAnimationEndTime;
    private float actionInputUnlockTime;

    public bool IsGroundedForCamera => groundedForCamera;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        ownColliders = GetComponentsInChildren<Collider>(true);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }
        }

        if (visualRoot == null && animator != null && animator.transform != transform)
        {
            visualRoot = animator.transform;
        }

        airControl = Mathf.Clamp(airControl, 0.1f, 1f);

        lastGroundedY = transform.position.y;
        lockedDepth = GetCurrentDepth();
        jumpsRemaining = maxJumps;
        actionInputUnlockTime = Time.time + startupActionInputLock;

        ConfigureMainCamera();
    }

    void Start()
    {
        if (alignVisualRootToControllerFeet)
        {
            visualAlignmentFramesRemaining = 3;
            AlignVisualRootToControllerFeet();
        }
    }

    void Update()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        float depthInput = Input.GetAxisRaw("Vertical");

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float moveSpeed = isRunning ? runSpeed : walkSpeed;
        bool jumpPressed = Input.GetKeyDown(jumpKey);
        bool didJump = false;
        bool blocking = Input.GetKey(blockKey) || Input.GetMouseButton(1);

        UpdateFacing(moveInput);

        bool grounded = controller.isGrounded;
        groundedForCamera = grounded;

        if (grounded)
        {
            jumpsRemaining = maxJumps;
            lastGroundedY = transform.position.y;

            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundStickForce;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        if (jumpPressed && jumpsRemaining > 0)
        {
            float currentJumpHeight = jumpsRemaining == maxJumps ? jumpHeight : doubleJumpHeight;
            verticalVelocity = Mathf.Sqrt(currentJumpHeight * -2f * gravity);
            jumpsRemaining--;
            didJump = true;
            cameraFollow?.PulseZoom();
            PlayFeedback(jumpFeedback);
        }

        float controlMultiplier = grounded ? 1f : airControl;
        float targetHorizontalVelocity = moveInput * moveSpeed * controlMultiplier;
        float velocityChangeRate = Mathf.Abs(moveInput) > 0.01f ? acceleration : deceleration;
        horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, targetHorizontalVelocity, velocityChangeRate * Time.deltaTime);

        Vector3 velocity = GetMoveAxis() * horizontalVelocity;

        if (lockToStartingDepth)
        {
            velocity += GetDepthAxis() * ((lockedDepth - GetCurrentDepth()) * depthCorrectionSpeed);
        }
        else if (Mathf.Abs(depthInput) > 0.01f)
        {
            float currentDepth = GetCurrentDepth();
            float projectedDepth = currentDepth + depthInput * depthSpeed * Time.deltaTime;
            float clampedDepth = Mathf.Clamp(projectedDepth, lockedDepth + depthMin, lockedDepth + depthMax);
            float depthDelta = clampedDepth - currentDepth;
            velocity += GetDepthAxis() * (depthDelta / Mathf.Max(Time.deltaTime, 0.0001f));
        }

        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        RaycastHit groundHit;

        if (snapToGround && verticalVelocity <= 0f && ProbeGround(out groundHit, groundProbeDistance))
        {
            float snapDistance = Mathf.Max(0f, groundHit.distance);

            if (snapDistance > 0.001f)
            {
                controller.Move(Vector3.down * Mathf.Min(snapDistance, groundSnapSpeed * Time.deltaTime));
            }

            verticalVelocity = groundedGravity;
        }

        groundedForCamera = controller.isGrounded || IsGroundClose();

        float animSpeed = Mathf.Abs(horizontalVelocity) / Mathf.Max(walkSpeed, 0.001f);
        UpdateStepFeedback(animSpeed, isRunning);
        UpdateBlockFeedback(blocking);

        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed, 0.08f, Time.deltaTime);
            animator.SetBool("Block", blocking);
            animator.SetBool("Crawl", Input.GetKey(crawlKey));
            animator.SetBool("Grounded", groundedForCamera);
            animator.SetFloat("VerticalVelocity", verticalVelocity);

            if (didJump)
            {
                animator.ResetTrigger("Jump");
                animator.SetTrigger("Jump");
                MarkActionAnimationStarted();
            }

            if (groundedForCamera)
            {
                animator.ResetTrigger("Jump");
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (!AreActionInputsUnlocked())
                {
                    return;
                }

                animator.ResetTrigger("Slash");
                animator.SetTrigger("Slash");
                MarkActionAnimationStarted();
                cameraFollow?.PulseZoom();
                PlayFeedback(slashFeedback);
            }

            if (Input.GetKeyDown(slashSpinKey))
            {
                if (!AreActionInputsUnlocked())
                {
                    return;
                }

                animator.ResetTrigger("SlashSpin");
                animator.SetTrigger("SlashSpin");
                MarkActionAnimationStarted();
                cameraFollow?.PulseZoom();
                PlayFeedback(slashSpinFeedback);
            }

            if (Input.GetKeyDown(powerKey))
            {
                if (!AreActionInputsUnlocked())
                {
                    return;
                }

                animator.ResetTrigger("Power");
                animator.SetTrigger("Power");
                MarkActionAnimationStarted();
                cameraFollow?.PulseZoom();
                PlayFeedback(powerFeedback);
            }

            RecoverFromStuckActionState(animSpeed);
        }
    }

    void LateUpdate()
    {
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
        }

        if (visualAlignmentFramesRemaining > 0)
        {
            AlignVisualRootToControllerFeet();
            visualAlignmentFramesRemaining--;
        }
    }

    private void UpdateFacing(float moveInput)
    {
        if (moveInput > 0.01f)
        {
            facingDirection = 1;
        }
        else if (moveInput < -0.01f)
        {
            facingDirection = -1;
        }

        float targetYRotation = facingDirection > 0 ? faceRightYRotation : faceLeftYRotation;
        Quaternion targetRotation = Quaternion.Euler(0f, targetYRotation, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, facingTurnSpeed * Time.deltaTime);
    }

    private Vector3 GetMoveAxis()
    {
        return movementAxis == SideMovementAxis.WorldZ ? Vector3.forward : Vector3.right;
    }

    private Vector3 GetDepthAxis()
    {
        return movementAxis == SideMovementAxis.WorldZ ? Vector3.right : Vector3.forward;
    }

    private float GetCurrentDepth()
    {
        return movementAxis == SideMovementAxis.WorldZ ? transform.position.x : transform.position.z;
    }

    private void ConfigureMainCamera()
    {
        if (!autoConfigureMainCamera || Camera.main == null)
        {
            return;
        }

        Transform cameraTransform = Camera.main.transform;
        cameraTransform.SetParent(null, true);

        if (!preserveCurrentCameraPosition)
        {
            cameraTransform.rotation = Quaternion.Euler(sideViewCameraEuler);
        }

        SideViewCameraFollow follow = Camera.main.GetComponent<SideViewCameraFollow>();

        if (follow == null)
        {
            follow = Camera.main.gameObject.AddComponent<SideViewCameraFollow>();
        }

        cameraFollow = follow;
        follow.target = transform;
        follow.horizontalAxis = movementAxis == SideMovementAxis.WorldZ
            ? SideViewCameraFollow.FollowAxis.WorldZ
            : SideViewCameraFollow.FollowAxis.WorldX;
        follow.preserveStartingPosition = preserveCurrentCameraPosition;

        if (!preserveCurrentCameraPosition)
        {
            follow.offset = sideViewCameraOffset;
        }
    }

    private bool IsGroundClose()
    {
        return ProbeGround(out _, groundProbeDistance);
    }

    private void UpdateStepFeedback(float animSpeed, bool isRunning)
    {
        if (!groundedForCamera || animSpeed <= 0.05f)
        {
            return;
        }

        if (Time.time < nextStepFeedbackTime)
        {
            return;
        }

        if (isRunning)
        {
            PlayFeedback(runFeedback);
            nextStepFeedbackTime = Time.time + runStepInterval;
        }
        else
        {
            PlayFeedback(walkFeedback);
            nextStepFeedbackTime = Time.time + walkStepInterval;
        }
    }

    private void UpdateBlockFeedback(bool blocking)
    {
        if (blocking && !wasBlocking)
        {
            PlayFeedback(blockStartFeedback);
        }

        wasBlocking = blocking;
    }

    private void MarkActionAnimationStarted()
    {
        actionAnimationEndTime = Time.time + actionAnimationTimeout;
    }

    private bool AreActionInputsUnlocked()
    {
        return Time.time >= actionInputUnlockTime;
    }

    private void RecoverFromStuckActionState(float animSpeed)
    {
        if (actionAnimationEndTime <= 0f || Time.time < actionAnimationEndTime || animator.IsInTransition(0))
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (!IsActionState(stateInfo))
        {
            actionAnimationEndTime = 0f;
            return;
        }

        string locomotionState = GetLocomotionStateName(animSpeed);
        animator.CrossFadeInFixedTime(locomotionState, 0.08f);
        actionAnimationEndTime = 0f;
    }

    private bool IsActionState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Slash_01")
            || stateInfo.IsName("Slash_spin")
            || stateInfo.IsName("Power")
            || stateInfo.IsName("Jump");
    }

    private string GetLocomotionStateName(float animSpeed)
    {
        if (animSpeed > 1.1f)
        {
            return "Run";
        }

        if (animSpeed > 0.1f)
        {
            return "Walk";
        }

        return "Idle";
    }

    private void PlayFeedback(GameplayFeedback feedback)
    {
        if (feedback == null)
        {
            return;
        }

        feedback.Play(this, audioSource, transform.position);
    }

    private void AlignVisualRootToControllerFeet()
    {
        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        float controllerBottomY = GetControllerBottomY();
        float targetVisualBottomY = controllerBottomY + visualGroundOffset;

        if (!TryGetVisualFootY(out float visualBottomY))
        {
            if (!TryGetVisualBounds(out Bounds visualBounds))
            {
                return;
            }

            visualBottomY = visualBounds.min.y;
        }

        float yDelta = targetVisualBottomY - visualBottomY;

        if (Mathf.Abs(yDelta) <= 0.001f)
        {
            return;
        }

        visualRoot.position += Vector3.up * yDelta;
    }

    private bool TryGetVisualFootY(out float footY)
    {
        footY = float.PositiveInfinity;

        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        bool foundFoot = TryIncludeBoneY(HumanBodyBones.LeftFoot, ref footY);
        foundFoot |= TryIncludeBoneY(HumanBodyBones.RightFoot, ref footY);
        foundFoot |= TryIncludeBoneY(HumanBodyBones.LeftToes, ref footY);
        foundFoot |= TryIncludeBoneY(HumanBodyBones.RightToes, ref footY);
        return foundFoot;
    }

    private bool TryIncludeBoneY(HumanBodyBones bone, ref float minY)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);

        if (boneTransform == null)
        {
            return false;
        }

        minY = Mathf.Min(minY, boneTransform.position.y);
        return true;
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default(Bounds);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer visualRenderer = renderers[i];

            if (visualRenderer == null || !visualRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = visualRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(visualRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private float GetControllerBottomY()
    {
        float radius = Mathf.Max(0f, controller.radius);
        float halfHeight = Mathf.Max(controller.height * 0.5f, radius);
        return transform.position.y + controller.center.y - halfHeight;
    }

    private bool ProbeGround(out RaycastHit hit, float probeDistance)
    {
        float radius = Mathf.Max(0.05f, controller.radius - groundProbeRadiusPadding);
        float halfHeight = Mathf.Max(controller.height * 0.5f, radius);
        Vector3 bottomSphereCenter = transform.position + controller.center + Vector3.down * (halfHeight - radius);
        Vector3 rayStart = transform.position + Vector3.up * 0.25f;
        Vector3 sphereStart = bottomSphereCenter + Vector3.up * GroundProbeSkin;
        float sphereDistance = probeDistance + GroundProbeSkin;

        RaycastHit[] sphereHits = Physics.SphereCastAll(sphereStart, radius, Vector3.down, sphereDistance, groundLayers, QueryTriggerInteraction.Ignore);

        if (TrySelectGroundHit(sphereHits, out hit))
        {
            return true;
        }

        RaycastHit[] rayHits = Physics.RaycastAll(rayStart, Vector3.down, probeDistance + 0.25f, groundLayers, QueryTriggerInteraction.Ignore);
        return TrySelectGroundHit(rayHits, out hit);
    }

    private bool TryFindGroundBelow(Vector3 origin, float distance, out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, groundLayers, QueryTriggerInteraction.Ignore);
        return TrySelectGroundHit(hits, out groundHit);
    }

    private bool TrySelectGroundHit(RaycastHit[] hits, out RaycastHit groundHit)
    {
        float nearestDistance = float.PositiveInfinity;
        groundHit = default(RaycastHit);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit + 2f)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                groundHit = hit;
            }
        }

        return nearestDistance < float.PositiveInfinity;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        if (ownColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == hitCollider)
            {
                return true;
            }
        }

        return false;
    }

    private const float GroundProbeSkin = 0.05f;
}
