using UnityEngine;

public class ShowOnClick : MonoBehaviour
{
    public GameObject sphere;

    void Start()
    {
        sphere.SetActive(false);
    }

    // Hook this up to the button's On Click
    public void Show()
    {
        sphere.SetActive(true);
    }
}