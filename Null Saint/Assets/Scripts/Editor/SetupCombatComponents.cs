using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupCombatComponents
{
    private const string EnemyPrefabPath = "Assets/Prefabs/enemy_ghost.prefab";

    [MenuItem("Null Saint/Setup Combat Components")]
    public static void Setup()
    {
        SetupPlayerInOpenScene();
        SetupEnemiesInOpenScene();
        SetupEnemyPrefab();

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("Combat components setup complete.");
    }

    private static void SetupPlayerInOpenScene()
    {
        PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>();

        if (playerMovement == null)
        {
            Debug.LogWarning("No PlayerMovement found in the open scene.");
            return;
        }

        PlayerCombat playerCombat = EnsureComponent<PlayerCombat>(playerMovement.gameObject);
        playerCombat.slashRange = Mathf.Max(playerCombat.slashRange, 4f);
        playerCombat.slashDepth = Mathf.Max(playerCombat.slashDepth, 1.4f);
        playerCombat.slashVerticalRadius = Mathf.Max(playerCombat.slashVerticalRadius, 1.25f);
        EditorUtility.SetDirty(playerMovement.gameObject);
    }

    private static void SetupEnemiesInOpenScene()
    {
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (!candidate.name.ToLowerInvariant().Contains("enemy_ghost"))
            {
                continue;
            }

            EnsureComponent<EnemyGhostCombat>(candidate);
            EnsureComponent<EnemyGhostMovement>(candidate);
            EditorUtility.SetDirty(candidate);
        }
    }

    private static void SetupEnemyPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);

        if (prefabRoot == null)
        {
            Debug.LogWarning($"Could not load enemy prefab at {EnemyPrefabPath}.");
            return;
        }

        EnsureComponent<EnemyGhostCombat>(prefabRoot);
        EnsureComponent<EnemyGhostMovement>(prefabRoot);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
