
using TaskManager.Exceptions;
using TaskManager.Repositories;
using TaskManager.Services;
using TaskManager.UI;

namespace TaskManager
{

    internal static class Program
    {
        private static async Task<int> Main() 
        {
            try
            {
                // The tasks.json is created in the Data folder.
                string filePath = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "tasks.json"));

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
                
                Console.WriteLine($"Unexpected error: {ex.Message}"); // error handling
                return 1;
            }
        }
    }
}
