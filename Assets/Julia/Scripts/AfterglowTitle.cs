using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AfterglowTitle : MonoBehaviour
{
    [SerializeField] private string nextScene = "Assets/Julia/Scenes/Tutorial.unity";
    [SerializeField, Min(0.1f)] private float writeSeconds = 3.9f;
    [SerializeField, Min(0.1f)] private float holdSeconds = 1.35f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.8f;
    private Canvas canvas;
    private bool enteringNextScene;

    private IEnumerator Start()
    {
        var surface = new GameObject("Title canvas", typeof(RectTransform), typeof(Canvas));
        surface.transform.SetParent(transform, false);
        canvas = surface.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.sortingOrder = short.MaxValue;
        BindCamera();

        var curtain = AddLayer<Image>("Fade curtain");
        curtain.color = Color.black;
        var ink = AddLayer<AfterglowLettering>("afterglow");
        var inkGroup = ink.gameObject.AddComponent<CanvasGroup>();

        AddLayer<AfterglowGlitter>("Glitter", ink.transform);

        AsyncOperation load = null;
        if (Application.CanStreamedLevelBeLoaded(nextScene))
        {
            DontDestroyOnLoad(gameObject);
            load = SceneManager.LoadSceneAsync(nextScene);
            load.allowSceneActivation = false;
        }

        yield return Animate(writeSeconds, ink.SetInk);
        yield return new WaitForSecondsRealtime(holdSeconds);
        if (load == null)
        {
            Debug.LogError($"Add {nextScene} to the enabled build scenes to finish the title transition.", this);
            yield break;
        }

        yield return Animate(fadeSeconds, t => inkGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t));
        ink.gameObject.SetActive(false);
        enteringNextScene = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        load.allowSceneActivation = true;
        yield return load;
        yield return null;
        yield return Animate(fadeSeconds, t => curtain.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t)));
        Destroy(gameObject);
    }

    private T AddLayer<T>(string layerName, Transform parent = null) where T : Graphic
    {
        var layer = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        layer.transform.SetParent(parent == null ? canvas.transform : parent, false);
        var graphic = layer.GetComponent<T>();
        graphic.rectTransform.anchorMin = Vector2.zero;
        graphic.rectTransform.anchorMax = Vector2.one;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static IEnumerator Animate(float seconds, System.Action<float> draw)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            draw(elapsed / seconds);
            yield return null;
        }
        draw(1f);
    }

    private void BindCamera()
    {
        var camera = Camera.main;
        if (camera == null) return;
        canvas.worldCamera = camera;
        canvas.planeDistance = enteringNextScene ? camera.nearClipPlane + 0.01f : 2f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindCamera();
    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
}
