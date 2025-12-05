using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null, string? searchTerm = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<List<Expense>> GetPendingExpensesAsync(string? searchTerm = null);
    Task<List<ExpenseCategory>> GetCategoriesAsync();
    Task<List<ExpenseStatus>> GetStatusesAsync();
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(int userId);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<List<ExpenseSummary>> GetExpenseSummaryAsync(int? userId = null);
    Task<List<CategorySummary>> GetExpensesByCategoryAsync(int? userId = null);
    (bool IsValid, string? ErrorMessage) ValidateExpense(CreateExpenseRequest request);
    Task<(bool IsValid, string? ErrorMessage)> ValidateHourlyLimitAsync(int userId, decimal amount);
}

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;
    private const decimal MaxExpenseAmount = 999m;
    private const decimal MaxTravelExpenseAmount = 99m;
    private const decimal HourlyLimit = 1500m;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    public (bool IsValid, string? ErrorMessage) ValidateExpense(CreateExpenseRequest request)
    {
        // Business rule: expenses cannot be larger than £999
        if (request.Amount > MaxExpenseAmount)
        {
            return (false, $"Expense amount cannot exceed £{MaxExpenseAmount:N2}");
        }

        // Business rule: travel expenses cannot be larger than £99
        // CategoryId 1 is Travel based on the database schema seed data
        if (request.CategoryId == 1 && request.Amount > MaxTravelExpenseAmount)
        {
            return (false, $"Travel expenses cannot exceed £{MaxTravelExpenseAmount:N2}");
        }

        return (true, null);
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateHourlyLimitAsync(int userId, decimal amount)
    {
        // Business rule: users cannot submit more than £1500 of total expenses within a 1 hour period
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT ISNULL(SUM(AmountMinor), 0) / 100.0
            FROM dbo.Expenses
            WHERE UserId = @UserId 
              AND CreatedAt >= @OneHourAgo";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@OneHourAgo", oneHourAgo);

        var result = await command.ExecuteScalarAsync();
        var totalInLastHour = result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;

        if (totalInLastHour + amount > HourlyLimit)
        {
            return (false, $"Cannot submit more than £{HourlyLimit:N2} of expenses within a 1 hour period. Current total in last hour: £{totalInLastHour:N2}");
        }

        return (true, null);
    }

    private static string ApplyBusinessRules(CreateExpenseRequest request, string? description)
    {
        var result = description ?? string.Empty;

        // Business rule: if description contains 'train' then it must be set as a travel expense
        // This is handled in the controller by changing the category

        // Business rule: if expense date falls on a weekend, add (WEEKEND) to description
        if (request.ExpenseDate.DayOfWeek == DayOfWeek.Saturday || 
            request.ExpenseDate.DayOfWeek == DayOfWeek.Sunday)
        {
            if (!result.Contains("(WEEKEND)"))
            {
                result = string.IsNullOrEmpty(result) ? "(WEEKEND)" : $"{result} (WEEKEND)";
            }
        }

        return result;
    }

    public async Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null, string? searchTerm = null)
    {
        var expenses = new List<Expense>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetExpenses", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        
        command.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@StatusId", statusId.HasValue ? statusId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CategoryId", categoryId.HasValue ? categoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? DBNull.Value : searchTerm);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            expenses.Add(MapExpenseFromReader(reader));
        }

        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetExpenseById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapExpenseFromReader(reader);
        }

        return null;
    }

    public async Task<List<Expense>> GetPendingExpensesAsync(string? searchTerm = null)
    {
        var expenses = new List<Expense>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetPendingExpenses", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? DBNull.Value : searchTerm);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            expenses.Add(new Expense
            {
                ExpenseId = reader.GetInt32("ExpenseId"),
                UserId = reader.GetInt32("UserId"),
                UserName = reader.GetString("UserName"),
                CategoryId = reader.GetInt32("CategoryId"),
                CategoryName = reader.GetString("CategoryName"),
                StatusId = reader.GetInt32("StatusId"),
                StatusName = reader.GetString("StatusName"),
                AmountMinor = reader.GetInt32("AmountMinor"),
                Currency = reader.GetString("Currency"),
                ExpenseDate = reader.GetDateTime("ExpenseDate"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                SubmittedAt = reader.IsDBNull("SubmittedAt") ? null : reader.GetDateTime("SubmittedAt")
            });
        }

        return expenses;
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        var categories = new List<ExpenseCategory>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetCategories", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new ExpenseCategory
            {
                CategoryId = reader.GetInt32("CategoryId"),
                CategoryName = reader.GetString("CategoryName"),
                IsActive = reader.GetBoolean("IsActive")
            });
        }

        return categories;
    }

    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        var statuses = new List<ExpenseStatus>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetStatuses", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            statuses.Add(new ExpenseStatus
            {
                StatusId = reader.GetInt32("StatusId"),
                StatusName = reader.GetString("StatusName")
            });
        }

        return statuses;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetUsers", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                UserId = reader.GetInt32("UserId"),
                UserName = reader.GetString("UserName"),
                Email = reader.GetString("Email"),
                RoleId = reader.GetInt32("RoleId"),
                RoleName = reader.GetString("RoleName"),
                ManagerId = reader.IsDBNull("ManagerId") ? null : reader.GetInt32("ManagerId"),
                IsActive = reader.GetBoolean("IsActive")
            });
        }

        return users;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetUserById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                UserId = reader.GetInt32("UserId"),
                UserName = reader.GetString("UserName"),
                Email = reader.GetString("Email"),
                RoleId = reader.GetInt32("RoleId"),
                RoleName = reader.GetString("RoleName"),
                ManagerId = reader.IsDBNull("ManagerId") ? null : reader.GetInt32("ManagerId"),
                IsActive = reader.GetBoolean("IsActive")
            };
        }

        return null;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        // Apply business rules
        var categoryId = request.CategoryId;
        
        // Business rule: if description contains 'train' then it must be set as a travel expense
        if (!string.IsNullOrEmpty(request.Description) && 
            request.Description.Contains("train", StringComparison.OrdinalIgnoreCase))
        {
            categoryId = 1; // Travel category
        }

        var description = ApplyBusinessRules(request, request.Description);
        var amountMinor = (int)(request.Amount * 100);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_CreateExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        
        command.Parameters.AddWithValue("@UserId", request.UserId);
        command.Parameters.AddWithValue("@CategoryId", categoryId);
        command.Parameters.AddWithValue("@AmountMinor", amountMinor);
        command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
        command.Parameters.AddWithValue("@Description", string.IsNullOrEmpty(description) ? DBNull.Value : description);
        command.Parameters.AddWithValue("@ReceiptFile", string.IsNullOrEmpty(request.ReceiptFile) ? DBNull.Value : request.ReceiptFile);
        
        var outputParam = new SqlParameter("@ExpenseId", SqlDbType.Int) { Direction = ParameterDirection.Output };
        command.Parameters.Add(outputParam);

        await command.ExecuteNonQueryAsync();
        
        return (int)outputParam.Value;
    }

    public async Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        var amountMinor = (int)(request.Amount * 100);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_UpdateExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        
        command.Parameters.AddWithValue("@ExpenseId", expenseId);
        command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@AmountMinor", amountMinor);
        command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
        command.Parameters.AddWithValue("@Description", string.IsNullOrEmpty(request.Description) ? DBNull.Value : request.Description);
        command.Parameters.AddWithValue("@ReceiptFile", string.IsNullOrEmpty(request.ReceiptFile) ? DBNull.Value : request.ReceiptFile);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32("RowsAffected") > 0;
        }
        return false;
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_SubmitExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32("RowsAffected") > 0;
        }
        return false;
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_ApproveExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);
        command.Parameters.AddWithValue("@ReviewerId", reviewerId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32("RowsAffected") > 0;
        }
        return false;
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_RejectExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);
        command.Parameters.AddWithValue("@ReviewerId", reviewerId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32("RowsAffected") > 0;
        }
        return false;
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_DeleteExpense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt32("RowsAffected") > 0;
        }
        return false;
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync(int? userId = null)
    {
        var summaries = new List<ExpenseSummary>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetExpenseSummary", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            summaries.Add(new ExpenseSummary
            {
                StatusName = reader.GetString("StatusName"),
                ExpenseCount = reader.GetInt32("ExpenseCount"),
                TotalAmountMinor = reader.IsDBNull("TotalAmountMinor") ? 0 : reader.GetInt32("TotalAmountMinor")
            });
        }

        return summaries;
    }

    public async Task<List<CategorySummary>> GetExpensesByCategoryAsync(int? userId = null)
    {
        var summaries = new List<CategorySummary>();
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("usp_GetExpensesByCategory", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            summaries.Add(new CategorySummary
            {
                CategoryName = reader.GetString("CategoryName"),
                ExpenseCount = reader.GetInt32("ExpenseCount"),
                TotalAmountMinor = reader.IsDBNull("TotalAmountMinor") ? 0 : reader.GetInt32("TotalAmountMinor")
            });
        }

        return summaries;
    }

    private static Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32("ExpenseId"),
            UserId = reader.GetInt32("UserId"),
            UserName = reader.GetString("UserName"),
            CategoryId = reader.GetInt32("CategoryId"),
            CategoryName = reader.GetString("CategoryName"),
            StatusId = reader.GetInt32("StatusId"),
            StatusName = reader.GetString("StatusName"),
            AmountMinor = reader.GetInt32("AmountMinor"),
            Currency = reader.GetString("Currency"),
            ExpenseDate = reader.GetDateTime("ExpenseDate"),
            Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
            ReceiptFile = reader.IsDBNull("ReceiptFile") ? null : reader.GetString("ReceiptFile"),
            SubmittedAt = reader.IsDBNull("SubmittedAt") ? null : reader.GetDateTime("SubmittedAt"),
            ReviewedBy = reader.IsDBNull("ReviewedBy") ? null : reader.GetInt32("ReviewedBy"),
            ReviewerName = reader.IsDBNull("ReviewerName") ? null : reader.GetString("ReviewerName"),
            ReviewedAt = reader.IsDBNull("ReviewedAt") ? null : reader.GetDateTime("ReviewedAt"),
            CreatedAt = reader.GetDateTime("CreatedAt")
        };
    }
}
