using UnityEngine;

namespace Afterglow.Tutorial
{
    public sealed class TutorialGrabTask : TutorialTask
    {
        [SerializeField] private Transform grabbable;
        [SerializeField] private Transform sourcePedestal;
        [SerializeField] private Transform destinationPedestal;
        [SerializeField] private Collider targetZone;
        [SerializeField] private TutorialVisualPulse targetVisual;
        [SerializeField] private float completeHoldSeconds = 0.2f;

        private float insideTimer;
        private Vector3 startPosition;

        private void Update()
        {
            if (!IsRunning || grabbable == null || targetZone == null) return;
            bool moved = Vector3.Distance(grabbable.position, startPosition) > 0.2f;
            bool inside = targetZone.bounds.Contains(grabbable.position);
            if (moved && inside)
            {
                insideTimer += Time.deltaTime;
                if (insideTimer >= completeHoldSeconds)
                {
                    if (targetVisual != null) targetVisual.MarkCompleted(true);
                    CompleteTask();
                }
            }
            else insideTimer = 0f;
        }

        protected override void OnTaskBegan()
        {
            insideTimer = 0f;
            startPosition = grabbable != null ? grabbable.position : Vector3.zero;
            if (sourcePedestal != null) sourcePedestal.gameObject.SetActive(true);
            if (destinationPedestal != null) destinationPedestal.gameObject.SetActive(true);
            if (targetZone != null) targetZone.gameObject.SetActive(true);
            if (grabbable != null) grabbable.gameObject.SetActive(true);
            if (targetVisual != null) targetVisual.MarkCompleted(false);
        }

        protected override void OnTaskReset()
        {
            if (sourcePedestal != null) sourcePedestal.gameObject.SetActive(false);
            if (destinationPedestal != null) destinationPedestal.gameObject.SetActive(false);
            if (targetZone != null) targetZone.gameObject.SetActive(false);
            if (grabbable != null) grabbable.gameObject.SetActive(false);
        }
    }
}
