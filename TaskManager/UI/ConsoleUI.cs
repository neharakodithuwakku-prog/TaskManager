using System.Globalization;
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.UI
{
    public class ConsoleUI // Displays the console menu, handles the user input and calls TaskService methods
    {
        private const string DateFormat = "yyyy-MM-dd";

      
        private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(3); // the interval at which the background auto-save task runs

        private readonly TaskService _taskService;
        private string _lastActivity = "None yet"; 

        public ConsoleUI(TaskService taskService) // constructor, takes a TaskService instance as a dependency
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService)); 
        }

        public async Task RunAsync() // The main menu loop, runs until the user chooses Exit
        {
            
            _taskService.TaskChanged += OnTaskChanged; 

            await LoadTasksAsync(); // load tasks from storage at the start of the program

            using CancellationTokenSource cancellation = new CancellationTokenSource(); // used to cancel the background auto-save task when the program exits
            Task autoSaveTask = _taskService.RunAutoSaveAsync(AutoSaveInterval, cancellation.Token); // start the background auto-save task

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
                    
                    catch (TaskValidationException ex) // catch validation errors thrown by the service
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
               
                cancellation.Cancel(); 
                await autoSaveTask;

                _taskService.TaskChanged -= OnTaskChanged;

                await SaveOnExitAsync();
            }
        }

        private void OnTaskChanged(object? sender, TaskChangedEventArgs e) // called when the TaskChanged event is raised by the TaskService
        {
            _lastActivity = $"{e.Change} task #{e.Task.Id} ({e.Task.Title})";
        }

        private async Task LoadTasksAsync() // called at the start of RunAsync to load tasks from storage
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
            
            if (!Console.IsOutputRedirected) 
            {
                Console.Clear();
            }

            Console.WriteLine("==================================");
            Console.WriteLine("       PERSONAL TASK MANAGER");
            Console.WriteLine("==================================");
            Console.WriteLine($"Data file  : {_taskService.StorageLocation}");
            Console.WriteLine($"Tasks      : {_taskService.TaskCount}");


            Console.WriteLine("1. Add a Task");
            Console.WriteLine("2. View the Tasks");
            Console.WriteLine("3. Complete a Task");
            Console.WriteLine("4. Update a Task");
            Console.WriteLine("5. Delete a Task");
            Console.WriteLine("6. Save Tasks");
            Console.WriteLine("7. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");
        }

        private void AddTask() // called when the user chooses Add Task
        {
            Console.WriteLine("===== ADD A TASK =====");
            Console.WriteLine();

            string title = ReadRequiredInput("Enter task title: ");
            string description = ReadRequiredInput("Enter task description: ");
            Priority priority = ReadPriority("Enter priority (L = Low, M = Medium, H = High) [M]: ", Priority.Medium);
            DateTime? dueDate = ReadOptionalDate($"Enter due date ({DateFormat}) or press Enter for none: ");

            TaskItem created = _taskService.AddTask(title, description, priority, dueDate);

            Console.WriteLine();
            Console.WriteLine($"Task #{created.Id} added. It is saved automatically within a few seconds.");
        }

        private void ViewTasks() // called when the user chooses View Tasks
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

        private void CompleteTask() // called when the user chooses Complete Task
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

        private void UpdateTask() // called when the user chooses Update Task
        {
            Console.WriteLine("===== UPDATE A TASK =====");
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

        private void DeleteTask() // called when the user chooses Delete Task
        {
            Console.WriteLine("===== DELETE A TASK =====");
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

        private async Task SaveNowAsync() // called when the user chooses Save Tasks    
        {
            Console.WriteLine("===== SAVE NOW =====");
            Console.WriteLine();

            await _taskService.SaveAsync();

            Console.WriteLine("Tasks saved successfully.");
            Console.WriteLine($"File location: {_taskService.StorageLocation}");
        }

        private async Task SaveOnExitAsync() // called when the user chooses Exit, or if an unexpected error ends the menu loop
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
        private static string ReadLine() // returns the string entered by the user, throws an exception if the input stream is closed
        {
            return Console.ReadLine() ?? throw new EndOfStreamException("Console input was closed.");
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Press Enter to continue...");
            ReadLine();
        }

        private static string ReadRequiredInput(string message) // returns the string entered by the user, keeps asking until a non-empty string is entered
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

        private static string ReadWithDefault(string message, string current) // returns the current value if the user presses Enter
        {
            Console.Write(message);

            string input = ReadLine();

            return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
        }

        private static int ReadInteger(string message) // returns the integer entered by the user, keeps asking until a valid integer is entered
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

        private static Priority ReadPriority(string message, Priority defaultValue) // returns the priority chosen by the user, or the default value if they press Enter
        {
            while (true)
            {
                Console.Write(message);

                string input = ReadLine().Trim();

                if (input.Length == 0)
                {
                    return defaultValue;
                }

                Priority? parsed = input.ToUpperInvariant() switch // convert the input to uppercase and matches it to the Priority enum value
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

        private static TaskFilter ReadFilter() // returns the filter chosen by the user
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

        
        private static DateTime? ReadOptionalDate(string message) // returns null if the user presses Enter
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

        
        private static DateTime? ReadDueDateForUpdate(DateTime? current) // current = the existing due date, or null if none
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

        private static bool TryParseFutureDate(string input, out DateTime date)
        {
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
