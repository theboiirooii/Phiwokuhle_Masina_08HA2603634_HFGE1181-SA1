using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("HUD")]
    [SerializeField] private GameObject hud;
    [SerializeField] private PlayerSizeHandler playerSizeHandler;
    [SerializeField] private TextMeshProUGUI tmpSize;
    [SerializeField] private TextMeshProUGUI tmpTimer;

    private float timer = 0f;

    [Header("Menus")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private TextMeshProUGUI tmpGameOverStats;
    [SerializeField] private SceneFader sceneFader;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool gameOverQueued;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        timer = 0f;
        Cursor.visible = true;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        if (gameOverMenu != null)
        {
            gameOverMenu.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameStateManager.Instance == null)
        {
            return;
        }

        GameState state = GameStateManager.Instance.GetGameState();

        if (hud != null)
        {
            hud.SetActive(state != GameState.GameOver);
        }

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(state == GameState.Paused);
        }

        if (gameOverMenu != null)
        {
            gameOverMenu.SetActive(state == GameState.GameOver);
        }

        Cursor.visible = state != GameState.Playing;

        if (state == GameState.Playing)
        {
            UpdateTimer();
            UpdateSize();
        }
    }

    public void UpdateTimer()
    {
        timer += Time.deltaTime;

        if (tmpTimer != null)
        {
            tmpTimer.text = TimeSpan.FromSeconds(timer).ToString(@"mm\:ss");
        }
    }

    public void UpdateSize()
    {
        if (tmpSize != null && playerSizeHandler != null)
        {
            tmpSize.text = "Size: " + playerSizeHandler.GetSize().ToString("0.00");
        }
    }

    public void Pause()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetGameState(GameState.Paused);
        }
    }

    public void Resume()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetGameState(GameState.Playing);
        }
    }

    public void Restart()
    {
        if (sceneFader != null)
        {
            sceneFader.FadeOutThen(ReloadCurrentScene);
        }
        else
        {
            ReloadCurrentScene();
        }
    }

    public void MainMenu()
    {
        if (sceneFader != null)
        {
            sceneFader.FadeOutThen(LoadMainMenu);
        }
        else
        {
            LoadMainMenu();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ShowGameOver(float delay)
    {
        if (gameOverQueued)
        {
            return;
        }

        gameOverQueued = true;
        StartCoroutine(GameOverRoutine(delay));
    }

    private IEnumerator GameOverRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Stop("Music");
        }

        if (tmpGameOverStats != null && playerSizeHandler != null)
        {
            tmpGameOverStats.text = "Survived " + TimeSpan.FromSeconds(timer).ToString(@"mm\:ss") +
                                    "\nFinal size: " + playerSizeHandler.GetSize().ToString("0.00");
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetGameState(GameState.GameOver);
        }
    }

    private void ReloadCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
