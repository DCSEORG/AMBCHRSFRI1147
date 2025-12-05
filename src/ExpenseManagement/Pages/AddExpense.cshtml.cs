using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public List<ExpenseCategory> Categories { get; set; } = new();

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public int CategoryId { get; set; } = 1;

    [BindProperty]
    public string? Description { get; set; }

    public string? ErrorMessage { get; set; }

    public AddExpenseModel(IExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Categories = await _expenseService.GetCategoriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading categories");
            ViewData["ErrorInfo"] = new ErrorInfo
            {
                Message = "Failed to load categories",
                Guidance = "Using default categories. Check database connection."
            };
            Categories = GetDefaultCategories();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Categories = await _expenseService.GetCategoriesAsync();
        }
        catch
        {
            Categories = GetDefaultCategories();
        }

        var request = new CreateExpenseRequest
        {
            UserId = 1, // Default to first user
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        // Validate business rules
        var validation = _expenseService.ValidateExpense(request);
        if (!validation.IsValid)
        {
            ErrorMessage = validation.ErrorMessage;
            return Page();
        }

        // Check hourly limit
        var hourlyValidation = await _expenseService.ValidateHourlyLimitAsync(request.UserId, Amount);
        if (!hourlyValidation.IsValid)
        {
            ErrorMessage = hourlyValidation.ErrorMessage;
            return Page();
        }

        try
        {
            await _expenseService.CreateExpenseAsync(request);
            return RedirectToPage("/Expenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = "Failed to create expense: " + ex.Message;
            return Page();
        }
    }

    private static List<ExpenseCategory> GetDefaultCategories()
    {
        return new List<ExpenseCategory>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }
}
