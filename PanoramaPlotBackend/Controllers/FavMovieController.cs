using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Models;
using Services;


namespace Controller
{
    public static class FavMovieController
    {
        public static async Task HandleFavoriteMovies(HttpContext context, IServiceProvider services)
        {
            var user = context.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = user.FindFirst(ClaimTypes.Name)?.Value;
            string requestPage = context.Request.RouteValues["page"]?.ToString() ?? "1";

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

                try
                {
                    var userFavorites = await dbContext.Favorites
                        .Where(f => f.UserId == int.Parse(userId))
                        .Include(f => f.Movie) // Assuming there is a navigation property named 'Movie' in the Favorite entity
                        .Skip((int.Parse(requestPage) - 1) * 20)
                        .Take(20)
                        .ToListAsync();

                    string urlNext = "";
                    string urlPrevious = "";

                    if (int.Parse(requestPage) >= 1 && int.Parse(requestPage) < 500)
                    {
                        urlNext = Environment.GetEnvironmentVariable("URL") + $"movies/favorite/{int.Parse(requestPage) + 1}";
                    }
                    if (int.Parse(requestPage) <= 500 && int.Parse(requestPage) > 1)
                    {
                        urlPrevious = Environment.GetEnvironmentVariable("URL") + $"movies/favorite/{int.Parse(requestPage) - 1}";
                    }

                    string jsonSerialized = JsonConvert.SerializeObject(new { data = userFavorites, url_path = new { previous = urlPrevious, next = urlNext } });

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
        }
        public static async Task HandleAddFavoriteMovie(HttpContext context, IServiceProvider services)
        {
            var user = context.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = user.FindFirst(ClaimTypes.Name)?.Value;
            var requestBody = await context.Request.ReadFromJsonAsync<Movie>();

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

                try
                {
                    var existingMovie = await dbContext.Movies.FirstOrDefaultAsync(u => u.Id == requestBody!.Id);
                    Console.WriteLine("Existing movie: " + existingMovie);
                    Console.WriteLine($"{userId} -> {requestBody.Id}");

                    if (existingMovie == null)
                    {
                        // Add movie to table if it doesn't exist
                        Console.WriteLine("Movie added to database");
                        Movie newMovie = new Movie
                        {
                            Adult = requestBody!.Adult,
                            BackdropPath = requestBody!.BackdropPath,
                            GenreIds = requestBody!.GenreIds,
                            Id = requestBody!.Id,
                            OriginalLanguage = requestBody!.OriginalLanguage,
                            OriginalTitle = requestBody!.OriginalTitle,
                            Overview = requestBody!.Overview,
                            Popularity = requestBody!.Popularity,
                            PosterPath = requestBody!.PosterPath,
                            ReleaseDate = requestBody!.ReleaseDate,
                            Title = requestBody!.Title,
                            VoteAverage = requestBody!.VoteAverage,
                            VoteCount = requestBody!.VoteCount
                        };

                        Console.WriteLine(newMovie);
                        dbContext.Movies.Add(newMovie);
                    }

                    // Add the movie id to the user's favorites
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
        }
    }
}