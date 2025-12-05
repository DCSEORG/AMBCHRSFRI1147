using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesModel> _logger;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    public ExpensesModel(IExpenseService expenseService, ILogger<ExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Categories = await _expenseService.GetCategoriesAsync();
            Statuses = await _expenseService.GetStatusesAsync();
            Expenses = await _expenseService.GetExpensesAsync(
                statusId: StatusId,
                categoryId: CategoryId,
                searchTerm: SearchTerm
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            ViewData["ErrorInfo"] = new ErrorInfo
            {
                Message = "Failed to load expenses: " + ex.Message,
                Guidance = "Check database connection and permissions."
            };
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", id);
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return RedirectToPage();
        }
    }
}
