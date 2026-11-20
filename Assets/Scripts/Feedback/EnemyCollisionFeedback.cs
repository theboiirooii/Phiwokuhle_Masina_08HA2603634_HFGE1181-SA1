using UnityEngine;

public class EnemyCollisionFeedback : MonoBehaviour
{
    [SerializeField] private string soundName = "Squelch";
    [SerializeField] private SpawnParticleEvent particleEvent;

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!col.CompareTag("Enemy"))
        {
            return;
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }

        if (particleEvent != null)
        {
            particleEvent.SpawnParticle();
        }
    }
}
