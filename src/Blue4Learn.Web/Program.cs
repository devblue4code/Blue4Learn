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
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Blue4Learn.Session";
    options.IdleTimeout = TimeSpan.FromDays(14);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<ILearningContextService, LearningContextService>();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddTransient<DevelopmentEmailSender>();
builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddTransient<IEmailSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    return smtp.IsConfigured
        ? sp.GetRequiredService<SmtpEmailSender>()
        : sp.GetRequiredService<DevelopmentEmailSender>();
});

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

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Blue4Learn/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IGitHubCommitService, GitHubCommitService>();

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
