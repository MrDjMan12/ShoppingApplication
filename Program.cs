using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using ShoppingApplication.Data;
using System.Security.Cryptography;
using System.Text;
using ShoppingApplication.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//Session Based Authentication: MiddleWare to add Services for Session
//builder.Services.AddDistributedMemoryCache(); // Required for session
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
//    options.Cookie.HttpOnly = true; // Prevent access via JavaScript
//    options.Cookie.IsEssential = true; // Required for GDPR compliance
//});

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();


builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

//Add configuration sources
builder.Configuration.AddUserSecrets<Program>();

var jwt = builder.Configuration.GetSection("Jwt");
var key = jwt["Key"];

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//Used for the Weather API
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Weather Forecast API",
            Version = "1.0.0",
            Description = "This api contains all endpoints for weather forcast"
        };
        document.Info.Contact = new()
        {
            Email = "test@gmail.com",
            Name = "Testing-Name",
            Url = new Uri("https://google.com.au")
        };
        return Task.CompletedTask;
    });
});

string scheme = JwtBearerDefaults.AuthenticationScheme;
builder.Services.AddAuthentication(options =>
{ 
    options.DefaultAuthenticateScheme = scheme;
    options.DefaultChallengeScheme = scheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateAudience = true,
        ValidateIssuer = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };

    //Cookie Based Authentication: Retrieves the token within the cookie instead of the authentication header
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Request.Cookies.TryGetValue("JwtToken", out var token))
            {
                context.Token = token; // Read the token from the cookie
            }
            return Task.CompletedTask;
        }
    };

});

//Role-Based Policy Configuration
//builder.Services.AddAuthorizationBuilder()
//    .AddPolicy("SuperAdminOnly", policy => policy.RequireClaim("SuperAdmin"));

//Claim-Based Policy Configuration
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SuperAdminOnly", policy => policy.RequireClaim("Role", "SuperAdmin"));

var app = builder.Build();

// Apply any pending migrations and create the database schema at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate(); // This automatically applies migrations and creates the DB schema
    
    //Seed Roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetService<UserManager<IdentityUser>>();

    string[] roles = { "SuperAdmin", "Admin", "User"};

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    //Assign a user to the SuperAdmin role
    var user = await userManager.FindByEmailAsync("Email");
    if (user != null && !await userManager.IsInRoleAsync(user, "SuperAdmin"))
    {
        await userManager.AddToRoleAsync(user, "SuperAdmin");
    }

}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

//app.UseSession();

app.MapScalarApiReference();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

//Use for the Weather Api
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authschemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authschemes.Any(authSchemes => authSchemes.Name == JwtBearerDefaults.AuthenticationScheme))
        {
            var requirements = new Dictionary<string, OpenApiSecurityScheme>
            {
                [JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme.ToLower(),
                    //Determines where the Auth token is stored (Can choose Cookie Or Header)
                    In = ParameterLocation.Header,
                    BearerFormat = "Json Web Token"
                }
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = requirements;
        }
    }
}