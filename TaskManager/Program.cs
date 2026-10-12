using TaskManager.Exceptions;
using TaskManager.Repositories;
using TaskManager.Services;
using TaskManager.UI;

namespace TaskManager
{

    internal static class Program
    {
        // Task<int>: the returned number is the exit code (0 = OK, 1 = error).
        private static async Task<int> Main()
        {
            try
            {
                // The tasks.json is created in <project>\bin\Debug\net10.0\Data
                string filePath = Path.Combine(AppContext.BaseDirectory, "Data", "tasks.json");

                ITaskRepository repository = new JsonTaskRepository(filePath);
                TaskService taskService = new TaskService(repository);
                ConsoleUI ui = new ConsoleUI(taskService);

                await ui.RunAsync();
                return 0;
            }
            catch (TaskStorageException ex)
            {
                Console.WriteLine($"Cannot start: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                // Error handling: logging the error and returning a non-zero exit code.
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return 1;
            }
        }
    }
}