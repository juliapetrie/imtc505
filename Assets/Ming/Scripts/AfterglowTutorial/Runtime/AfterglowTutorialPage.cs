using UnityEngine;

namespace Afterglow.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class AfterglowTutorialPage : MonoBehaviour
    {
        [SerializeField] private bool showContinueButton = false;
        [SerializeField] private GameObject continueButton;

        private AfterglowTutorialFlowManager flow;
        private int stepIndex = -1;

        public void Bind(AfterglowTutorialFlowManager owner, int index)
        {
            flow = owner;
            stepIndex = index;
            if (continueButton != null) continueButton.SetActive(showContinueButton);
        }

        public void GoNext()
        {
            if (flow != null) flow.AdvanceFromPage(stepIndex);
            else Debug.LogWarning("Afterglow Tutorial Page is not bound to a flow manager.", this);
        }
    }
}
