﻿using System.Text.Encodings.Web;
using System.Text.Unicode;
using BuildingBlocks;
using BuildingBlocks.Exception;
using BuildingBlocks.OpenTelemetry;
using BuildingBlocks.Types;
using DotNetCore.CAP.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using University.Courses.Application;
using University.Courses.Application.Services;
using University.Courses.Infrastructure.EfCore;
using University.Courses.Infrastructure.Services;

namespace University.Courses.Infrastructure
{
    public static class Extensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            var configuration = services.BuildServiceProvider().GetService<IConfiguration>();
            var connectionString = configuration!.GetSection("connectionString").Value;

            services.AddErrorHandler<ExceptionToResponseMapper>();
            services.AddTransient<IExceptionToMessageMapper, ExceptionToMessageMapper>();

            services.AddDbContext<CourseDbContext>(options =>
                options.UseSqlServer(connectionString));


            var outboxOptions = services.GetOptions<Options.OutboxOptions>("outbox");
            services.AddSingleton(outboxOptions);

            services.AddTransient<ICourseDbContext>(provider => provider.GetService<CourseDbContext>());

            services.AddTransient<IMessageBroker, MessageBroker>();
            services.AddTransient<IEventMapper, EventMapper>();
            services.AddTransient<IEventProcessor, EventProcessor>();
            
            services.AddCap(x =>
            {
                x.UseEntityFramework<CourseDbContext>();

                x.UseSqlServer(connectionString);

                x.UseRabbitMQ(r =>
                {
                    r.HostName = "localhost";
                    r.ExchangeName = "courses";
                    r.ExchangeName = "students";
                });

                x.FailedRetryCount = 5;
                x.FailedThresholdCallback = failed =>
                {
                    Log.Error(
                        $@"A message of type {failed.MessageType} failed after executing {x.FailedRetryCount} several times, 
                        requiring manual troubleshooting. Message name: {failed.Message.GetName()}");
                };
                x.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });
            
            services.AddOpenTelemetry();
            
            return services;
        }

        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            app.UseErrorHandler();

            return app;
        }
    }
}