using CGM.Api.Data;
using CGM.Api.Services;
using CGM.Api.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore.Storage;
namespace CGM.Api.Tests;
public sealed class ApiFactory:WebApplicationFactory<Program>
{
    private readonly string _databaseName = "integration-" + Guid.NewGuid();
    public ApiFactory(){Environment.SetEnvironmentVariable("DB_CONNECTION_STRING","Server=test;Database=test;");Environment.SetEnvironmentVariable("JWT_SECRET",new string('t',64));}
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services=>
        {
            services.RemoveAll<DbContextOptions<CgmDbContext>>();services.RemoveAll<CgmDbContext>();
            services.RemoveAll<IDatabaseProvider>();
            services.RemoveAll<IHostedService>();services.RemoveAll<IEmailService>();
            var inMemoryProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            services.AddDbContext<CgmDbContext>(o=>o.UseInMemoryDatabase(_databaseName).UseInternalServiceProvider(inMemoryProvider));
            services.AddScoped<IEmailService,FakeEmailService>();
        });
    }
    private sealed class FakeEmailService:IEmailService
    {
        public Task<bool> SendWelcomeEmailAsync(string a,string b)=>Task.FromResult(true);
        public Task<bool> SendEmailVerificationCodeAsync(string a,string b,string c,DateTime d)=>Task.FromResult(true);
        public Task<bool> SendPasswordResetEmailAsync(string a,string b,string c,string d,DateTime e)=>Task.FromResult(true);
        public Task<bool> SendGlucoseAlertEmailAsync(string a,string b,string c,decimal d,string e,decimal f,bool g)=>Task.FromResult(true);
    }
}
