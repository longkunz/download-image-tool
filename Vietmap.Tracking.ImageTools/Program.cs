using Hangfire;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Vietmap.Tracking.ImageTools.Services;

var builder = WebApplication.CreateBuilder(args);

// Log.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
#if !DEBUG
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
#endif
    .CreateLogger();

builder.Host.UseSerilog();
// Add services to the container.
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký Hangfire
builder.Services.AddHangfire(config =>
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseDefaultTypeSerializer()
          .UseSQLiteStorage());

builder.Services.AddHangfireServer();

builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);
builder.Services.AddSingleton<ProcessStatusManager>();
builder.Services.AddTransient<IImageDownloadService, ImageDownloadService>();

var app = builder.Build();

// Create content directory.
var contentDirectory = Path.Combine(app.Environment.ContentRootPath, "contents");
if (!Directory.Exists(contentDirectory))
{
    Directory.CreateDirectory(contentDirectory);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(contentDirectory),
    RequestPath = "/contents"
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
        {
            RequireSsl = false,
            SslRedirect = false,
            LoginCaseSensitive = true,
            Users = new []
            {
                new BasicAuthAuthorizationUser
                {
                    Login = app.Configuration["Authentication:Hangfire:Username"],
                    PasswordClear =  app.Configuration["Authentication:Hangfire:Password"]
                }
            }

        }) }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

RecurringJob.AddOrUpdate<ImageDownloadService>("CleanUpTempFiles", service => service.CleanUpTempFiles(), app.Configuration["CronExpression"] ?? "*/5 * * * *");

app.Run();
