using System.Net;
using System.Net.Http.Json;
using HeckelCrm.Core.DTOs;
using HeckelCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace HeckelCrm.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext? _dbContext;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real database context
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });

                // Build service provider to get DbContext
                var sp = services.BuildServiceProvider();
                _dbContext = sp.GetRequiredService<ApplicationDbContext>();
                _dbContext.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetLeads_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/leads");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // Requires authentication
    }


    public void Dispose()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
        _client?.Dispose();
    }
}

// Create a Program class for testing
public partial class Program { }

