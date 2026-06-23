using CertificadosLaboralesV2.Components;
using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using CertificadosLaboralesV2.Services.Core;
using CertificadosLaboralesV2.Services.Documents;
using CertificadosLaboralesV2.Services.Email;
using CertificadosLaboralesV2.Services.Import;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ";
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".CertificadosLaboralesV2.Identity";
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/acceso-denegado";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.SlidingExpiration = true;
});

builder.Services.AddServerSideBlazor()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);

builder.Services.AddHttpClient();
builder.Services.AddControllers();

// Core services
builder.Services.AddScoped<EmpresaService>();
builder.Services.AddScoped<EmpleadoService>();
builder.Services.AddScoped<FirmanteService>();
builder.Services.AddScoped<PlantillaService>();
builder.Services.AddScoped<FuenteService>();
builder.Services.AddScoped<PlaceholderService>();
builder.Services.AddScoped<HistorialService>();

// Document services
builder.Services.AddScoped<ReplaceService>();
builder.Services.AddScoped<HtmlPdfService>();
builder.Services.AddScoped<HtmlDocxService>();
builder.Services.AddScoped<WordService>();
builder.Services.AddScoped<CreateDocService>();
builder.Services.AddScoped<QrCodeService>();

// Email service
builder.Services.AddScoped<EmailService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Import services
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<ExcelUploadService>();

// PDF native library (singleton — avoids re-initialization)
builder.Services.AddSingleton<IConverter>(new SynchronizedConverter(new PdfTools()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
    }
});
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
