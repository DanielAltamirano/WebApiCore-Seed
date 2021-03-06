﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using WebApiCoreSeed.Data.EF;
using WebApiCoreSeed.Domain.Services;
using WebApiCoreSeed.Domain.Services.Interfaces;
using WebApiCoreSeed.Infrastructure.AuthZero;
using WebApiCoreSeed.Infrastructure.RestClient;
using WebApiCoreSeed.WebApi.Authorization;
using WebApiCoreSeed.WebApi.Middleware;

namespace WebApiCoreSeed.WebApi
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<WebApiCoreSeedContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            // Add framework services.
            services.AddMvc();

            IAuthorizationPolicies authorizationPolicies = new AuthorizationPolicies();
            services.AddSingleton(authorizationPolicies);

            services.AddAuthorization(options =>
            {
                options.AddPolicy(nameof(AuthorizationPolicies.AdminOnly), authorizationPolicies.AdminOnly);
            });

            // Register Infrastructure dependencies
            services.AddScoped<IRestClient>(sp => new RestClient($"https://{Configuration["auth0:domain"]}", new HttpClient()));
            services.AddSingleton<IAuthZeroClient>(sp => new AuthZeroClient(sp.GetRequiredService<IRestClient>(), Configuration["auth0:NonInteractiveClientId"], Configuration["auth0:NonInteractiveClientSecret"], Configuration["auth0:domain"]));
            services.AddTransient<IAuthZeroService>(sp => new AuthZeroService(sp.GetRequiredService<IAuthZeroClient>()));

            // Register Services
            services.AddTransient<IUserService>(sp => new UserService(sp.GetRequiredService<WebApiCoreSeedContext>()));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, WebApiCoreSeedContext dbContext)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            var jwtOptions = new JwtBearerOptions
            {
                Audience = Configuration["auth0:clientId"],
                Authority = $"https://{Configuration["auth0:domain"]}/",
            };

            app.UseMiddleware(typeof(ErrorHandlingMiddleware));
            app.UseMiddleware(typeof(AuthorizationMiddleware));
            app.UseJwtBearerAuthentication(jwtOptions);

            app.UseMvc();
            DatabaseSeed.Initialize(dbContext);
        }
    }
}
