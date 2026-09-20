using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Emergency;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class ApiApplication : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "CareLanka#Test2026";
    public const string NurseEmail = "nurse.tests@carelanka.invalid";
    public const string ManagerEmail = "manager.tests@carelanka.invalid";
    public const string AdministratorEmail = "administrator.tests@carelanka.invalid";
    public const string InactiveEmail = "inactive.tests@carelanka.invalid";
    public const string DoctorEmail = "doctor.tests@carelanka.invalid";
    public const string EquipmentEmail = "equipment.tests@carelanka.invalid";

    public const string ReceptionEmail = "reception.tests@carelanka.invalid";

    public const string AmbulanceEmail = "ambulance.tests@carelanka.invalid";
    public const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    public const string EquipmentConfirmationCode = "test-confirmation-code";

    private readonly string _databasePassword = Guid.NewGuid().ToString("N");
    private readonly PostgreSqlContainer _database;
    private string? _originalConnectionString;
    private string? _originalSigningKey;

    public ConcurrentQueue<string> Logs { get; } = new();

    public ApiApplication()
    {
        _database = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("carelanka_tests")
            .WithUsername("carelanka_tests")
            .WithPassword(_databasePassword)
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("Equipment:ConfirmationCode", EquipmentConfirmationCode);
        // Tests run the sweep themselves; a timer sweeping mid-test would only add noise.
        builder.UseSetting("Equipment:WarningSweepIntervalMinutes", "0");

        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestPolicyController).Assembly);
            // Tests drain SceneLookupQueue and run the processor themselves; the real worker would call the internet.
            services.Remove(services.Single(service =>
                service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(SceneLookupWorker)));
            services.Replace(ServiceDescriptor.Singleton<IReverseGeocoder, NoAddressGeocoder>());
            // Tests run the delivery pass themselves and never talk to Firebase.
            services.Remove(services.Single(service =>
                service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(PushDeliveryWorker)));
            services.Replace(ServiceDescriptor.Singleton<IPushSender, RecordingPushSender>());
        });
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CareLanka");
        _originalSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        Environment.SetEnvironmentVariable("ConnectionStrings__CareLanka", _database.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", SigningKey);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        await db.Database.MigrateAsync();

        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        db.StaffMembers.AddRange(
            Staff(NurseEmail, StaffRole.WardNurse, true, passwords),
            Staff(ManagerEmail, StaffRole.DutyManager, true, passwords),
            Staff(InactiveEmail, StaffRole.Doctor, false, passwords),
            Staff(DoctorEmail, StaffRole.Doctor, true, passwords),
            Staff(AdministratorEmail, StaffRole.HospitalAdministrator, true, passwords),
            Staff(EquipmentEmail, StaffRole.EquipmentManager, true, passwords),
            Staff(ReceptionEmail, StaffRole.GeneralStaff, true, passwords),
            Staff(AmbulanceEmail, StaffRole.AmbulanceCrew, true, passwords));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        await _database.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__CareLanka", _originalConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", _originalSigningKey);
    }

    private static StaffMember Staff(
        string email, StaffRole role, bool active, IPasswordService passwords)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwords.Hash(Password),
            FirstName = role.ToString(),
            LastName = "Test",
            Role = role,
            IsActive = active
        };

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _logs;

        public CapturingLoggerProvider(ConcurrentQueue<string> logs) => _logs = logs;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_logs);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _logs;

        public CapturingLogger(ConcurrentQueue<string> logs) => _logs = logs;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _logs.Enqueue(formatter(state, exception));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiApplication>
{
    public const string Name = "API integration";
}
