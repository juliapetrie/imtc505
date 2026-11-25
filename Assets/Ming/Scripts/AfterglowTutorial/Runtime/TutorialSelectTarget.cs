using UnityEngine;
using UnityEngine.Events;

namespace Afterglow.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialSelectTarget : MonoBehaviour
    {
        [SerializeField] private UnityEvent onSelected = new UnityEvent();
        public UnityEvent OnSelected => onSelected;
        public void SelectTarget() => onSelected.Invoke();
    }
}
