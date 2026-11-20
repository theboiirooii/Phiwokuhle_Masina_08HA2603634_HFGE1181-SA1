using UnityEngine;

public class PlaySoundEvent : MonoBehaviour
{
    public void PlaySound(string soundName)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(soundName);
        }
    }
}
