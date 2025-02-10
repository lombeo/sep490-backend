using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.CacheService;
using System.ComponentModel.Design;

namespace Sep490_Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            //AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

            // Load environment variables from .env
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            builder.ConfigureServices();

            var app = builder.Build().Configure();

            using (var scope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
            {
                scope.ServiceProvider.GetService<IPubSubService>()?.SubscribeInternal();
                scope.ServiceProvider.GetService<IAuthenService>()?.InitUserMemory();
            }

            // Setting the static variable
            var utcNow = DateTime.UtcNow;
            int timeToday = utcNow.Year + utcNow.Month + utcNow.Day;
            StaticVariable.TimeToday = timeToday;

            

            app.Run();
        }
    }
}
