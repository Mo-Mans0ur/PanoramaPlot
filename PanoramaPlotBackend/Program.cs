using DotNetEnv;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.WithOrigins("http://localhost:3000")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});
var app = builder.Build();
Env.Load();

app.UseCors();



app.MapGet("/", () => "Hello World!");

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
        if (response.IsSuccessStatusCode)
        {
            // Read the JSON response as a string
            string jsonResponse = await response.Content.ReadAsStringAsync();

            // Deserialize JSON string into dynamic object using Newtonsoft.Json
            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResponse);

            JArray resultsArray = jsonObject.results;

            List<string> titles = new List<string>();

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

                titles.Add(movie.ToString());
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
            


            string jsonSerialized = JsonConvert.SerializeObject(new { data = titles, url_path = new {previous = UrlPrevious, next = UrlNext}});



            context.Response.Headers.Add("Content-Type", "application/json");
            await context.Response.WriteAsync(jsonSerialized);
        }
        else
        {
            // Write error message to the response stream
            await context.Response.WriteAsync("Failed to fetch data.");
        }
    }
});


app.Run();
