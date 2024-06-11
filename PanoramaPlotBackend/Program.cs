using DotNetEnv;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Models;
using Services;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
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
    var requestBody = await context.Request.ReadFromJsonAsync<User>();
    string username = requestBody.Username;
    string password = requestBody.Password;

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user != null)
            {
                bool passwordMatch = BCrypt.Net.BCrypt.Verify(password, user.Password);
                if (passwordMatch)
                {
                    // Generate JWT token
                    var token = GenerateJwtToken(user, builder.Configuration);
                    
                    // Return token
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { Token = token }));
                    return;
                }
            }
            
            // User not found or password does not match
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Invalid username or password"});
        }
        catch (Exception ex)
        {
            // Handle database errors
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to connect to database" });
        }
    }
}).AllowAnonymous();

app.MapPost("/register", async context => {
    var requestBody = await context.Request.ReadFromJsonAsync<User>();
    string username = requestBody.Username;
    string password = BCrypt.Net.BCrypt.HashPassword(requestBody.Password, BCrypt.Net.BCrypt.GenerateSalt());
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        try
        {
            var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (existingUser != null)
            {
                context.Response.StatusCode = 409; // Conflict
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Username already taken" });                return;
            }

            var user = new User
            {
                Username = username,
                Password = password
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "User registered successfully" });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to register user: " + ex.Message });
        }

    }
}).AllowAnonymous();

app.MapGet("/secret", async (HttpContext context) =>
{
    var user = context.User;

    var username = user.FindFirst(ClaimTypes.Name)!.Value;

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { User = username });
    return;
}).RequireAuthorization();

app.MapGet("/movies/{page:int?}", async context =>
{
    string RequestPage = context.Request.RouteValues["page"]?.ToString() ?? "1";

    string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
    string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");
    Console.WriteLine("Key: " + api_token);

    string apiURL = $"https://api.themoviedb.org/3/discover/movie?include_adult=false&include_video=false&language=en-US&page={RequestPage}&sort_by=popularity.desc";

    Console.WriteLine("URL: " + apiURL);
    using (HttpClient client = new HttpClient())
    {
        client.DefaultRequestHeaders.Add("accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + api_token);
        HttpResponseMessage response = await client.GetAsync(apiURL);
        Console.WriteLine("Response: " + response);
        if (response.IsSuccessStatusCode)
        {
            // Read the JSON response as a string
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Deserialize JSON string into dynamic object using Newtonsoft.Json
            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResponse);

            JArray resultsArray = jsonObject.results;

            List<Movie> movies = new List<Movie>();

            foreach (JToken result in resultsArray)
            {
                Movie movie = new Movie(
                    result["adult"]?.ToString(), 
                    result["backdrop_path"]?.ToString(), 
                    result["genre_ids"]?.ToString(), 
                    result["id"]?.ToString(),
                    result["original_language"]?.ToString(), 
                    result["original_title"]?.ToString(),
                    result["overview"]?.ToString(), 
                    result["popularity"]?.ToString(), 
                    result["poster_path"]?.ToString(), 
                    result["release_date"]?.ToString(),
                    result["title"]?.ToString(), 
                    result["vote_average"]?.ToString(), 
                    result["vote_count"]?.ToString()
                );

                movies.Add(movie);
            }
            //------------------------------------------Above to get the data and manipulate it. Under to create the url for getting new data
            string UrlNext = "";
            string UrlPrevious = "";
            
            if(int.Parse(RequestPage) >= 1 && int.Parse(RequestPage) < 500){
                UrlNext = Environment.GetEnvironmentVariable("URL")+$"movies/{int.Parse(RequestPage)+1}";
            }
            if(int.Parse(RequestPage) <= 500 && int.Parse(RequestPage) > 1){
                UrlPrevious = Environment.GetEnvironmentVariable("URL")+$"movies/{int.Parse(RequestPage)-1}";
            }
            


            string jsonSerialized = JsonConvert.SerializeObject(new { data = movies, url_path = new { previous = UrlPrevious, next = UrlNext }});

            context.Response.Headers.Add("Content-Type", "application/json");
            await context.Response.WriteAsync(jsonSerialized);
        }
        else
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to fetch data."});
        }
    }
});

