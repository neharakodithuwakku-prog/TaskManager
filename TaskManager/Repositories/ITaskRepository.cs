using TaskManager.Models;

namespace TaskManager.Repositories
{
    public interface ITaskRepository
    {
        string Location { get; } // The location of the storage file.

        Task<List<TaskItem>> LoadAsync(); // Loads all the tasks from storage.

        Task SaveAsync(IEnumerable<TaskItem> tasks); // Saves all the tasks to storage.
    }
}