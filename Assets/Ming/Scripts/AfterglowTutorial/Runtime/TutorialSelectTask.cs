using UnityEngine;

namespace Afterglow.Tutorial
{
    public sealed class TutorialSelectTask : TutorialTask
    {
        [SerializeField] private TutorialSelectTarget target;

        protected override void OnTaskBegan()
        {
            if (target != null)
            {
                target.gameObject.SetActive(true);
                target.OnSelected.AddListener(OnTargetSelected);
            }
        }

        protected override void OnTaskReset()
        {
            if (target != null)
            {
                target.OnSelected.RemoveListener(OnTargetSelected);
                target.gameObject.SetActive(false);
            }
        }

        protected override void OnTaskCompleted()
        {
            if (target != null) target.OnSelected.RemoveListener(OnTargetSelected);
        }

        private void OnTargetSelected()
        {
            CompleteTask();
        }
    }
}
