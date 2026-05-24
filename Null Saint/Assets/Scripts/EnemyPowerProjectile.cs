using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyPowerProjectile : MonoBehaviour
{
    public GameplayFeedback impactFeedback;
    public GameplayFeedback blockedFeedback;
    public GameplayFeedback slashedFeedback;

    private Vector3 direction;
    private float speed;
    private float deathTime;
    private bool initialized;

    public void Initialize(Vector3 travelDirection, float travelSpeed, float lifetime)
    {
        direction = travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector3.forward;
        speed = travelSpeed;
        deathTime = Time.time + lifetime;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        transform.position += direction * speed * Time.deltaTime;

        if (Time.time >= deathTime)
        {
            DestroyProjectileSilently();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        PlayerCombat player = other.GetComponentInParent<PlayerCombat>();

        if (player != null)
        {
            if (player.IsBlocking)
            {
                blockedFeedback?.PlayAtPosition(this, transform.position);
            }
            else
            {
                impactFeedback?.PlayAtPosition(this, transform.position);
                player.Kill();
            }

            DestroyProjectileSilently();
            return;
        }

        if (other.GetComponentInParent<EnemyGhostCombat>() != null)
        {
            return;
        }

        impactFeedback?.PlayAtPosition(this, transform.position);
        DestroyProjectileSilently();
    }

    public void DestroyBySlash()
    {
        slashedFeedback?.PlayAtPosition(this, transform.position);
        DestroyProjectileSilently();
    }

    public void DestroyProjectileSilently()
    {
        Destroy(gameObject);
    }
}
