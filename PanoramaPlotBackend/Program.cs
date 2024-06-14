using DotNetEnv;
using Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Controller;
Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseMySql(
        Environment.GetEnvironmentVariable("CONNECTION_STRING"),
        new MySqlServerVersion(new Version(8, 0, 28))
    ));

builder.Services.AddAuthentication(x => {
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x => {
    x.TokenValidationParameters = new TokenValidationParameters{
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey
            (Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddAuthorization();


var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();



app.MapPost("/login", async context => {
    var configuration = builder.Configuration;
    var services = app.Services;
    await UserController.HandleLogin(context, configuration, services);
}).AllowAnonymous();


app.MapPost("/register", async context => {
    var configuration = builder.Configuration;
    var services = app.Services;
    await UserController.HandleRegistration(context, configuration, services);
}).AllowAnonymous();


app.MapGet("/movies/{page:int?}", MovieController.HandleMovies).AllowAnonymous();


app.MapGet("/movies/search/{query}/{page:int?}", MovieController.HandleMovieSearch).AllowAnonymous();


app.MapGet("/movies/favorite/{page:int?}", async (HttpContext context) => {
    await FavMovieController.HandleFavoriteMovies(context, app.Services);
}).RequireAuthorization();


app.MapPost("/movies/favorite", async (HttpContext context) => {
    await FavMovieController.HandleAddFavoriteMovie(context, app.Services);
}).RequireAuthorization();


app.MapGet("/movie/{id:int}", async context => {
    await MovieController.HandleGetMovieById(context);
});


//Test Endpoint
app.MapGet("/test", async context => {

    
    string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
    string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");

    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { message = $"{api_key}\n{api_token}"});
});


app.MapGet("/secret", async (HttpContext context) =>
{
    var user = context.User;

    var username = user.FindFirst(ClaimTypes.Name)!.Value;

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { User = username });
    return;
}).RequireAuthorization();


app.Run();

public partial class Program { }