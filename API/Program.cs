using System.Text;
using API.Constants;
using API.Data;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using API.Middleware;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<ILikesRepository, LikesRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<LogUserActivity>();
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection(("CloudinarySettings")));

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

// Following code creates database with all migrations if DB is not already created or found. And then seeds the data - Start
// Because we cannot use injection pattern in Program.cs that's why we are using Service Locator pattern here, for AppDbContext and ILogger
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
   var context = services.GetRequiredService<AppDbContext>();
   var userManager = services.GetRequiredService<UserManager<AppUser>>();
   await context.Database.MigrateAsync(); // This creates DB with all migrations if DB is not already created or not found
   await Seed.SeedUsers(userManager);
}
catch (Exception ex)
{
   var logger = services.GetRequiredService<ILogger<Program>>();
   logger.LogError(ex, "An error occured during migration in Program.cs");
}

// Above code creates database with all migrations if DB is not already created or found. And then seeds the data - End

app.Run();