app.MapGet("/movies/search/{query}/{page:int?}", async context => 
{
    string RequestPage = context.Request.RouteValues["page"]?.ToString() ?? "1";
    string RequestQuery = context.Request.RouteValues["query"]?.ToString();
    string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
    string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");
    
    string RequestQueryEncoded = Uri.EscapeDataString(RequestQuery); //Needed to format the string to HexEscape for IMDB api
    
    if( string.IsNullOrEmpty(RequestQueryEncoded) ){
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Query parameter is required.");
    }
    string apiURL = $"https://api.themoviedb.org/3/search/movie?query={RequestQueryEncoded}&include_adult=false&language=en-US&page={RequestPage}";
    
    using (HttpClient client = new HttpClient())
    {
        client.DefaultRequestHeaders.Add("accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + api_token);
        HttpResponseMessage response = await client.GetAsync(apiURL);
        Console.WriteLine("Response: " + response);
        if (response.IsSuccessStatusCode)
        {
            // Read the JSON response as a string
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Deserialize JSON string into dynamic object using Newtonsoft.Json
            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResponse);

            JArray resultsArray = jsonObject.results;

            List<Movie> movies = new List<Movie>();

            foreach (JToken result in resultsArray)
            {
                string backdropPath = string.IsNullOrEmpty(result["backdrop_path"]?.ToString()) ? "defaultBackdropPath" : result["backdrop_path"].ToString();
                Movie movie = new Movie(
                    result["adult"]?.ToString(), 
                    backdropPath, 
                    result["genre_ids"]?.ToString(), 
                    result["id"]?.ToString(),
                    result["original_language"]?.ToString(), 
                    result["original_title"]?.ToString(),
                    result["overview"]?.ToString(), 
                    result["popularity"]?.ToString(), 
                    result["poster_path"]?.ToString(), 
                    result["release_date"]?.ToString(),
                    result["title"]?.ToString(), 
                    result["vote_average"]?.ToString(), 
                    result["vote_count"]?.ToString()
                );

                movies.Add(movie);
            }
            //------------------------------------------Above to get the data and manipulate it. Under to create the url for getting new data
            string UrlNext = "";
            string UrlPrevious = "";
            
            if(int.Parse(RequestPage) >= 1 && int.Parse(RequestPage) < 500){
                UrlNext = Environment.GetEnvironmentVariable("URL")+$"movies/search/{RequestQuery}/{int.Parse(RequestPage)+1}";
            }
            if(int.Parse(RequestPage) <= 500 && int.Parse(RequestPage) > 1){
                UrlPrevious = Environment.GetEnvironmentVariable("URL")+$"movies/search/{RequestQuery}/{int.Parse(RequestPage)-1}";
            }
            


            string jsonSerialized = JsonConvert.SerializeObject(new { data = movies, url_path = new { previous = UrlPrevious, next = UrlNext }});

            context.Response.Headers.Add("Content-Type", "application/json");
            await context.Response.WriteAsync(jsonSerialized);
        }
        else
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to fetch data."});
        }
    }

});

