using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    public void Configure(Button play, Button quit)
    {
        playButton = play;
        quitButton = quit;
    }

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(PlayGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    private void PlayGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.Gameplay);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
