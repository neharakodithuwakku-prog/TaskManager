namespace TaskManager.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty; //Avoids null values
        public string Description { get; set; } = string.Empty; // makes it easier to display tasks without checking for nulls
        public Priority Priority { get; set; } = Priority.Medium; // Default priority is Medium
        public DateTime? DueDate { get; set; }// Nullable DateTime to allow tasks without a due date
        public bool IsCompleted { get; set; } // Indicates whether the task is completed or not
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Automatically set to the current date and time when the task is created

        public TaskItem()
        {
        }

        
        public TaskItem(int id, string title, string description, Priority priority, DateTime? dueDate) // Constructor to initialize a new task with the provided values
        {
            Id = id;
            Title = title;
            Description = description;
            Priority = priority;
            DueDate = dueDate;
            IsCompleted = false;
            CreatedAt = DateTime.Now;
        }

        public bool IsOverdue() =>
            !IsCompleted && DueDate.HasValue && DueDate.Value.Date < DateTime.Today; // Checks if the task is overdue

        public TaskItem Clone() => (TaskItem)MemberwiseClone(); // Creates a copy of the current task item

        public override string ToString() => $"#{Id} {Title} [{Priority}]"; // Returns a string representation of the task item, including its ID, title, and priority
    }
}