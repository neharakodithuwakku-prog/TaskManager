using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManager.Exceptions;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public class JsonTaskRepository : ITaskRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions 
        {
            WriteIndented = true, // Makes the JSON file easier to read
            PropertyNameCaseInsensitive = true, // Allows for case-insensitive property names when deserializing JSON
            Converters = { new JsonStringEnumConverter() } // Converts enum values to their string representation in JSON
        };

        private readonly string _filePath;

        public string Location => _filePath;

        public JsonTaskRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            }
            _filePath = Path.GetFullPath(filePath); 

            try
            {
                string? folder = Path.GetDirectoryName(_filePath); // Get the directory path from the file path 
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new TaskStorageException(
                    $"Cannot create the data folder for '{_filePath}': {ex.Message}", ex);
            }
        }

        public async Task<List<TaskItem>> LoadAsync()
        {
            if (!File.Exists(_filePath))
            {
                return new List<TaskItem>();
            }

            try
            {
                string json = await File.ReadAllTextAsync(_filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<TaskItem>();
                }

                List<TaskItem>? tasks = JsonSerializer.Deserialize<List<TaskItem>>(json, JsonOptions);
                return tasks ?? new List<TaskItem>();
            }
            catch (JsonException ex)
            {
              
                string backupPath = BackupDamagedFile(); // Save a copy of the damaged file for debugging
                throw new TaskStorageException(
                    $"tasks.json contains invalid JSON. A copy was saved as '{backupPath}'.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new TaskStorageException($"Error reading '{_filePath}': {ex.Message}", ex);
            }
        }

        public async Task SaveAsync(IEnumerable<TaskItem> tasks) 
        {
            string tempPath = _filePath + ".tmp";

            try
            {
                string json = JsonSerializer.Serialize(tasks, JsonOptions); 
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new TaskStorageException($"Error saving '{_filePath}': {ex.Message}", ex);
            }
        }
        private string BackupDamagedFile()
        {
            string backupPath = $"{_filePath}.damaged-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Copy(_filePath, backupPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
             
            }
            return backupPath;
        }
    }
}