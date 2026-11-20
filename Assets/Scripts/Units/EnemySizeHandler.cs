using UnityEngine;

public class EnemySizeHandler : UnitSizeHandler
{
    [Header("Feedback Settings")]
    [SerializeField] protected Animator anim;

    private bool isDying;

    private void Start()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    protected override void Consume(GameObject unit)
    {
        if (isSmaller)
        {
            return;
        }

        if (unit.CompareTag("Player"))
        {
            if (isDying)
            {
                return;
            }

            isDying = true;

            if (anim != null)
            {
                anim.SetTrigger("Bite");
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.Play("Death");
            }

            Animator playerAnim = unit.GetComponent<Animator>();
            if (playerAnim != null)
            {
                playerAnim.SetTrigger("Die");
            }

            PlayerController playerController = unit.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowGameOver(0.9f);
            }

            return;
        }

        satiationValue += 25f;

        if (anim != null)
        {
            anim.SetTrigger("Bite");
        }

        base.Consume(unit);
    }

    private void OnDestroy()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.CleanEnemyArray(this);
        }
    }
}
