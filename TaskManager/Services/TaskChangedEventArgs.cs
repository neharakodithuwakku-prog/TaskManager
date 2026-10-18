using TaskManager.Models;

namespace TaskManager.Services
{
    public enum TaskChangeType // Represents the type of change that was done to a task.
    {
        Added,
        Updated,
        Completed,
        Deleted
    }

    public class TaskChangedEventArgs : EventArgs // Represents the event arguments for a task change event.
    {
        public TaskChangeType Change { get; }

        public TaskItem Task { get; } // Represents the task that was changed.

        public TaskChangedEventArgs(TaskChangeType change, TaskItem task)
        {
            Change = change;
            Task = task;
        }
    }
}