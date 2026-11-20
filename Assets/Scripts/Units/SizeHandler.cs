using UnityEngine;

public abstract class SizeHandler : MonoBehaviour
{
    [Header("Size Parameters")]
    [SerializeField] protected float size = 1f;
    [SerializeField] protected bool isSmaller = false;
    protected float satiationValue = 0f;

    protected virtual void Update()
    {
        transform.localScale = new Vector3(size, size, size);
    }

    public virtual float GetSize()
    {
        return size;
    }

    public virtual void SetSize(float newSize)
    {
        size = newSize;
    }

    protected virtual void MoveTowardsCenter(GameObject target)
    {
        SizeHandler targetSize = target.GetComponent<SizeHandler>();
        if (targetSize == null)
        {
            return;
        }

        float pullSpeed = (size + targetSize.GetSize()) * 1.5f;
        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, pullSpeed * Time.deltaTime);
    }

    protected virtual void OnTriggerEnter2D(Collider2D col)
    {
        SizeHandler other = col.gameObject.GetComponent<SizeHandler>();
        if (other == null)
        {
            return;
        }

        if (other.GetSize() > size)
        {
            isSmaller = true;
        }
    }

    protected virtual void OnTriggerStay2D(Collider2D col)
    {
        SizeHandler other = col.gameObject.GetComponent<SizeHandler>();
        if (other == null)
        {
            return;
        }

        if (other.GetSize() > size)
        {
            MoveTowardsCenter(col.gameObject);
        }
    }

    protected virtual void OnTriggerExit2D(Collider2D col)
    {
        isSmaller = false;
    }
}
