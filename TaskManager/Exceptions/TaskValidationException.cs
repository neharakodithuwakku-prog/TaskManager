namespace TaskManager.Exceptions
{
  
    public class TaskValidationException : Exception
    {
        public TaskValidationException(string message) : base(message) // Constructor used to create a new instance of the TaskValidationException class with a error message
        {
        }
    }
}