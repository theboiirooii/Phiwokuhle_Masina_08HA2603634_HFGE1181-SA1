using UnityEngine;

public class PlaySoundOnStart : MonoBehaviour
{
    [SerializeField] private string soundName = "Music";
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        if (delay > 0f)
        {
            Invoke(nameof(PlaySound), delay);
        }
        else
        {
            PlaySound();
        }
    }

    private void PlaySound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }
}
