using UnityEngine;

public class PlaySoundEnter : StateMachineBehaviour
{
    [SerializeField] private string soundName;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }
}
