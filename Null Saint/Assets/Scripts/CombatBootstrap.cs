using UnityEngine;

public static class CombatBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallCombat()
    {
        PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();

        if (playerMovement != null && playerMovement.GetComponent<PlayerCombat>() == null)
        {
            playerMovement.gameObject.AddComponent<PlayerCombat>();
        }

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (!candidate.name.ToLowerInvariant().Contains("enemy_ghost"))
            {
                continue;
            }

            if (candidate.GetComponentInParent<EnemyGhostCombat>() == null)
            {
                candidate.AddComponent<EnemyGhostCombat>();
            }

            if (candidate.GetComponentInParent<EnemyGhostMovement>() == null)
            {
                candidate.AddComponent<EnemyGhostMovement>();
            }
        }
    }
}
