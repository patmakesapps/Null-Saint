using UnityEngine;

[System.Serializable]
public class GameplayFeedback
{
    public AudioClip audioClip;
    public GameObject prefab;
    public Transform spawnPoint;
    public Vector3 spawnOffset;
    public float volume = 1f;
    public bool parentPrefabToSpawnPoint;

    public void Play(MonoBehaviour owner, AudioSource audioSource, Vector3 fallbackPosition)
    {
        if (audioClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(audioClip, volume);
        }

        if (prefab == null)
        {
            return;
        }

        Transform targetSpawnPoint = spawnPoint != null ? spawnPoint : owner.transform;
        Vector3 spawnPosition = (spawnPoint != null ? spawnPoint.position : fallbackPosition) + spawnOffset;
        GameObject instance = Object.Instantiate(prefab, spawnPosition, targetSpawnPoint.rotation);

        if (parentPrefabToSpawnPoint)
        {
            instance.transform.SetParent(targetSpawnPoint, true);
        }
    }

    public void PlayAtPosition(MonoBehaviour owner, Vector3 position)
    {
        if (audioClip != null)
        {
            AudioSource.PlayClipAtPoint(audioClip, position, volume);
        }

        if (prefab == null)
        {
            return;
        }

        GameObject instance = Object.Instantiate(prefab, position + spawnOffset, owner.transform.rotation);

        if (parentPrefabToSpawnPoint && spawnPoint != null)
        {
            instance.transform.SetParent(spawnPoint, true);
        }
    }
}
