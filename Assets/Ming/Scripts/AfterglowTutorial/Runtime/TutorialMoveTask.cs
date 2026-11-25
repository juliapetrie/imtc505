using UnityEngine;
using UnityEngine.InputSystem;

namespace Afterglow.Tutorial
{
    public sealed class TutorialMoveTask : TutorialTask
    {
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform moveTarget;
        [SerializeField] private TutorialVisualPulse targetVisual;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private float minInputMagnitude = 0.35f;
        [SerializeField] private float targetRadius = 0.45f;
        [SerializeField] private bool requireThumbstickInput = true;

        private void Update()
        {
            if (!IsRunning || playerRoot == null || moveTarget == null) return;
            bool inTarget = Vector3.Distance(ProjectXZ(playerRoot.position), ProjectXZ(moveTarget.position)) <= targetRadius;
            bool hasInput = !requireThumbstickInput || (moveAction != null && moveAction.action != null && moveAction.action.ReadValue<Vector2>().magnitude >= minInputMagnitude);
            if (inTarget && hasInput)
            {
                if (targetVisual != null) targetVisual.MarkCompleted(true);
                CompleteTask();
            }
        }

        protected override void OnTaskBegan()
        {
            if (moveTarget != null) moveTarget.gameObject.SetActive(true);
            if (targetVisual != null) targetVisual.MarkCompleted(false);
            if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
        }

        protected override void OnTaskReset()
        {
            if (moveTarget != null) moveTarget.gameObject.SetActive(false);
        }

        private Vector3 ProjectXZ(Vector3 p) => new Vector3(p.x, 0f, p.z);
    }
}
