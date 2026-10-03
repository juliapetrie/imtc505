using UnityEngine;

public class StoryPolesInfo : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;

    private void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void ToggleInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(!infoPanel.activeSelf);
    }
}