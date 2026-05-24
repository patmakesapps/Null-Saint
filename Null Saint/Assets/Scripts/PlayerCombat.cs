using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    public KeyCode slashSpinKey = KeyCode.Q;
    public float slashRange = 4f;
    public float slashDepth = 1.4f;
    public float slashVerticalRadius = 1.25f;
    public float slashHeight = 1f;
    public float slashCooldown = 0.25f;
    public int slashDamage = 1;
    public bool drawSlashDebug;
    public float startupAttackInputLock = 0.35f;

    [Header("Defense")]
    public KeyCode blockKey = KeyCode.LeftControl;
    public bool rightMouseBlocks = true;

    [Header("Death")]
    public float fallDeathSeconds = 3f;
    public float fallingVelocityThreshold = -0.1f;
    public bool reloadSceneOnDeath = true;
    public float reloadDelay = 1.5f;
    public bool stopEnemiesOnDeath = true;
    public bool clearProjectilesOnDeath = true;
    public bool hidePlayerOnDeath;
    public string deathTriggerName = "Die";

    [Header("Feedback")]
    public AudioSource audioSource;
    public GameplayFeedback slashHitFeedback;
    public GameplayFeedback deathFeedback;

    private PlayerMovement movement;
    private CharacterController controller;
    private Animator animator;
    private float nextSlashTime;
    private float fallingTimer;
    private bool dead;
    private float reloadTime;
    private float attackInputUnlockTime;

    public bool IsBlocking => !dead && (Input.GetKey(blockKey) || (rightMouseBlocks && Input.GetMouseButton(1)));
    public bool IsDead => dead;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        blockKey = movement.blockKey;
        slashSpinKey = movement.slashSpinKey;
        attackInputUnlockTime = Time.time + startupAttackInputLock;
    }

    private void Update()
    {
        if (dead)
        {
            UpdateDeath();
            return;
        }

        if (Time.time >= attackInputUnlockTime && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(slashSpinKey)))
        {
            TrySlash();
        }

        UpdateFallDeath();
    }

    public void Kill()
    {
        if (dead)
        {
            return;
        }

        dead = true;
        deathFeedback?.Play(this, audioSource, transform.position);
        TriggerDeathAnimation();
        StopCombatThreats();
        movement.enabled = false;

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (hidePlayerOnDeath)
        {
            SetRenderersEnabled(false);
        }

        if (reloadSceneOnDeath)
        {
            reloadTime = Time.unscaledTime + reloadDelay;
        }
    }

    private void UpdateDeath()
    {
        if (!reloadSceneOnDeath || reloadTime <= 0f || Time.unscaledTime < reloadTime)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (!string.IsNullOrEmpty(activeScene.path))
        {
            SceneManager.LoadScene(activeScene.path);
        }
        else
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    private void TrySlash()
    {
        if (Time.time < nextSlashTime)
        {
            return;
        }

        nextSlashTime = Time.time + slashCooldown;
        Vector3 attackDirection = GetAttackDirection();
        Vector3 attackCenter = GetSlashCenter(attackDirection);
        Vector3 halfExtents = GetSlashHalfExtents(attackDirection);
        Quaternion attackRotation = Quaternion.identity;
        Collider[] hits = Physics.OverlapBox(attackCenter, halfExtents, attackRotation, ~0, QueryTriggerInteraction.Collide);
        HashSet<EnemyGhostCombat> damagedEnemies = new HashSet<EnemyGhostCombat>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];

            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            EnemyGhostCombat enemy = hit.GetComponentInParent<EnemyGhostCombat>();

            if (enemy != null)
            {
                if (damagedEnemies.Contains(enemy))
                {
                    continue;
                }

                damagedEnemies.Add(enemy);
                slashHitFeedback?.Play(this, audioSource, hit.bounds.center);
                enemy.TakeDamage(slashDamage);
                continue;
            }

            EnemyPowerProjectile projectile = hit.GetComponentInParent<EnemyPowerProjectile>();

            if (projectile != null)
            {
                projectile.DestroyBySlash();
            }
        }

        EnemyGhostCombat[] enemies = FindObjectsByType<EnemyGhostCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGhostCombat enemy = enemies[i];

            if (enemy == null || damagedEnemies.Contains(enemy))
            {
                continue;
            }

            Vector3 enemyPoint = enemy.GetHitTargetPoint();

            if (!IsPointInsideSlash(enemyPoint, attackCenter, halfExtents))
            {
                continue;
            }

            damagedEnemies.Add(enemy);
            slashHitFeedback?.Play(this, audioSource, enemyPoint);
            enemy.TakeDamage(slashDamage);
        }
    }

    private void UpdateFallDeath()
    {
        bool falling = !movement.IsGroundedForCamera && controller != null && controller.velocity.y < fallingVelocityThreshold;

        if (falling)
        {
            fallingTimer += Time.deltaTime;

            if (fallingTimer >= fallDeathSeconds)
            {
                Kill();
            }
        }
        else
        {
            fallingTimer = 0f;
        }
    }

    private void TriggerDeathAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(deathTriggerName))
        {
            return;
        }

        animator.ResetTrigger(deathTriggerName);
        animator.SetTrigger(deathTriggerName);
    }

    private void StopCombatThreats()
    {
        if (stopEnemiesOnDeath)
        {
            EnemyGhostCombat[] enemies = FindObjectsByType<EnemyGhostCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].enabled = false;
            }
        }

        if (clearProjectilesOnDeath)
        {
            EnemyPowerProjectile[] projectiles = FindObjectsByType<EnemyPowerProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < projectiles.Length; i++)
            {
                projectiles[i].DestroyProjectileSilently();
            }
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }
    }

    private Vector3 GetAttackDirection()
    {
        Vector3 attackDirection = transform.forward;
        attackDirection.y = 0f;

        if (attackDirection.sqrMagnitude < 0.001f)
        {
            attackDirection = Vector3.forward;
        }

        return attackDirection.normalized;
    }

    private Vector3 GetSlashCenter(Vector3 attackDirection)
    {
        return transform.position + Vector3.up * slashHeight + attackDirection * slashRange * 0.5f;
    }

    private Vector3 GetSlashHalfExtents(Vector3 attackDirection)
    {
        bool attacksMostlyZ = Mathf.Abs(attackDirection.z) >= Mathf.Abs(attackDirection.x);
        float forwardHalfExtent = slashRange * 0.5f;

        return attacksMostlyZ
            ? new Vector3(slashDepth, slashVerticalRadius, forwardHalfExtent)
            : new Vector3(forwardHalfExtent, slashVerticalRadius, slashDepth);
    }

    private bool IsPointInsideSlash(Vector3 point, Vector3 attackCenter, Vector3 halfExtents)
    {
        Vector3 localPoint = point - attackCenter;
        return Mathf.Abs(localPoint.x) <= halfExtents.x
            && Mathf.Abs(localPoint.y) <= halfExtents.y
            && Mathf.Abs(localPoint.z) <= halfExtents.z;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSlashDebug)
        {
            return;
        }

        Vector3 attackDirection = GetAttackDirection();
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GetSlashCenter(attackDirection), GetSlashHalfExtents(attackDirection) * 2f);
    }
}
