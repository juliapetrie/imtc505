using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Afterglow.SimpleTutorial
{
    [DisallowMultipleComponent]
    public sealed class SimpleTutorialFlow : MonoBehaviour
    {
        [SerializeField] private GameObject[] pages;
        [SerializeField] private int finalPageIndex = 8;
        [Tooltip("Exact scene name or full Assets/.../*.unity path from the active Build Profile. Leave blank until configured.")]
        [SerializeField] private string gameScene = "";
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button enterButton;
        private int currentIndex;
        private bool loading;
        private int lastNavigationFrame = -1;

        public int CurrentIndex => currentIndex;
        private void Awake()
        {
            if (pages == null || pages.Length == 0) { Debug.LogError("Afterglow: pages are not assigned.", this); return; }
            Show(0);
        }
        private bool CanNavigate()
        {
            if (loading || pages == null || pages.Length == 0 || lastNavigationFrame == Time.frameCount) return false;
            lastNavigationFrame = Time.frameCount;
            return true;
        }
        public void Next() { if (CanNavigate()) Show(currentIndex + 1); }
        public void Back() { if (CanNavigate()) Show(currentIndex - 1); }
        public void SkipTutorial() { if (CanNavigate()) Show(Mathf.Clamp(finalPageIndex, 0, pages.Length - 1)); }
        public void Show(int index)
        {
            if (pages == null || pages.Length == 0) return;
            index = Mathf.Clamp(index, 0, pages.Length - 1);
            if (pages[index] == null) { Debug.LogError("Afterglow: missing page at index " + index, this); return; }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == index);
            currentIndex = index;
            if (statusText != null) statusText.text = "";
        }
        public void EnterGame()
        {
            if (loading) return;
            string destination = (gameScene ?? "").Trim();
            int buildIndex = string.IsNullOrEmpty(destination) ? -1 : SceneUtility.GetBuildIndexByScenePath(destination);
            bool available = buildIndex >= 0 ? Application.CanStreamedLevelBeLoaded(buildIndex) : !string.IsNullOrEmpty(destination) && Application.CanStreamedLevelBeLoaded(destination);
            if (!available)
            {
                Report("Game scene not configured or not included in the build.");
                return;
            }
            if (destination == gameObject.scene.name || destination == gameObject.scene.path || (buildIndex >= 0 && buildIndex == gameObject.scene.buildIndex))
            {
                Report("Choose the main game scene, not this tutorial scene."); return;
            }
            loading = true;
            if (enterButton != null) enterButton.interactable = false;
            if (statusText != null) statusText.text = "Loading...";
            try
            {
                var operation = buildIndex >= 0 ? SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single) : SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
                if (operation == null) throw new System.InvalidOperationException("Scene load did not start.");
            }
            catch (System.Exception ex)
            {
                loading = false;
                if (enterButton != null) enterButton.interactable = true;
                Report("Unable to load game scene. Check the Console.");
                Debug.LogException(ex, this);
            }
        }
        private void Report(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.LogWarning("Afterglow: " + message, this);
        }
    }
}
