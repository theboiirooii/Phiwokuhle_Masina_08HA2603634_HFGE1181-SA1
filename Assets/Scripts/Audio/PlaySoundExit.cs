using UnityEngine;

public class PlaySoundExit : StateMachineBehaviour
{
    [SerializeField] private string soundName;

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }
}
