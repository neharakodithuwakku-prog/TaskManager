using System.Globalization;
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.UI
{
    /// <summary>
    /// The console menu (the "ConsoleUI" box in the architecture diagram).
    /// It only reads input and prints output; all rules live in TaskService.
    /// </summary>
    public class ConsoleUI
    {
        private const string DateFormat = "yyyy-MM-dd";

        // How often the background task checks for unsaved changes.
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(3);

        private readonly TaskService _taskService;
        private string _lastActivity = "None yet";

        public ConsoleUI(TaskService taskService)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        /// <summary>Runs the program until the user chooses Exit.</summary>
        public async Task RunAsync()
        {
            // Event handling: subscribe a handler (method) to the service's TaskChanged event.
            _taskService.TaskChanged += OnTaskChanged;

            await LoadTasksAsync();

            // Start the background auto-save. It is NOT awaited here, so it runs at the same
            // time as the menu loop below (even while we wait in Console.ReadLine).
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task autoSaveTask = _taskService.RunAutoSaveAsync(AutoSaveInterval, cancellation.Token);

            try
            {
                bool running = true;

                while (running)
                {
                    DisplayMenu();

                    string choice = ReadLine().Trim();

                    Console.WriteLine();

                    try
                    {
                        switch (choice)
                        {
                            case "1":
                                AddTask();
                                break;

                            case "2":
                                ViewTasks();
                                break;

                            case "3":
                                CompleteTask();
                                break;

                            case "4":
                                UpdateTask();
                                break;

                            case "5":
                                DeleteTask();
                                break;

                            case "6":
                                await SaveNowAsync();
                                break;

                            case "7":
                                running = false;
                                break;

                            default:
                                Console.WriteLine("Invalid option. Please choose 1-7.");
                                break;
                        }
                    }
                    // Exception handling: each kind of error gets its own handler (catch block).
                    catch (TaskValidationException ex)
                    {
                        Console.WriteLine($"Invalid data: {ex.Message}");
                    }
                    catch (TaskStorageException ex)
                    {
                        Console.WriteLine($"Storage problem: {ex.Message}");
                    }

                    if (running)
                    {
                        Pause();
                    }
                }
            }
            finally
            {
                // finalisation: runs on normal exit AND if an unexpected error ends the loop.
                cancellation.Cancel();
                await autoSaveTask;

                _taskService.TaskChanged -= OnTaskChanged;

                await SaveOnExitAsync();
            }
        }

        // ------------------------------------------------------------------
        // Event handler
        // ------------------------------------------------------------------

        private void OnTaskChanged(object? sender, TaskChangedEventArgs e)
        {
            _lastActivity = $"{e.Change} task #{e.Task.Id} ({e.Task.Title})";
        }

        // ------------------------------------------------------------------
        // Menu actions
        // ------------------------------------------------------------------

        private async Task LoadTasksAsync()
        {
            try
            {
                await _taskService.LoadAsync();
            }
            catch (TaskStorageException ex)
            {
                Console.WriteLine($"Warning: {ex.Message}");
                Console.WriteLine("Starting with an empty task list.");
                Pause();
            }
        }

        private void DisplayMenu()
        {
            // Console.Clear throws if the output is redirected to a file, so check first.
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            Console.WriteLine("==================================");
            Console.WriteLine("       PERSONAL TASK MANAGER");
            Console.WriteLine("==================================");
            Console.WriteLine($"Data file  : {_taskService.StorageLocation}");
            Console.WriteLine($"Tasks      : {_taskService.TaskCount}");


            Console.WriteLine("1. Add Task");
            Console.WriteLine("2. View Tasks");
            Console.WriteLine("3. Complete Task");
            Console.WriteLine("4. Update Task");
            Console.WriteLine("5. Delete Task");
            Console.WriteLine("6. Save Tasks");
            Console.WriteLine("7. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
        }

        private void AddTask()
        {
            Console.WriteLine("===== ADD TASK =====");
            Console.WriteLine();

            string title = ReadRequiredInput("Enter task title: ");
            string description = ReadRequiredInput("Enter task description: ");
            Priority priority = ReadPriority("Enter priority (L = Low, M = Medium, H = High) [M]: ", Priority.Medium);
            DateTime? dueDate = ReadOptionalDate($"Enter due date ({DateFormat}) or press Enter for none: ");

            TaskItem created = _taskService.AddTask(title, description, priority, dueDate);

            Console.WriteLine();
            Console.WriteLine($"Task #{created.Id} added. It is saved automatically within a few seconds.");
        }

        private void ViewTasks()
        {
            Console.WriteLine("===== VIEW TASKS =====");
            Console.WriteLine();

            TaskFilter filter = ReadFilter();

            IReadOnlyList<TaskItem> tasks = _taskService.GetTasks(filter);

            Console.WriteLine();
            Console.WriteLine($"===== {filter.ToString().ToUpperInvariant()} TASKS ({tasks.Count}) =====");
            Console.WriteLine();

            if (tasks.Count == 0)
            {
                Console.WriteLine("No tasks found.");
                return;
            }

            foreach (TaskItem task in tasks)
            {
                string status = task.IsCompleted ? "Completed" : "Pending";

                string due = task.DueDate.HasValue
                    ? task.DueDate.Value.ToString(DateFormat) + (task.IsOverdue() ? "  (OVERDUE)" : "")
                    : "None";

                Console.WriteLine($"ID         : {task.Id}");
                Console.WriteLine($"Title      : {task.Title}");
                Console.WriteLine($"Description: {task.Description}");
                Console.WriteLine($"Priority   : {task.Priority}");
                Console.WriteLine($"Due date   : {due}");
                Console.WriteLine($"Status     : {status}");
                Console.WriteLine($"Created on : {task.CreatedAt:yyyy-MM-dd HH:mm}");
                Console.WriteLine("----------------------------------");
            }
        }

        private void CompleteTask()
        {
            Console.WriteLine("===== COMPLETE TASK =====");
            Console.WriteLine();

            int id = ReadInteger("Enter the task ID: ");

            TaskItem? task = _taskService.GetTaskById(id);

            Console.WriteLine();

            if (task == null)
            {
                Console.WriteLine("Task not found.");
            }
            else if (task.IsCompleted)
            {
                Console.WriteLine("That task is already completed.");
            }
            else if (_taskService.CompleteTask(id))
            {
                Console.WriteLine("The task is marked as completed.");
            }
            else
            {
                Console.WriteLine("Task not found.");
            }
        }

        private void UpdateTask()
        {
            Console.WriteLine("===== UPDATE TASK =====");
            Console.WriteLine();

            int id = ReadInteger("Enter the task ID: ");

            TaskItem? task = _taskService.GetTaskById(id);

            if (task == null)
            {
                Console.WriteLine();
                Console.WriteLine("Task not found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press Enter at any question to keep the current value.");
            Console.WriteLine();

            string title = ReadWithDefault($"Title [{task.Title}]: ", task.Title);
            string description = ReadWithDefault($"Description [{task.Description}]: ", task.Description);
            Priority priority = ReadPriority($"Priority (L/M/H) [{task.Priority}]: ", task.Priority);
            DateTime? dueDate = ReadDueDateForUpdate(task.DueDate);

            bool updated = _taskService.UpdateTask(id, title, description, priority, dueDate);

            Console.WriteLine();
            Console.WriteLine(updated ? "Task updated successfully." : "Task could not be updated.");
        }

        private void DeleteTask()
        {
            Console.WriteLine("===== DELETE TASK =====");
            Console.WriteLine();

            int id = ReadInteger("Enter the task ID: ");

            TaskItem? task = _taskService.GetTaskById(id);

            if (task == null)
            {
                Console.WriteLine();
                Console.WriteLine("Task not found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Task: {task.Title}");
            Console.Write("Are you sure you want to delete this task? (y/n): ");

            string confirmation = ReadLine().Trim();

            Console.WriteLine();

            if (confirmation.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(_taskService.DeleteTask(id)
                    ? "Task deleted successfully."
                    : "Error. Task could not be deleted.");
            }
            else
            {
                Console.WriteLine("Error. Deletion cancelled...");
            }
        }

        private async Task SaveNowAsync()
        {
            Console.WriteLine("===== SAVE NOW =====");
            Console.WriteLine();

            await _taskService.SaveAsync();

            Console.WriteLine("Tasks saved successfully.");
            Console.WriteLine($"File location: {_taskService.StorageLocation}");
        }

        private async Task SaveOnExitAsync()
        {
            try
            {
                await _taskService.SaveAsync();

                Console.WriteLine();
                Console.WriteLine($"Tasks saved to: {_taskService.StorageLocation}");
                Console.WriteLine("Thank you for using Personal Task Manager.");
            }
            catch (TaskStorageException ex)
            {
                Console.WriteLine();
                Console.WriteLine($"WARNING - the tasks could not be saved: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Input helpers (validation loops: keep asking until the input is valid)
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads one line. Console.ReadLine returns null when the input stream is closed;
        /// we turn that into an exception so the program ends instead of looping forever.
        /// </summary>
        private static string ReadLine()
        {
            return Console.ReadLine() ?? throw new EndOfStreamException("Console input was closed.");
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Press Enter to continue...");
            ReadLine();
        }

        private static string ReadRequiredInput(string message)
        {
            while (true)
            {
                Console.Write(message);

                string input = ReadLine();

                if (!string.IsNullOrWhiteSpace(input))
                {
                    return input.Trim();
                }

                Console.WriteLine("This field cannot be empty.");
            }
        }

        /// <summary>Returns what the user typed, or "current" if they just pressed Enter.</summary>
        private static string ReadWithDefault(string message, string current)
        {
            Console.Write(message);

            string input = ReadLine();

            return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
        }

        private static int ReadInteger(string message)
        {
            while (true)
            {
                Console.Write(message);

                if (int.TryParse(ReadLine(), out int number))
                {
                    return number;
                }

                Console.WriteLine("Please enter a valid number.");
            }
        }

        private static Priority ReadPriority(string message, Priority defaultValue)
        {
            while (true)
            {
                Console.Write(message);

                string input = ReadLine().Trim();

                if (input.Length == 0)
                {
                    return defaultValue;
                }

                // Switch expression with "or" patterns; null means "not recognised".
                Priority? parsed = input.ToUpperInvariant() switch
                {
                    "L" or "LOW" => Priority.Low,
                    "M" or "MEDIUM" => Priority.Medium,
                    "H" or "HIGH" => Priority.High,
                    _ => null
                };

                if (parsed.HasValue)
                {
                    return parsed.Value;
                }

                Console.WriteLine("Please enter L, M or H.");
            }
        }

        private static TaskFilter ReadFilter()
        {
            while (true)
            {
                Console.WriteLine("Show: 1) All tasks 2) Pending tasks 3) Completed tasks  4) Overdue tasks");
                Console.Write("Enter your choice [1]: ");

                string input = ReadLine().Trim();

                TaskFilter? filter = input switch
                {
                    "" or "1" => TaskFilter.All,
                    "2" => TaskFilter.Pending,
                    "3" => TaskFilter.Completed,
                    "4" => TaskFilter.Overdue,
                    _ => null
                };

                if (filter.HasValue)
                {
                    return filter.Value;
                }

                Console.WriteLine("Please choose 1-4.");
            }
        }

        /// <summary>Reads a due date for a NEW task. Enter = no due date. Past dates are refused.</summary>
        private static DateTime? ReadOptionalDate(string message)
        {
            while (true)
            {
                Console.Write(message);

                string input = ReadLine().Trim();

                if (input.Length == 0)
                {
                    return null;
                }

                if (TryParseFutureDate(input, out DateTime date))
                {
                    return date;
                }
            }
        }

        /// <summary>Reads a due date for an UPDATE: Enter = keep, "none" = remove the date.</summary>
        private static DateTime? ReadDueDateForUpdate(DateTime? current)
        {
            string currentText = current.HasValue ? current.Value.ToString(DateFormat) : "none";

            while (true)
            {
                Console.Write($"Due date ({DateFormat}) [{currentText}] (type 'none' to remove): ");

                string input = ReadLine().Trim();

                if (input.Length == 0)
                {
                    return current;
                }

                if (input.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (TryParseFutureDate(input, out DateTime date))
                {
                    return date;
                }
            }
        }

        /// <summary>Parses yyyy-MM-dd and prints the reason if the text is not acceptable.</summary>
        private static bool TryParseFutureDate(string input, out DateTime date)
        {
            // ParseExact + InvariantCulture: the same format works on every computer.
            if (!DateTime.TryParseExact(input, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
            {
                Console.WriteLine($"Please use the format {DateFormat} (for example 2026-12-31).");
                return false;
            }

            if (date.Date < DateTime.Today)
            {
                Console.WriteLine("The due date cannot be in the past.");
                return false;
            }

            return true;
        }
    }
}
