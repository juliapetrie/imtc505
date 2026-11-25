using System;
using UnityEngine;

namespace Afterglow.Tutorial
{
    public abstract class TutorialTask : MonoBehaviour
    {
        private Action<TutorialTask> onCompleted;
        private bool isCompleted;
        protected bool isRunning;

        public bool IsCompleted => isCompleted;
        public bool IsRunning => isRunning;

        public void BeginTask(Action<TutorialTask> completed)
        {
            onCompleted = completed;
            isCompleted = false;
            isRunning = true;
            gameObject.SetActive(true);
            OnTaskBegan();
        }

        public virtual void ResetTask()
        {
            isCompleted = false;
            isRunning = false;
            OnTaskReset();
        }

        protected void CompleteTask()
        {
            if (!isRunning || isCompleted) return;
            isCompleted = true;
            isRunning = false;
            OnTaskCompleted();
            onCompleted?.Invoke(this);
        }

        protected virtual void OnTaskBegan() { }
        protected virtual void OnTaskReset() { }
        protected virtual void OnTaskCompleted() { }
    }
}
