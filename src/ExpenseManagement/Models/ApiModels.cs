namespace ExpenseManagement.Models;

public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}

public class UpdateExpenseRequest
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}

public class ApproveRejectRequest
{
    public int ReviewerId { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = new();
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ErrorInfo
{
    public string Message { get; set; } = string.Empty;
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Guidance { get; set; }
}
