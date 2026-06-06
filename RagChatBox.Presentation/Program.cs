using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RagChatBox.DAL;
using RagChatBox.BLL.Interfaces;
using RagChatBox.BLL.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure dynamic pgvector mapping before DbContext instantiation
AppDbContext.UsePgVector = builder.Configuration.GetValue<bool>("VectorDbSettings:UsePgVector");

// Add database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector()));

// Add HttpClient for Services
builder.Services.AddHttpClient();

// Register BLL Services
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ITextExtractorService, TextExtractorService>();
builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register Email Settings with validation on start
builder.Services.AddOptions<RagChatBox.BLL.Settings.EmailSettings>()
    .Bind(builder.Configuration.GetSection("EmailSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// Add MVC services
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Automatically apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Lỗi tự động cập nhật database: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication middleware must be registered before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
