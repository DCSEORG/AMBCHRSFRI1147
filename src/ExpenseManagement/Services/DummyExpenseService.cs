using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public class DummyExpenseService : IExpenseService
{
    private static readonly List<ExpenseCategory> _categories = new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    private static readonly List<ExpenseStatus> _statuses = new()
    {
        new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
    };

    private static readonly List<User> _users = new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
        new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
    };

    private static readonly List<Expense> _expenses = new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-10), Description = "Taxi from airport to client site", SubmittedAt = DateTime.UtcNow.AddDays(-9), CreatedAt = DateTime.UtcNow.AddDays(-10) },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-30), Description = "Client lunch meeting", SubmittedAt = DateTime.UtcNow.AddDays(-29), ReviewedBy = 2, ReviewerName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-28), CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-5), Description = "Office stationery", CreatedAt = DateTime.UtcNow.AddDays(-5) },
        new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-60), Description = "Hotel during client visit", SubmittedAt = DateTime.UtcNow.AddDays(-59), ReviewedBy = 2, ReviewerName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-58), CreatedAt = DateTime.UtcNow.AddDays(-60) }
    };

    private int _nextExpenseId = 5;

    public Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null, string? searchTerm = null)
    {
        var result = _expenses.AsEnumerable();
        
        if (userId.HasValue)
            result = result.Where(e => e.UserId == userId.Value);
        if (statusId.HasValue)
            result = result.Where(e => e.StatusId == statusId.Value);
        if (categoryId.HasValue)
            result = result.Where(e => e.CategoryId == categoryId.Value);
        if (!string.IsNullOrEmpty(searchTerm))
            result = result.Where(e => e.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true);

        return Task.FromResult(result.OrderByDescending(e => e.ExpenseDate).ToList());
    }

    public Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        return Task.FromResult(_expenses.FirstOrDefault(e => e.ExpenseId == expenseId));
    }

    public Task<List<Expense>> GetPendingExpensesAsync(string? searchTerm = null)
    {
        var result = _expenses.Where(e => e.StatusName == "Submitted");
        
        if (!string.IsNullOrEmpty(searchTerm))
            result = result.Where(e => e.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true || 
                                       e.CategoryName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(result.OrderBy(e => e.SubmittedAt).ToList());
    }

    public Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        return Task.FromResult(_categories.ToList());
    }

    public Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        return Task.FromResult(_statuses.ToList());
    }

    public Task<List<User>> GetUsersAsync()
    {
        return Task.FromResult(_users.ToList());
    }

    public Task<User?> GetUserByIdAsync(int userId)
    {
        return Task.FromResult(_users.FirstOrDefault(u => u.UserId == userId));
    }

    public Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        var category = _categories.FirstOrDefault(c => c.CategoryId == request.CategoryId);
        var user = _users.FirstOrDefault(u => u.UserId == request.UserId);

        var expense = new Expense
        {
            ExpenseId = _nextExpenseId++,
            UserId = request.UserId,
            UserName = user?.UserName ?? "Unknown",
            CategoryId = request.CategoryId,
            CategoryName = category?.CategoryName ?? "Unknown",
            StatusId = 1,
            StatusName = "Draft",
            AmountMinor = (int)(request.Amount * 100),
            Currency = "GBP",
            ExpenseDate = request.ExpenseDate,
            Description = request.Description,
            ReceiptFile = request.ReceiptFile,
            CreatedAt = DateTime.UtcNow
        };

        _expenses.Add(expense);
        return Task.FromResult(expense.ExpenseId);
    }

    public Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        var expense = _expenses.FirstOrDefault(e => e.ExpenseId == expenseId);
        if (expense == null) return Task.FromResult(false);

        var category = _categories.FirstOrDefault(c => c.CategoryId == request.CategoryId);
        
        expense.CategoryId = request.CategoryId;
        expense.CategoryName = category?.CategoryName ?? expense.CategoryName;
        expense.AmountMinor = (int)(request.Amount * 100);
        expense.ExpenseDate = request.ExpenseDate;
        expense.Description = request.Description;
        expense.ReceiptFile = request.ReceiptFile;

        return Task.FromResult(true);
    }

    public Task<bool> SubmitExpenseAsync(int expenseId)
    {
        var expense = _expenses.FirstOrDefault(e => e.ExpenseId == expenseId);
        if (expense == null) return Task.FromResult(false);

        expense.StatusId = 2;
        expense.StatusName = "Submitted";
        expense.SubmittedAt = DateTime.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        var expense = _expenses.FirstOrDefault(e => e.ExpenseId == expenseId);
        if (expense == null) return Task.FromResult(false);

        var reviewer = _users.FirstOrDefault(u => u.UserId == reviewerId);

        expense.StatusId = 3;
        expense.StatusName = "Approved";
        expense.ReviewedBy = reviewerId;
        expense.ReviewerName = reviewer?.UserName;
        expense.ReviewedAt = DateTime.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        var expense = _expenses.FirstOrDefault(e => e.ExpenseId == expenseId);
        if (expense == null) return Task.FromResult(false);

        var reviewer = _users.FirstOrDefault(u => u.UserId == reviewerId);

        expense.StatusId = 4;
        expense.StatusName = "Rejected";
        expense.ReviewedBy = reviewerId;
        expense.ReviewerName = reviewer?.UserName;
        expense.ReviewedAt = DateTime.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> DeleteExpenseAsync(int expenseId)
    {
        var expense = _expenses.FirstOrDefault(e => e.ExpenseId == expenseId && e.StatusName == "Draft");
        if (expense == null) return Task.FromResult(false);

        _expenses.Remove(expense);
        return Task.FromResult(true);
    }

    public Task<List<ExpenseSummary>> GetExpenseSummaryAsync(int? userId = null)
    {
        var query = userId.HasValue ? _expenses.Where(e => e.UserId == userId.Value) : _expenses;
        
        var summaries = query
            .GroupBy(e => e.StatusName)
            .Select(g => new ExpenseSummary
            {
                StatusName = g.Key,
                ExpenseCount = g.Count(),
                TotalAmountMinor = g.Sum(e => e.AmountMinor)
            })
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<List<CategorySummary>> GetExpensesByCategoryAsync(int? userId = null)
    {
        var query = userId.HasValue ? _expenses.Where(e => e.UserId == userId.Value) : _expenses;
        
        var summaries = query
            .GroupBy(e => e.CategoryName)
            .Select(g => new CategorySummary
            {
                CategoryName = g.Key,
                ExpenseCount = g.Count(),
                TotalAmountMinor = g.Sum(e => e.AmountMinor)
            })
            .OrderByDescending(s => s.TotalAmountMinor)
            .ToList();

        return Task.FromResult(summaries);
    }

    public (bool IsValid, string? ErrorMessage) ValidateExpense(CreateExpenseRequest request)
    {
        if (request.Amount > 999m)
            return (false, "Expense amount cannot exceed £999.00");

        if (request.CategoryId == 1 && request.Amount > 99m)
            return (false, "Travel expenses cannot exceed £99.00");

        return (true, null);
    }

    public Task<(bool IsValid, string? ErrorMessage)> ValidateHourlyLimitAsync(int userId, decimal amount)
    {
        // For dummy data, always allow
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
