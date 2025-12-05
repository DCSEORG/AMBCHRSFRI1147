using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public List<ExpenseSummary> StatusSummary { get; set; } = new();
    public List<CategorySummary> CategorySummary { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            StatusSummary = await _expenseService.GetExpenseSummaryAsync();
            CategorySummary = await _expenseService.GetExpensesByCategoryAsync();
            RecentExpenses = (await _expenseService.GetExpensesAsync()).Take(5).ToList();

            TotalAmount = StatusSummary.Sum(s => s.TotalAmountDisplay);
            TotalCount = StatusSummary.Sum(s => s.ExpenseCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            ViewData["ErrorInfo"] = CreateErrorInfo(ex);
        }
    }

    private static ErrorInfo CreateErrorInfo(Exception ex)
    {
        var errorInfo = new ErrorInfo
        {
            Message = "Failed to load dashboard data"
        };

        // Extract file and line info from stack trace
        var stackTrace = ex.StackTrace;
        if (!string.IsNullOrEmpty(stackTrace))
        {
            var match = System.Text.RegularExpressions.Regex.Match(stackTrace, @"at .+ in (.+):line (\d+)");
            if (match.Success)
            {
                errorInfo.File = System.IO.Path.GetFileName(match.Groups[1].Value);
                errorInfo.Line = int.Parse(match.Groups[2].Value);
            }
        }

        // Add guidance for common issues
        if (ex.Message.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase))
        {
            errorInfo.Guidance = "Ensure AZURE_CLIENT_ID environment variable is set and the managed identity has database permissions.";
        }
        else if (ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            errorInfo.Guidance = "Check the ConnectionStrings__DefaultConnection app setting includes the User Id parameter.";
        }

        return errorInfo;
    }
}
