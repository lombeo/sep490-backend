using Sep490_Backend.DTO.Configuration;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.Hosted;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.ComponentModel.Design;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Sep490_Backend.Services.AuthenService;

namespace Sep490_Backend
{
    public static class HostingExtensions
    {
        public static void ConfigureServices(this WebApplicationBuilder builder)
        {
            builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
            {
                AppSettings.Instance.SetConfiguration(hostingContext.Configuration);
            });
            builder.Host.UseSerilog((hostContext, services, configuration) =>
            {
                configuration.ReadFrom.Configuration(hostContext.Configuration);
            });
            
            builder.Services.AddHttpClient();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
            });
            builder.Services.AddControllers();
            builder.Services.AddDistributedRedisCache(options =>
            {
                var connectionString = StaticVariable.RedisConfig.ConnectionString;
                options.Configuration = connectionString;
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<ICacheService, CacheService>();
            builder.Services.AddScoped<IPubSubService, PubSubService>();
            builder.Services.AddScoped<IAuthenService, AuthenService>();
            builder.Services.AddScoped<RedisConnManager>();
            builder.Services.AddHostedService<DefaultBackgroundService>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHealthChecks();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
            });

            string certPath = builder.Environment.ContentRootPath + StaticVariable.JwtValidation.CertificatePath;
            StaticVariable.JwtValidation.CertificatePath = certPath;

            // Adding Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })

            // Adding Jwt Bearer
            .AddJwtBearer(options =>
            {
                X509Certificate2 cert = new X509Certificate2(StaticVariable.JwtValidation.CertificatePath, StaticVariable.JwtValidation.CertificatePassword);
                SecurityKey key = new X509SecurityKey(cert);

                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = StaticVariable.JwtValidation.ValidAudience,
                    ValidIssuer = StaticVariable.JwtValidation.ValidIssuer,
                    IssuerSigningKey = key,
                    ValidateLifetime = false,
                };
            });

            var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddDbContext<BackendContext>(options => options.UseNpgsql(connectionString));
            builder.Services.AddScoped<BackendContext>();
        }

        public static WebApplication ConfigurePipeline(this WebApplication app)
        {
            app.UseSerilogRequestLogging();
            InitializeDatabase(app);

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseRouting();
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/author/healthy");
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("[API] SEP490 Backend");
                });
            });

            

            return app;
        }

        private static void InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<BackendContext>();
                if (context.Database.GetPendingMigrations().Any())
                {
                    context.Database.MigrateAsync();
                }
            }
        }
    }
}