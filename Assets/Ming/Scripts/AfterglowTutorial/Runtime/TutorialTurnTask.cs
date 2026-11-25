using UnityEngine;
using UnityEngine.InputSystem;

namespace Afterglow.Tutorial
{
    public sealed class TutorialTurnTask : TutorialTask
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform leftTarget;
        [SerializeField] private Transform rightTarget;
        [SerializeField] private TutorialVisualPulse leftVisual;
        [SerializeField] private TutorialVisualPulse rightVisual;
        [SerializeField] private InputActionReference turnAction;
        [SerializeField] private float minStickX = 0.4f;
        [SerializeField] private float requiredYawDelta = 22f;

        private float startYaw;
        private bool leftDone;
        private bool rightDone;

        private void Update()
        {
            if (!IsRunning || playerRoot == null) return;
            Vector2 input = turnAction != null && turnAction.action != null ? turnAction.action.ReadValue<Vector2>() : Vector2.zero;
            float delta = Mathf.DeltaAngle(startYaw, playerRoot.eulerAngles.y);

            if (!leftDone && input.x <= -minStickX && delta <= -requiredYawDelta)
            {
                leftDone = true;
                if (leftVisual != null) leftVisual.MarkCompleted(true);
                if (leftTarget != null) leftTarget.gameObject.SetActive(false);
            }
            if (!rightDone && input.x >= minStickX && delta >= requiredYawDelta)
            {
                rightDone = true;
                if (rightVisual != null) rightVisual.MarkCompleted(true);
                if (rightTarget != null) rightTarget.gameObject.SetActive(false);
            }
            if (leftDone && rightDone) CompleteTask();
        }

        protected override void OnTaskBegan()
        {
            startYaw = playerRoot != null ? playerRoot.eulerAngles.y : 0f;
            leftDone = rightDone = false;
            if (leftTarget != null) leftTarget.gameObject.SetActive(true);
            if (rightTarget != null) rightTarget.gameObject.SetActive(true);
            if (leftVisual != null) leftVisual.MarkCompleted(false);
            if (rightVisual != null) rightVisual.MarkCompleted(false);
            if (turnAction != null && turnAction.action != null) turnAction.action.Enable();
        }

        protected override void OnTaskReset()
        {
            if (leftTarget != null) leftTarget.gameObject.SetActive(false);
            if (rightTarget != null) rightTarget.gameObject.SetActive(false);
        }
    }
}
