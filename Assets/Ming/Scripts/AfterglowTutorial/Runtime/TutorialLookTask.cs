using UnityEngine;

namespace Afterglow.Tutorial
{
    public sealed class TutorialLookTask : TutorialTask
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform leftTarget;
        [SerializeField] private Transform rightTarget;
        [SerializeField] private TutorialVisualPulse leftVisual;
        [SerializeField] private TutorialVisualPulse rightVisual;
        [SerializeField] private float requiredLookSeconds = 0.8f;
        [SerializeField] private float maxAngle = 12f;

        private float leftTimer;
        private float rightTimer;
        private bool leftDone;
        private bool rightDone;

        private void Update()
        {
            if (!IsRunning || cameraTransform == null) return;
            if (!leftDone) leftDone = TickTarget(leftTarget, ref leftTimer, leftVisual);
            if (!rightDone) rightDone = TickTarget(rightTarget, ref rightTimer, rightVisual);
            if (leftDone && rightDone) CompleteTask();
        }

        protected override void OnTaskBegan()
        {
            leftTimer = rightTimer = 0f;
            leftDone = rightDone = false;
            if (leftVisual != null) leftVisual.MarkCompleted(false);
            if (rightVisual != null) rightVisual.MarkCompleted(false);
            if (leftTarget != null) leftTarget.gameObject.SetActive(true);
            if (rightTarget != null) rightTarget.gameObject.SetActive(true);
        }

        protected override void OnTaskReset()
        {
            if (leftTarget != null) leftTarget.gameObject.SetActive(false);
            if (rightTarget != null) rightTarget.gameObject.SetActive(false);
        }

        protected override void OnTaskCompleted()
        {
            if (leftVisual != null) leftVisual.MarkCompleted(true);
            if (rightVisual != null) rightVisual.MarkCompleted(true);
        }

        private bool TickTarget(Transform target, ref float timer, TutorialVisualPulse visual)
        {
            if (target == null) return true;
            Vector3 toTarget = (target.position - cameraTransform.position).normalized;
            float angle = Vector3.Angle(cameraTransform.forward, toTarget);
            if (angle <= maxAngle) timer += Time.deltaTime;
            else timer = 0f;
            bool done = timer >= requiredLookSeconds;
            if (done && visual != null) visual.MarkCompleted(true);
            return done;
        }
    }
}
