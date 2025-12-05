using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class EditExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<EditExpenseModel> _logger;

    public Expense? Expense { get; set; }
    public List<ExpenseCategory> Categories { get; set; } = new();

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; }

    [BindProperty]
    public int CategoryId { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public string? ErrorMessage { get; set; }

    public EditExpenseModel(IExpenseService expenseService, ILogger<EditExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Expense = await _expenseService.GetExpenseByIdAsync(id);
            Categories = await _expenseService.GetCategoriesAsync();

            if (Expense == null)
            {
                return Page();
            }

            if (Expense.StatusName != "Draft")
            {
                return RedirectToPage("/Expenses");
            }

            Id = Expense.ExpenseId;
            Amount = Expense.AmountDisplay;
            ExpenseDate = Expense.ExpenseDate;
            CategoryId = Expense.CategoryId;
            Description = Expense.Description;

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expense {ExpenseId}", id);
            ViewData["ErrorInfo"] = new ErrorInfo
            {
                Message = "Failed to load expense",
                Guidance = "Check database connection."
            };
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Categories = await _expenseService.GetCategoriesAsync();
            Expense = await _expenseService.GetExpenseByIdAsync(Id);
        }
        catch
        {
            // Continue with validation
        }

        var request = new UpdateExpenseRequest
        {
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        // Validate business rules
        var createRequest = new CreateExpenseRequest
        {
            CategoryId = CategoryId,
            Amount = Amount
        };
        var validation = _expenseService.ValidateExpense(createRequest);
        if (!validation.IsValid)
        {
            ErrorMessage = validation.ErrorMessage;
            return Page();
        }

        try
        {
            await _expenseService.UpdateExpenseAsync(Id, request);
            return RedirectToPage("/Expenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", Id);
            ErrorMessage = "Failed to update expense: " + ex.Message;
            return Page();
        }
    }
}
