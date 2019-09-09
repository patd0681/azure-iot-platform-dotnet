﻿
using System.Linq;
using IdentityGateway.Services;
using IdentityGateway.Services.Runtime;
using IdentityGateway.Services.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityGateway.WebService
{
    public class Startup
    {
        private const string APP_CONFIGURATION = "PCS_APPLICATION_CONFIGURATION";

        // Initialized in `Startup`
        public IConfigurationRoot Configuration { get; }

        // Invoked by `Program.cs`
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
#if DEBUG
                .AddIniFile("appsettings.ini", optional: false, reloadOnChange: true)
#endif
                ;
            // build configuration with environment variables
            var preConfig = builder.Build();
            // Add app config settings to the configuration builder
            builder.Add(new AppConfigSettingsSource(preConfig[APP_CONFIGURATION]));
            Configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddControllersAsServices();

            services.AddScoped<TableHelper>();
            
            services.AddSingleton<UserSettingsContainer>();
            services.AddSingleton<UserTenantContainer>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddSingleton<IStatusService, StatusService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMiddleware<AuthMiddleware>();
            app.UseMvc();
        }
    }
}
