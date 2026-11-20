using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private SceneFader sceneFader;

    public void LoadScene()
    {
        if (sceneFader != null)
        {
            sceneFader.FadeOutThen(Load);
        }
        else
        {
            Load();
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    private void Load()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
