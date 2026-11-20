using UnityEngine;

public class SpawnParticleEvent : MonoBehaviour
{
    public float duration = 1.2f;
    public GameObject particlePrefab;
    public Transform spawnPoint;

    public void SpawnParticle()
    {
        if (particlePrefab == null)
        {
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        GameObject particle = Instantiate(particlePrefab, point.position, Quaternion.identity);
        Destroy(particle, duration);
    }
}
