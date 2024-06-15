using System;
using System.IO;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using Services; // Adjust namespace as per your project

namespace Test
{
    public class DatabaseIntegrationTests : IDisposable
    {
        private readonly ApplicationDBContext _dbContext;
        private readonly ServiceProvider _serviceProvider;
        private readonly ITestOutputHelper _output;

        public DatabaseIntegrationTests(ITestOutputHelper output)
        {
            _output = output;

            // Set the current directory to where your .env file is located
            string envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "../.."); // Adjust path as per your structure
            Directory.SetCurrentDirectory(Path.GetDirectoryName(envFilePath));

            // Load environment variables from .env file
            DotNetEnv.Env.Load(envFilePath);

            // Fetch the connection string from environment variables
            string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                ?? "input your env variable here";//Fuck xunit not being able to read dotenv files.

            _output.WriteLine($"Connection string: {connectionString}");

            // Setup service provider with DbContext
            _serviceProvider = new ServiceCollection()
                .AddDbContext<ApplicationDBContext>(options =>
                {
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 28)));
                })
                .BuildServiceProvider();

            _dbContext = _serviceProvider.GetRequiredService<ApplicationDBContext>();
        }

        [Fact]
        public async Task TestDatabaseConnection()
        {
            // Act
            var isConnected = await _dbContext.Database.CanConnectAsync();

            // Assert
            Assert.True(isConnected);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
        }
    }
}
