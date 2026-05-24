using UnityEngine;

public class EnemyGhostCombat : MonoBehaviour
{
    [Header("Health")]
    public int health = 1;
    public bool destroyOnDeath = true;

    [Header("Shooting")]
    public float firstShotDelay = 1.25f;
    public float shotInterval = 2.5f;
    public float shotRange = 25f;
    public float projectileSpeed = 8f;
    public float projectileLifetime = 6f;
    public float projectileRadius = 0.22f;
    public Vector3 projectileSpawnOffset = new Vector3(0f, 0.4f, 0f);
    public Color projectileColor = new Color(0.75f, 0.2f, 1f, 1f);

    [Header("Feedback")]
    public AudioSource audioSource;
    public GameplayFeedback shootFeedback;
    public GameplayFeedback deathFeedback;
    public GameplayFeedback projectileImpactFeedback;
    public GameplayFeedback projectileBlockedFeedback;
    public GameplayFeedback projectileSlashedFeedback;

    private Transform player;
    private float nextShotTime;
    private bool dead;

    private void Awake()
    {
        EnsurePhysicsSetup();
        EnsureAudioSource();
    }

    private void Start()
    {
        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        player = playerMovement != null ? playerMovement.transform : null;
        nextShotTime = Time.time + firstShotDelay + Random.Range(0f, 0.5f);
    }

    private void Update()
    {
        if (dead || player == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= shotRange && Time.time >= nextShotTime)
        {
            ShootAtPlayer();
            nextShotTime = Time.time + shotInterval;
        }
    }

    public void TakeDamage(int damage)
    {
        if (dead)
        {
            return;
        }

        health -= Mathf.Max(1, damage);

        if (health <= 0)
        {
            Die();
        }
    }

    public Vector3 GetHitTargetPoint()
    {
        Collider enemyCollider = GetComponent<Collider>();

        if (enemyCollider != null)
        {
            return enemyCollider.bounds.center;
        }

        Renderer enemyRenderer = GetComponentInChildren<Renderer>();

        if (enemyRenderer != null)
        {
            return enemyRenderer.bounds.center;
        }

        return transform.position;
    }

    private void Die()
    {
        dead = true;
        deathFeedback?.PlayAtPosition(this, transform.position);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ShootAtPlayer()
    {
        Vector3 spawnPosition = transform.position + projectileSpawnOffset;
        Vector3 direction = player.position + Vector3.up * 0.8f - spawnPosition;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "EnemyPowerProjectile";
        projectileObject.transform.position = spawnPosition;
        projectileObject.transform.localScale = Vector3.one * projectileRadius * 2f;

        Renderer projectileRenderer = projectileObject.GetComponent<Renderer>();

        if (projectileRenderer != null)
        {
            projectileRenderer.material.color = projectileColor;
        }

        Collider projectileCollider = projectileObject.GetComponent<Collider>();

        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.useGravity = false;
        projectileBody.isKinematic = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        EnemyPowerProjectile projectile = projectileObject.AddComponent<EnemyPowerProjectile>();
        projectile.Initialize(direction.normalized, projectileSpeed, projectileLifetime);
        projectile.impactFeedback = projectileImpactFeedback;
        projectile.blockedFeedback = projectileBlockedFeedback;
        projectile.slashedFeedback = projectileSlashedFeedback;

        shootFeedback?.Play(this, audioSource, spawnPosition);
    }

    private void EnsurePhysicsSetup()
    {
        Collider[] colliders = GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = true;
        }

        if (colliders.Length == 0)
        {
            BoxCollider enemyCollider = gameObject.AddComponent<BoxCollider>();
            enemyCollider.isTrigger = true;
            enemyCollider.size = Vector3.one;
            enemyCollider.center = Vector3.zero;
        }

        Rigidbody enemyBody = GetComponent<Rigidbody>();

        if (enemyBody == null)
        {
            enemyBody = gameObject.AddComponent<Rigidbody>();
        }

        enemyBody.useGravity = false;
        enemyBody.isKinematic = true;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }
}
