using UnityEngine;

public class TargetIndicator : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float hideDistance = 4f;
    [SerializeField] private float offset = 2.2f;

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;

        if (dir.magnitude < hideDistance)
        {
            SetChildrenActive(false);
        }
        else
        {
            SetChildrenActive(true);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 dir = target.position - transform.position;
        HandleOffset(dir);
    }

    public void HandleOffset(Vector3 dir)
    {
        if (transform.parent == null)
        {
            return;
        }

        FollowTarget followTarget = transform.parent.GetComponent<FollowTarget>();
        if (followTarget == null || followTarget.GetTarget() == null)
        {
            return;
        }

        SizeHandler playerSize = followTarget.GetTarget().GetComponent<SizeHandler>();
        float playerScale = playerSize != null ? playerSize.GetSize() : 1f;

        transform.position = transform.parent.position + dir.normalized * offset * playerScale;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetChildrenActive(bool value)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(value);
        }
    }
}
