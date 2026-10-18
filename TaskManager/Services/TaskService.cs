
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Repositories;

namespace TaskManager.Services
{
    public class TaskService // Provides methods to manage tasks, including adding, updating, completing, deleting, loading, and saving tasks
    {
        private readonly ITaskRepository _repository;
        private readonly List<TaskItem> _tasks = new List<TaskItem>();

        private readonly object _lock = new object();

        private readonly SemaphoreSlim _saveGate = new SemaphoreSlim(1, 1); // Ensures that only one save operation can run at a time

        private volatile bool _isDirty; // shows whether there are unsaved changes
        private DateTime? _lastSavedAt;
        private string? _lastSaveError;

        public event EventHandler<TaskChangedEventArgs>? TaskChanged; // Raised when a task is added, updated, completed, or deleted.

        public TaskService(ITaskRepository repository) // Constructor that takes an ITaskRepository to handle loading and saving tasks
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public string StorageLocation => _repository.Location;

        public DateTime? LastSavedAt // Returns the last time the tasks were successfully saved, or null if they have never been saved
        {
            get { lock (_lock) { return _lastSavedAt; } }
        }

        public string? LastSaveError // Returns the last error message of a failed save operation, or null if the last save was successful
        {
            get { lock (_lock) { return _lastSaveError; } }
        }

        public int TaskCount // Returns the number of tasks in the in-memory list
        {
            get { lock (_lock) { return _tasks.Count; } }
        }

        public async Task LoadAsync() // Loads all tasks
        {
            List<TaskItem> loaded = await _repository.LoadAsync(); // Load tasks from the repository 

            lock (_lock)
            {
                _tasks.Clear();
                _tasks.AddRange(loaded);
                _isDirty = false;
            }
        }

        public async Task SaveAsync() // Saves all tasks to the repository, if there are unsaved changes
        {
            await _saveGate.WaitAsync();
            try
            {
                List<TaskItem> snapshot;

                lock (_lock) // Takes a snapshot of the current tasks to save, and mark them as saved
                {
                    snapshot = _tasks.Select(task => task.Clone()).ToList();
                    _isDirty = false;
                }

                try
                {
                    await _repository.SaveAsync(snapshot);

                    lock (_lock)
                    {
                        _lastSavedAt = DateTime.Now;
                        _lastSaveError = null;
                    }
                }
                catch (TaskStorageException ex)
                {
                    _isDirty = true;
                    lock (_lock)
                    {
                        _lastSaveError = ex.Message;
                    }
                    throw;
                }
            }
            finally
            {
                _saveGate.Release();
            }
        }

        public async Task RunAutoSaveAsync(TimeSpan interval, CancellationToken cancellationToken) // Runs an auto-save loop that saves tasks at regular intervals, if there are unsaved changes
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_isDirty)
                {
                    try
                    {
                        await SaveAsync();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }      
        public IReadOnlyList<TaskItem> GetTasks(TaskFilter filter = TaskFilter.All) // Returns a copy of the tasks that match the given filter, sorted by completion status, priority, due date, and id
        {
            lock (_lock)
            {
                return _tasks
                    .Where(task => Matches(task, filter))
                    .OrderBy(task => task.IsCompleted)
                    .ThenByDescending(task => task.Priority)
                    .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                    .ThenBy(task => task.Id)
                    .Select(task => task.Clone())
                    .ToList();
            }
        }

        public TaskItem? GetTaskById(int id) // Returns a copy of the task with the given id, or null if it does not exist
        {
            lock (_lock)
            {
                return _tasks.FirstOrDefault(task => task.Id == id)?.Clone();
            }
        }
        public TaskItem AddTask(string title, string? description, Priority priority, DateTime? dueDate) // Adds a new task with the given data, and returns a copy of the created task
        {
            string cleanTitle = ValidateTitle(title);
            ValidateDueDate(dueDate);

            TaskItem created;

            lock (_lock)
            {
                int newId = _tasks.Count == 0 ? 1 : _tasks.Max(existing => existing.Id) + 1; // Generate a new id that is one greater than the current maximum id, or 1 if there are no tasks

                TaskItem task = new TaskItem(
                    newId,
                    cleanTitle,
                    description?.Trim() ?? string.Empty,
                    priority,
                    dueDate?.Date);

                _tasks.Add(task);
                _isDirty = true;
                created = task.Clone();
            }

            OnTaskChanged(TaskChangeType.Added, created);
            return created;
        }

        public bool CompleteTask(int id) // Marks a task as completed. Returns false if the id does not exist.
        {
            TaskItem changed;

            lock (_lock)
            {
                TaskItem? task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null)
                {
                    return false;
                }

                task.IsCompleted = true;
                _isDirty = true;
                changed = task.Clone();
            }

            OnTaskChanged(TaskChangeType.Completed, changed);
            return true;
        }

        public bool UpdateTask(int id, string title, string? description, Priority priority, DateTime? dueDate) // Updates a task with the given data. Returns false if the id does not exist.
        {
            string cleanTitle = ValidateTitle(title);

            TaskItem changed;

            lock (_lock)
            {
                TaskItem? task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null)
                {
                    return false;
                }

                if (dueDate?.Date != task.DueDate?.Date) 
                {
                    ValidateDueDate(dueDate);
                }

                task.Title = cleanTitle;
                task.Description = description?.Trim() ?? string.Empty;
                task.Priority = priority;
                task.DueDate = dueDate?.Date;
                _isDirty = true;
                changed = task.Clone();
            }

            OnTaskChanged(TaskChangeType.Updated, changed); // change type is Updated
            return true;
        }

        public bool DeleteTask(int id) // Deletes a task with the given id. Returns false if the id does not exist.
        {
            TaskItem removed;

            lock (_lock)
            {
                TaskItem? task = _tasks.FirstOrDefault(t => t.Id == id);
                if (task == null)
                {
                    return false;
                }

                _tasks.Remove(task);
                _isDirty = true;
                removed = task.Clone();
            }

            OnTaskChanged(TaskChangeType.Deleted, removed);
            return true;
        }

        private void OnTaskChanged(TaskChangeType change, TaskItem task) // Raises the TaskChanged event with the given change type and task.
        {
            TaskChanged?.Invoke(this, new TaskChangedEventArgs(change, task));
        }

        private static bool Matches(TaskItem task, TaskFilter filter) => filter switch // Returns true if the task matches the given filter
        {
            TaskFilter.Pending => !task.IsCompleted,
            TaskFilter.Completed => task.IsCompleted,
            TaskFilter.Overdue => task.IsOverdue(),
            _ => true
        };

        private static string ValidateTitle(string? title) // Checks the title of the task
        {
            if (string.IsNullOrWhiteSpace(title)) // checks if the title is null, empty, or consists only of whitespace characters
            {
                throw new TaskValidationException("The title cannot be empty."); 
            }

            string trimmed = title.Trim();
            if (trimmed.Length > 100) // checks if the title is longer than 100 characters
            {
                throw new TaskValidationException("The title cannot be longer than 100 characters.");
            }

            return trimmed;
        }

        private static void ValidateDueDate(DateTime? dueDate) // Checks the due date of the task
        {
            if (dueDate.HasValue && dueDate.Value.Date < DateTime.Today)
            {
                throw new TaskValidationException("The due date cannot be in the past.");
            }
        }
    }
}