app.MapGet("/movies/favorite/{page:int?}", async (HttpContext context) => {
    //Get users movies from the id's, look at foreign key unions
    var user = context.User;
    var userId= user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
    var username = user.FindFirst(ClaimTypes.Name)!.Value;
    string RequestPage = context.Request.RouteValues["page"]?.ToString() ?? "1";

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        try
        {
            var userFavorites = await dbContext.Favorites
                .Where(f => f.UserId == int.Parse(userId))
                .Include(f => f.Movie) // Assuming there is a navigation property named 'Movie' in the Favorite entity
                .Skip((int.Parse(RequestPage) - 1) * 20)
                .Take(20)
                .ToListAsync();


            string UrlNext = "";
            string UrlPrevious = "";
            
            if(int.Parse(RequestPage) >= 1 && int.Parse(RequestPage) < 500){
                UrlNext = Environment.GetEnvironmentVariable("URL")+$"movies/search/{int.Parse(RequestPage)+1}";
            }
            if(int.Parse(RequestPage) <= 500 && int.Parse(RequestPage) > 1){
                UrlPrevious = Environment.GetEnvironmentVariable("URL")+$"movies/search/{int.Parse(RequestPage)-1}";
            }

            string jsonSerialized = JsonConvert.SerializeObject(new { data = userFavorites, url_path = new { previous = UrlPrevious, next = UrlNext }});

            context.Response.Headers.Add("Content-Type", "application/json");
            await context.Response.WriteAsync(jsonSerialized);
        }
        catch (Exception ex)
        {
            // Handle database errors
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to connect to database" });
        }
    }


});

app.MapPost("/movies/favorite", async (HttpContext context) => {
    //Create the movie in the movie table if it dosen't exist then add it's id to the users favorites.
    var user = context.User;
    var userId= user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
    var username = user.FindFirst(ClaimTypes.Name)!.Value;
    var requestBody = await context.Request.ReadFromJsonAsync<Movie>();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

        try
        {
            var existingMovie = await dbContext.Movies.FirstOrDefaultAsync(u => u.Id == requestBody!.Id);
            Console.WriteLine("Existing movie: "+existingMovie);
            Console.WriteLine($"{userId} -> {requestBody.Id}");

            if (existingMovie == null)
            {
                //add movie to table if it dosen't exist
                Console.WriteLine("Movie added to database");
                Movie newMovie = new Movie();
                newMovie.Adult = requestBody!.Adult;
                newMovie.BackdropPath = requestBody!.BackdropPath;
                newMovie.GenreIds = requestBody!.GenreIds;
                newMovie.Id = requestBody!.Id;
                newMovie.OriginalLanguage = requestBody!.OriginalLanguage;
                newMovie.OriginalTitle = requestBody!.OriginalTitle;
                newMovie.Overview = requestBody!.Overview;
                newMovie.Popularity = requestBody!.Popularity;
                newMovie.PosterPath = requestBody!.PosterPath;
                newMovie.ReleaseDate = requestBody!.ReleaseDate;
                newMovie.Title = requestBody!.Title;
                newMovie.VoteAverage = requestBody!.VoteAverage;
                newMovie.VoteCount = requestBody!.VoteCount;


                Console.WriteLine(newMovie);
                dbContext.Movies.Add(newMovie);
            }
            //add the movie id to the 
            Console.WriteLine("Movie added to users favorites");
            
            var newFavorite = new Favorite(int.Parse(userId), (int)requestBody.Id);
            Console.WriteLine(newFavorite);
            dbContext.Favorites.Add(newFavorite);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Handle database errors
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Failed to connect to database" });
        }
    }

    //Console.WriteLine(requestBody!.ToStringDefault());


});

app.MapGet("/test", async context => {

    
    string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
    string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");

    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { message = $"{api_key}\n{api_token}"});
});

app.MapGet("/movie/{id:int}", async context => {
    string RequestId = context.Request.RouteValues["id"]?.ToString();
    string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
    string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");

    string apiURL = $"https://api.themoviedb.org/3/movie/{RequestId}?language=en-US";

    Console.WriteLine("URL: " + apiURL);
    using (HttpClient client = new HttpClient())
    {
        client.DefaultRequestHeaders.Add("accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + api_token);
        HttpResponseMessage response = await client.GetAsync(apiURL);
        Console.WriteLine("Response: " + response);
        if (response.IsSuccessStatusCode)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            context.Response.Headers.Add("Content-Type", "application/json");
            await context.Response.WriteAsync(jsonResponse);
        }
    }
});

app.Run();

string GenerateJwtToken(User user, IConfiguration configuration)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!);

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        Issuer = configuration["Jwt:Issuer"],
        Audience = configuration["Jwt:Audience"],
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}