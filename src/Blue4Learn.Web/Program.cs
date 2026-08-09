using Blue4Learn.Web.Data;
using Blue4Learn.Web.Data.Seed;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.Services.Ai;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddTransient<IEmailSender, DevelopmentEmailSender>();

builder.Services.Configure<AiTutorOptions>(builder.Configuration.GetSection(AiTutorOptions.SectionName));
builder.Services.AddSingleton<HeuristicAiTutorService>();
builder.Services.AddHttpClient("AiTutor", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<AiTutorOptions>>().Value;
    var endpoint = string.IsNullOrWhiteSpace(opts.Endpoint)
        ? "https://api.openai.com/v1"
        : opts.Endpoint.TrimEnd('/');
    client.BaseAddress = new Uri(endpoint + "/");
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<IAiTutorService, OpenAiTutorService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Em container (Docker) exponemos só HTTP na porta 8080.
var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
if (!runningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
