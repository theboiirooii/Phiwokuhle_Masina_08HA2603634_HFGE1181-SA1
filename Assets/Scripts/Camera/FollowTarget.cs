using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [SerializeField] private Transform target;

    private void FixedUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position;
    }

    public Transform GetTarget()
    {
        return target;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
