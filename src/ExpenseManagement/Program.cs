using ExpenseManagement.Services;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Expense Management API", Version = "v1" });
});

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Try to register the real ExpenseService, fall back to dummy if connection fails
builder.Services.AddSingleton<IExpenseService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ExpenseService>>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        var dummyLogger = sp.GetRequiredService<ILogger<DummyExpenseService>>();
        dummyLogger.LogWarning("No connection string configured, using dummy data service");
        return new DummyExpenseService();
    }

    // Test the connection
    try
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        logger.LogInformation("Database connection successful");
        return new ExpenseService(configuration, logger);
    }
    catch (Exception ex)
    {
        var dummyLogger = sp.GetRequiredService<ILogger<DummyExpenseService>>();
        dummyLogger.LogWarning(ex, "Database connection failed, using dummy data service");
        return new DummyExpenseService();
    }
});

builder.Services.AddSingleton<IChatService, ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
