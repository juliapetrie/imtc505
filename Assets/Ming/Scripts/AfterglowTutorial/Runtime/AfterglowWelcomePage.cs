using UnityEngine;
using UnityEngine.Events;

namespace Afterglow.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class AfterglowWelcomePage : MonoBehaviour
    {
        [Tooltip("Optional: drag the Introduction page here when it is ready. It must be outside this Welcome page.")]
        [SerializeField] private GameObject nextPage;
        [Tooltip("Optional: connect a future TutorialManager or other response to START.")]
        [SerializeField] private UnityEvent onStartRequested = new UnityEvent();

        public UnityEvent OnStartRequested => onStartRequested;

        public void StartTutorial()
        {
            if (nextPage != null)
            {
                if (nextPage == gameObject || nextPage.transform.IsChildOf(transform) || transform.IsChildOf(nextPage.transform))
                {
                    Debug.LogWarning("Introduction must be a separate page, not an ancestor or child of Welcome.", this);
                }
                else
                {
                    nextPage.SetActive(true);
                    gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.Log("Afterglow: START selected. Connect Next Page or On Start Requested when Introduction is ready.", this);
            }
            onStartRequested.Invoke();
        }
    }
}
