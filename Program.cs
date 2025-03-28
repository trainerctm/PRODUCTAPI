using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ProductApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights; 
using Microsoft.ApplicationInsights.DataContracts;

var builder = WebApplication.CreateBuilder(args);


// CORS policy allowing your Blazor app (adjust the URL as needed)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins("https://courageblazorfeapp-fmghdhazf4h3dbb7.uksouth-01.azurewebsites.net")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register your DbContext
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register IHttpClientFactory (needed for calling GitHub endpoints)
builder.Services.AddHttpClient();

// Add Application Insights and Profiler
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddServiceProfiler();

// Configure JWT Bearer Authentication only
var jwtSecret = builder.Configuration["Jwt:Secret"];
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = key
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowBlazorClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
