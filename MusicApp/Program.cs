
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MusicApp.Components;
using MusicApp.Data;
using MusicApp.Interfaces;
using MusicApp.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MusicApp.Client.Interfaces;
using MusicApp.Client.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtIssuer"],
        ValidAudience = builder.Configuration["JwtAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSecurityKey"]!)),
        ClockSkew = TimeSpan.FromMinutes(5) // Захист від розсинхронізації часу ngrok
    };

    // Логування помилок автентифікації в консоль бекенду
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT AUTH FAILED: {context.Exception.Message}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpClient();
builder.Services.AddScoped<ISpotifyService, SpotifyService>();
builder.Services.AddScoped<IMp3MetadataService, Mp3MetadataService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MusicApp.Client._Imports).Assembly);

app.MapControllers();

app.Run();
 
