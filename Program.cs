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

// Use PostgreSQL if connection string contains "Host=", otherwise SQLite
if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[INFO] Using PostgreSQL database provider");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
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
    
    // Only migrate if using a relational database (not InMemory for tests)
    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
    {
        if (db.Database.IsRelational())
        {
            Console.WriteLine("[INFO] Initializing database...");
            
            // Ensure database is created
            db.Database.EnsureCreated();
            
            // Run custom SQL migrations (only for SQLite - PostgreSQL uses EF migrations)
            if (db.Database.ProviderName?.Contains("Sqlite") == true)
            {
                var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrEmpty(sqliteConnectionString))
                {
                    try
                    {
                        MigrationRunner.RunMigrations(sqliteConnectionString);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error running SQLite migrations: {ex.Message}");
                        throw;
                    }
                }
            }
            else if (db.Database.ProviderName?.Contains("Npgsql") == true)
            {
                // For PostgreSQL, use EF Core migrations
                try
                {
                    Console.WriteLine("[INFO] Applying PostgreSQL migrations...");
                    db.Database.Migrate();
                    Console.WriteLine("[SUCCESS] PostgreSQL migrations applied.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error applying PostgreSQL migrations: {ex.Message}");
                    throw;
                }
            }
            
            // ✅ FIXED: Initialize seed data
            try
            {
                DbInitializer.Initialize(db);
                Console.WriteLine("[SUCCESS] Database initialization completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error initializing seed data: {ex.Message}");
                throw;
            }
        }
    }
    else
    {
        // For InMemory, just ensure the database is created
        db.Database.EnsureCreated();
    }
}

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
