using UnityEngine;

public class PlayerSizeHandler : UnitSizeHandler
{
    [Header("Growth Settings")]
    [SerializeField] private float maximumSize = 2.5f;

    [Header("Feedback Settings")]
    [SerializeField] protected Animator anim;

    private void Start()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    protected override void HandleSize()
    {
        if (satiationValue >= 100f)
        {
            satiationValue = 0f;

            if (size < maximumSize)
            {
                size += .25f;

                if (anim != null)
                {
                    anim.SetTrigger("Grow");
                }
            }
        }
    }

    protected override void MoveTowardsCenter(GameObject target)
    {
        if (target.CompareTag("Enemy"))
        {
            base.MoveTowardsCenter(target);
        }
    }

    protected override void Consume(GameObject unit)
    {
        if (!isSmaller)
        {
            satiationValue += 25f;

            if (anim != null)
            {
                anim.SetTrigger("Bite");
            }

            Destroy(unit);
        }
    }
}
