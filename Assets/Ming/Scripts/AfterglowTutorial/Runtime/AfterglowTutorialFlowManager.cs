using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Afterglow.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class AfterglowTutorialFlowManager : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Step { public string name; public GameObject page; public TutorialTask task; }
        [SerializeField] private List<Step> steps = new List<Step>();
        [SerializeField] private int startIndex = 0;
        [SerializeField] private float autoAdvanceDelay = 0.9f;
        [SerializeField] private UnityEvent onFlowCompleted = new UnityEvent();
        private int currentIndex = -1;
        private Coroutine pending;
        public IReadOnlyList<Step> Steps => steps;
        public int CurrentIndex => currentIndex;
        public UnityEvent OnFlowCompleted => onFlowCompleted;
        private void Awake() { HideAllPages(); }
        public void StartFlow() { ShowStep(startIndex); }
        public void RestartFlow()
        {
            CancelPending();
            foreach (var step in steps) if (step != null && step.task != null) step.task.ResetTask();
            ShowStep(startIndex);
        }
        public void AdvanceFromPage(int index)
        {
            if (currentIndex == index && index >= 0 && index < steps.Count) GoToNextStep();
        }
        public void GoToNextStep() { if (currentIndex < steps.Count) ShowStep(currentIndex+1); }
        public void ShowStep(int index)
        {
            CancelPending();
            if (currentIndex >= 0 && currentIndex < steps.Count)
                steps[currentIndex]?.task?.ResetTask();
            HideAllPages();
            if (index < 0 || index >= steps.Count)
            {
                bool alreadyCompleted = currentIndex == steps.Count;
                currentIndex = steps.Count;
                if (!alreadyCompleted) onFlowCompleted.Invoke();
                return;
            }
            currentIndex = index;
            var step = steps[index];
            if (step == null || step.page == null) { GoToNextStep(); return; }
            step.page.SetActive(true);
            var page = step.page.GetComponent<AfterglowTutorialPage>();
            if (page != null) page.Bind(this,index);
            if (step.task != null) step.task.BeginTask(OnCurrentTaskCompleted);
        }
        private void OnCurrentTaskCompleted(TutorialTask task)
        {
            if (!isActiveAndEnabled || currentIndex < 0 || currentIndex >= steps.Count || steps[currentIndex].task != task) return;
            CancelPending();
            pending = StartCoroutine(AdvanceAfterDelay(currentIndex));
        }
        private IEnumerator AdvanceAfterDelay(int index)
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            pending = null;
            AdvanceFromPage(index);
        }
        private void CancelPending() { if (pending != null) { StopCoroutine(pending); pending = null; } }
        private void OnDisable()
        {
            CancelPending();
            if (currentIndex >= 0 && currentIndex < steps.Count) steps[currentIndex]?.task?.ResetTask();
        }
        private void HideAllPages()
        {
            foreach (var step in steps) if (step != null && step.page != null) step.page.SetActive(false);
        }
    }
}
