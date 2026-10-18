using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Repositories;

namespace TaskManager.Services
{
    public class TaskService // Provides methods to manage tasks, including adding, updating, completing, deleting, loading, and saving tasks
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