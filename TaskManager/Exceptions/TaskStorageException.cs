namespace TaskManager.Exceptions
{
    public class TaskStorageException : Exception
    {
        public TaskStorageException(string message, Exception? innerException = null) // Constructor used to create a new instance of the TaskStorageException class with a error message and an optional inner exception
            : base(message, innerException)
        {
        }
    }
}