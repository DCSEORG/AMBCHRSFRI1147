using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveModel> _logger;

    public List<Expense> PendingExpenses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [TempData]
    public string? Message { get; set; }

    public ApproveModel(IExpenseService expenseService, ILogger<ApproveModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            PendingExpenses = await _expenseService.GetPendingExpensesAsync(SearchTerm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending expenses");
            ViewData["ErrorInfo"] = new ErrorInfo
            {
                Message = "Failed to load pending expenses",
                Guidance = "Check database connection and permissions."
            };
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        try
        {
            // Default reviewer is the manager (user 2)
            await _expenseService.ApproveExpenseAsync(id, 2);
            Message = $"Expense #{id} approved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            Message = $"Error approving expense: {ex.Message}";
        }
        
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        try
        {
            await _expenseService.RejectExpenseAsync(id, 2);
            Message = $"Expense #{id} rejected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", id);
            Message = $"Error rejecting expense: {ex.Message}";
        }
        
        return RedirectToPage();
    }
}
