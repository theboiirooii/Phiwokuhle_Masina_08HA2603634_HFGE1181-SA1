using UnityEngine;

public class SpawnParticleOnEnter : StateMachineBehaviour
{
    public float duration = 1.2f;
    public GameObject particlePrefab;
    public Transform spawnPoint;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (particlePrefab == null)
        {
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : animator.transform;
        GameObject particle = Instantiate(particlePrefab, point.position, Quaternion.identity);
        Destroy(particle, duration);
    }
}
