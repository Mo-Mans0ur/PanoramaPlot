using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Models;

namespace Controller
{
    public static class MovieController
    {
        public static async Task HandleMovies(HttpContext context)
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

                    string UrlNext = "";
                    string UrlPrevious = "";
                    
                    if (int.Parse(RequestPage) >= 1 && int.Parse(RequestPage) < 500)
                    {
                        UrlNext = Environment.GetEnvironmentVariable("URL") + $"movies/{int.Parse(RequestPage) + 1}";
                    }
                    if (int.Parse(RequestPage) <= 500 && int.Parse(RequestPage) > 1)
                    {
                        UrlPrevious = Environment.GetEnvironmentVariable("URL") + $"movies/{int.Parse(RequestPage) - 1}";
                    }

                    string jsonSerialized = JsonConvert.SerializeObject(new { data = movies, url_path = new { previous = UrlPrevious, next = UrlNext } });

                    context.Response.Headers.Add("Content-Type", "application/json");
                    await context.Response.WriteAsync(jsonSerialized);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Failed to fetch data." });
                }
            }
        }
        public static async Task HandleMovieSearch(HttpContext context)
        {
            Console.WriteLine("Searching for movies");
            string RequestPage = context.Request.RouteValues["page"]?.ToString() ?? "1";
            string RequestQuery = context.Request.RouteValues["query"]?.ToString();
            string api_key = Environment.GetEnvironmentVariable("TMDB_KEY");
            string api_token = Environment.GetEnvironmentVariable("TMDB_READ_ACCESS_TOKEN");
            
            string RequestQueryEncoded = Uri.EscapeDataString(RequestQuery); // Needed to format the string to HexEscape for IMDB api
            
            if (string.IsNullOrEmpty(RequestQueryEncoded))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Query parameter is required.");
                return;
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

                    string UrlNext = "";
                    string UrlPrevious = "";

                    if (int.Parse(RequestPage) >= 1 && int.Parse(RequestPage) < 500)
                    {
                        UrlNext = Environment.GetEnvironmentVariable("URL") + $"movies/search/{RequestQuery}/{int.Parse(RequestPage) + 1}";
                    }
                    if (int.Parse(RequestPage) <= 500 && int.Parse(RequestPage) > 1)
                    {
                        UrlPrevious = Environment.GetEnvironmentVariable("URL") + $"movies/search/{RequestQuery}/{int.Parse(RequestPage) - 1}";
                    }

                    string jsonSerialized = JsonConvert.SerializeObject(new { data = movies, url_path = new { previous = UrlPrevious, next = UrlNext } });

                    context.Response.Headers.Add("Content-Type", "application/json");
                    await context.Response.WriteAsync(jsonSerialized);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Failed to fetch data." });
                }
            }
        }
        public static async Task HandleGetMovieById(HttpContext context)
        {
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
                else
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Failed to fetch movie data" });
                }
            }
        }
    }
    
}