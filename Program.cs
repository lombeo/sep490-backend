using Api_Project_Prn.Infra.Constants;
using Api_Project_Prn.Services.CacheService;
using System.ComponentModel.Design;

namespace Api_Project_Prn
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container.
            builder.ConfigureServices();

            var app = builder.Build().ConfigurePipeline();

            var scope = app.Services.GetService<IServiceScopeFactory>().CreateScope();
            scope.ServiceProvider.GetService<IPubSubService>().SubscribeInternal();

            var utcNow = DateTime.UtcNow;
            int timeToday = utcNow.Year + utcNow.Month + utcNow.Day;
            StaticVariable.TimeToday = timeToday;

            //scope.ServiceProvider.GetService<IDiscussionService>()?.InitDiscussionMemory();
            //scope.ServiceProvider.GetService<IHelpService>()?.InitHelpMemory();
            app.Run();
        }
    }
}
