using System.Security.AccessControl;
using System.Text;
using API.Constants;
using API.Data;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using API.Middleware;
using API.Services;
using API.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(opt =>
{
   opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddCors();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<LogUserActivity>();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection(("CloudinarySettings")));

#region SignalR

builder.Services.AddSignalR();
builder.Services.AddSingleton<PresenceTracker>();

#endregion SignalR

builder.Services.AddIdentityCore<AppUser>(opt =>
{
   opt.Password.RequireNonAlphanumeric = false;
   opt.User.RequireUniqueEmail = true;
}).AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
   .AddJwtBearer(options =>
      {
         var tokenKey = builder.Configuration["TokenKey"] 
            ?? throw new Exception("Token key not found - Program.cs");
         options.TokenValidationParameters = new TokenValidationParameters
         {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateIssuer = false,
            ValidateAudience = false
         };

         // Following code block is used for SignalR to get access token and then pass it to context so that methods inside PresenceHub.cs can be called
         // because PresenceHub is decorated with [Authorize] attribute
         #region SignalR Access Token

         options.Events = new JwtBearerEvents
         {
            OnMessageReceived = context =>
            {
               var accessToken = context.Request.Query["access_token"];

               var path = context.HttpContext.Request.Path;
               if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
               {
                  context.Token = accessToken;
               }

               return Task.CompletedTask;
            }
         };

         #endregion SignalR Access Token
      });

builder.Services.AddAuthorizationBuilder()
   .AddPolicy(UserPolicyNames.RequireAdminRole, policy => policy.RequireRole("Admin"))
   .AddPolicy(UserPolicyNames.ModeratePhotoRole, policy => policy.RequireRole("Admin", "Moderator"));

var app = builder.Build();

// Configure the HTTP request pipeline.
// Middleware

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors(options => 
   options.AllowAnyHeader()
   .AllowAnyMethod()
   .AllowCredentials() //This line was added after adding RegreshToken. This allows to send and receive cookies from API server
   .WithOrigins("http://localhost:4200", "https://localhost:4200"));

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

#region SignalR

app.MapHub<PresenceHub>("hubs/presence");
app.MapHub<MessageHub>("hubs/messages");

#endregion SignalR

// Following code creates database with all migrations if DB is not already created or found. And then seeds the data - Start
// Because we cannot use injection pattern in Program.cs that's why we are using Service Locator pattern here, for AppDbContext and ILogger
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
   var context = services.GetRequiredService<AppDbContext>();
   var userManager = services.GetRequiredService<UserManager<AppUser>>();
   await context.Database.MigrateAsync(); // This creates DB with all migrations if DB is not already created or not found
   await context.Connections.ExecuteDeleteAsync(); // This will delete connections from DB table for the MessageHub
   await Seed.SeedUsers(userManager);
}
catch (Exception ex)
{
   var logger = services.GetRequiredService<ILogger<Program>>();
   logger.LogError(ex, "An error occured during migration in Program.cs");
}

// Above code creates database with all migrations if DB is not already created or found. And then seeds the data - End

app.Run();
