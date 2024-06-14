using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Services;
using DotNetEnv;
using Xunit.Abstractions;

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

            // Load environment variables from .env file
            var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "path_to_your_env_file");
            Directory.SetCurrentDirectory(Path.GetDirectoryName(envFilePath));

            // Load environment variables from .env file
            DotNetEnv.Env.Load(envFilePath);

            // Fetch the connection string from environment variables
            string connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING"); 
            //    ?? throw new InvalidOperationException("Connection string not found in environment variables.");

            _output.WriteLine($"Connection string: {connectionString}");

            _serviceProvider = new ServiceCollection()
                .AddDbContext<ApplicationDBContext>(options =>
                {
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
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
