using Sep490_Backend.DTO.Configuration;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.Hosted;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.ComponentModel.Design;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Sep490_Backend.Services.AuthenService;
using Microsoft.OpenApi.Models;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.AdminService;
using Sep490_Backend.Services.OTPService;
using Sep490_Backend.Services.HelperService;
//using Sep490_Backend.Services.SiteSurveyService;
using Sep490_Backend.Services.CustomerService;
using Sep490_Backend.Services.ProjectService;
using Sep490_Backend.Services.ContractService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.Infra.ModelBinders;
using Sep490_Backend.Services.SiteSurveyService;
using Sep490_Backend.Services.MaterialService;
using Sep490_Backend.Services.ConstructionTeamService;
using Sep490_Backend.Services.ResourceReqService;
using Sep490_Backend.Services.ConstructionPlanService;
using Sep490_Backend.Services.VehicleService;
using Sep490_Backend.Services.ActionLogService;
using System.Net;
using Sep490_Backend.Services.ConstructionLogService;
using Sep490_Backend.Services.ConstructionProgressService;
using Sep490_Backend.Services.InspectionReportService;

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
            
            // Add response caching services
            builder.Services.AddResponseCaching(options =>
            {
                // Limit cache size to 100 MB
                options.MaximumBodySize = 100 * 1024 * 1024;
                options.UseCaseSensitivePaths = false;
            });
            
            builder.Services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new Infra.ModelBinders.ContractDetailModelBinderProvider());
                options.ModelBinderProviders.Insert(0, new Infra.ModelBinders.ConstructionLogArrayModelBinderProvider());
                
                // Add cache profiles for different scenarios
                options.CacheProfiles.Add("Default", new Microsoft.AspNetCore.Mvc.CacheProfile
                {
                    Duration = 60 // 1 minute
                });
                options.CacheProfiles.Add("Short", new Microsoft.AspNetCore.Mvc.CacheProfile
                {
                    Duration = 30 // 30 seconds
                });
                options.CacheProfiles.Add("Long", new Microsoft.AspNetCore.Mvc.CacheProfile
                {
                    Duration = 300 // 5 minutes
                });
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new Sep490_Backend.Infra.Helps.ReviewerDictionaryConverter());
                options.JsonSerializerOptions.Converters.Add(new Sep490_Backend.Infra.Services.DateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new Sep490_Backend.Infra.Services.NullableDateTimeJsonConverter());
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            });
            
            builder.Services.AddDistributedRedisCache(options =>
            {
                var connectionString = StaticVariable.RedisConfig.ConnectionString;
                options.Configuration = connectionString;
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<RedisConnManager>();
            builder.Services.AddScoped<ICacheService, CacheService>();
            builder.Services.AddScoped<IPubSubService, PubSubService>();
            builder.Services.AddScoped<IAuthenService, AuthenService>();
            builder.Services.AddScoped<IAdminService, AdminService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IOTPService, OTPService>();
            builder.Services.AddScoped<IHelperService, HelperService>();
            builder.Services.AddScoped<ISiteSurveyService, SiteSurveyService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IContractService, ContractService>();
            builder.Services.AddScoped<IDataService, DataService>();
            builder.Services.AddScoped<IGoogleDriveService, GoogleDriveService>();
            builder.Services.AddScoped<IMaterialService, MaterialService>();
            builder.Services.AddScoped<IConstructionTeamService, ConstructionTeamService>();
            builder.Services.AddScoped<IResourceReqService, ResourceReqService>();
            builder.Services.AddScoped<IConstructionPlanService, ConstructionPlanService>();
            builder.Services.AddScoped<IVehicleService, VehicleService>();
            builder.Services.AddScoped<IActionLogService, ActionLogService>();
            builder.Services.AddScoped<IConstructionLogService, ConstructionLogService>();
            builder.Services.AddScoped<IConstructionProgressService, ConstructionProgressService>();
            builder.Services.AddScoped<IInspectionReportService, InspectionReportService>();
            builder.Services.AddHostedService<DefaultBackgroundService>();

            builder.Services.AddEndpointsApiExplorer();
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

            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SEP490 API", Version = "v1" });

                // Custom schema ID generator to avoid conflicts between DTOs with same name
                c.CustomSchemaIds(type => type.FullName);

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme.
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                  {
                    {
                      new OpenApiSecurityScheme
                      {
                        Reference = new OpenApiReference
                          {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                          },
                          Scheme = "oauth2",
                          Name = "Bearer",
                          In = ParameterLocation.Header,
                        },
                        new List<string>()
                      }
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

            // Add response caching middleware
            app.UseResponseCaching();
            
            // Add cache control headers middleware
            app.Use(async (context, next) =>
            {
                // For GET and HEAD requests, add cache control headers
                if (context.Request.Method == "GET" || context.Request.Method == "HEAD")
                {
                    // For Vehicle API endpoints, set cache headers
                    if (context.Request.Path.StartsWithSegments("/sep490/vehicle"))
                    {
                        // Skip caching for authenticated endpoints that modify data
                        if (!context.Request.Path.ToString().Contains("/create") && 
                            !context.Request.Path.ToString().Contains("/update") && 
                            !context.Request.Path.ToString().Contains("/delete"))
                        {
                            context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                            {
                                Public = true,
                                MaxAge = TimeSpan.FromSeconds(60),
                                MustRevalidate = true
                            };
                        }
                        else
                        {
                            // Ensure write operations aren't cached
                            context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                            {
                                NoStore = true,
                                NoCache = true,
                                MustRevalidate = true
                            };
                        }
                    }
                    // For ActionLog API endpoints, set cache headers
                    else if (context.Request.Path.StartsWithSegments("/sep490/actionlog"))
                    {
                        // Skip caching for authenticated endpoints that modify data
                        if (!context.Request.Path.ToString().Contains("/create") && 
                            !context.Request.Path.ToString().Contains("/update") && 
                            !context.Request.Path.ToString().Contains("/delete") &&
                            !context.Request.Path.ToString().Contains("/invalidate-cache"))
                        {
                            context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                            {
                                Public = true,
                                MaxAge = TimeSpan.FromSeconds(60),
                                MustRevalidate = true
                            };
                        }
                        else
                        {
                            // Ensure write operations aren't cached
                            context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                            {
                                NoStore = true,
                                NoCache = true,
                                MustRevalidate = true
                            };
                        }
                    }
                }
                
                await next();
            });

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