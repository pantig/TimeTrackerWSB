using Microsoft.EntityFrameworkCore;
using TimeTrackerApp.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using TimeTrackerApp.Services;
using TimeTrackerApp.Migrations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database - detect provider from connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

Console.WriteLine($"[INFO] Connection string: {connectionString.Replace(connectionString.Split("Password=")[1].Split(";")[0], "***")}");

// Use PostgreSQL if connection string contains "Host=", otherwise SQLite
if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[INFO] Using PostgreSQL database provider");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString)
               .EnableSensitiveDataLogging()
               .LogTo(Console.WriteLine, LogLevel.Information));
}
else
{
    Console.WriteLine("[INFO] Using SQLite database provider");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Services
builder.Services.AddScoped<ITimeEntryService, TimeEntryService>();
builder.Services.AddScoped<ExcelExportService>();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ FIXED: Redirect root to login page
app.MapGet("/", context =>
{
    context.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
});

// ✅ FIXED: Routing z domyślną akcją Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Index}/{id?}");

// ✅ FIXED: Apply migrations + Seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Only migrate if using a relational database (not InMemory for tests)
    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
    {
        if (db.Database.IsRelational())
        {
            logger.LogInformation("[INFO] Initializing database...");
            logger.LogInformation($"[INFO] Database provider: {db.Database.ProviderName}");
            
            try
            {
                // Test connection
                logger.LogInformation("[INFO] Testing database connection...");
                var canConnect = await db.Database.CanConnectAsync();
                logger.LogInformation($"[INFO] Can connect to database: {canConnect}");
                
                if (!canConnect)
                {
                    logger.LogError("[ERROR] Cannot connect to database!");
                    throw new Exception("Cannot connect to database");
                }
                
                // Ensure database is created
                logger.LogInformation("[INFO] Ensuring database exists...");
                db.Database.EnsureCreated();
                logger.LogInformation("[INFO] Database exists.");
                
                // Run custom SQL migrations (only for SQLite - PostgreSQL uses EF migrations)
                if (db.Database.ProviderName?.Contains("Sqlite") == true)
                {
                    var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                    if (!string.IsNullOrEmpty(sqliteConnectionString))
                    {
                        try
                        {
                            logger.LogInformation("[INFO] Running SQLite migrations...");
                            MigrationRunner.RunMigrations(sqliteConnectionString);
                            logger.LogInformation("[INFO] SQLite migrations completed.");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[ERROR] Error running SQLite migrations");
                            throw;
                        }
                    }
                }
                else if (db.Database.ProviderName?.Contains("Npgsql") == true)
                {
                    // For PostgreSQL, use EF Core migrations
                    try
                    {
                        logger.LogInformation("[INFO] Applying PostgreSQL migrations...");
                        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                        logger.LogInformation($"[INFO] Pending migrations: {string.Join(", ", pendingMigrations)}");
                        
                        await db.Database.MigrateAsync();
                        logger.LogInformation("[SUCCESS] PostgreSQL migrations applied.");
                        
                        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                        logger.LogInformation($"[INFO] Applied migrations: {string.Join(", ", appliedMigrations)}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[ERROR] Error applying PostgreSQL migrations");
                        throw;
                    }
                }
                
                // ✅ FIXED: Initialize seed data
                try
                {
                    logger.LogInformation("[INFO] Initializing seed data...");
                    
                    // Check if data already exists
                    var userCount = db.Users.Count();
                    logger.LogInformation($"[INFO] Current users in database: {userCount}");
                    
                    if (userCount == 0)
                    {
                        logger.LogInformation("[INFO] No users found. Creating seed data...");
                        DbInitializer.Initialize(db);
                        logger.LogInformation("[SUCCESS] Seed data created.");
                    }
                    else
                    {
                        logger.LogInformation($"[INFO] Seed data already exists ({userCount} users).");
                    }
                    
                    // Verify seed data
                    logger.LogInformation("[INFO] Verifying database contents:");
                    logger.LogInformation($"  - Users: {db.Users.Count()}");
                    logger.LogInformation($"  - Employees: {db.Employees.Count()}");
                    logger.LogInformation($"  - Projects: {db.Projects.Count()}");
                    logger.LogInformation($"  - Time Entries: {db.TimeEntries.Count()}");
                    
                    logger.LogInformation("[SUCCESS] Database initialization completed.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[ERROR] Error initializing seed data");
                    throw;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ERROR] Fatal error during database initialization");
                throw;
            }
        }
    }
    else
    {
        // For InMemory, just ensure the database is created
        logger.LogInformation("[INFO] Using InMemory database - skipping migrations");
        db.Database.EnsureCreated();
    }
}

Console.WriteLine("[INFO] Application starting...");
app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
