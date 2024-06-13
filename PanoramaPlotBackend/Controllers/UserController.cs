using Models;
using Microsoft.EntityFrameworkCore;
using Services;
using DotNetEnv;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Controller
{
    public static class UserController
    {
        public static async Task HandleLogin(HttpContext context, IConfiguration configuration, IServiceProvider services)
        {
            var requestBody = await context.Request.ReadFromJsonAsync<User>();
            string username = requestBody.Username;
            string password = requestBody.Password;

            using (var scope = services.CreateScope())
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
                            var token = JwtService.GenerateJwtToken(user, configuration);

                            // Return token
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { Token = token }));
                            return;
                        }
                    }

                    // User not found or password does not match
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { message = "Invalid username or password" });
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
        public static async Task HandleRegistration(HttpContext context, IConfiguration configuration, IServiceProvider services)
        {
            var requestBody = await context.Request.ReadFromJsonAsync<User>();
            string username = requestBody.Username;
            string password = BCrypt.Net.BCrypt.HashPassword(requestBody.Password, BCrypt.Net.BCrypt.GenerateSalt());

            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

                try
                {
                    var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
                    if (existingUser != null)
                    {
                        context.Response.StatusCode = 409; // Conflict
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { message = "Username already taken" });
                        return;
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
        }

    }

}

