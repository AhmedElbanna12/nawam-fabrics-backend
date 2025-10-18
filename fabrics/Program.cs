
using Docker.DotNet.Models;
using fabrics.Services;
using fabrics.Services.Interface;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace fabrics
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseUrls("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "7227"));
           ////builder.WebHost.UseUrls($"http://*:{Port}");



            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddSingleton<TelegramService>();
            builder.Services.AddScoped<IAirtableService, AirtableService>();
            builder.Services.AddScoped<AirtableService>();
            builder.Services.AddScoped<FaqService>();


            // builder.Services.AddScoped<MessengerService>();
            builder.Services.AddHttpClient();



            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy => policy
                        .WithOrigins(
                            // Local development
                            "http://localhost:3000",
                            "http://127.0.0.1:3000",

                            // Docker internal
                            "http://frontend:3000",

                            // Nginx configurations
                            "http://localhost",              // Nginx serving frontend
                            "http://127.0.0.1",             // Alternative nginx localhost
                            "https://localhost",             // HTTPS nginx
                            "https://127.0.0.1",            // HTTPS alternative
                            "http://yourdomain.com",         // Production domain (nginx)
                            "https://yourdomain.com",      // Production HTTPS domain (nginx)
                            "http://elnawamfabrics.com",
                            "https://elnawamfabrics.com",
                            "http://72.61.163.15",
                            "https://72.61.163.15"
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()); // Important for cookies/auth headers

            });



            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseCors("AllowFrontend"); // Use the specific frontend policy for better security
            var telegramService = app.Services.GetRequiredService<TelegramService>();

            // ? polling loop
            var bot = new TelegramBotClient(builder.Configuration["Telegram:BotToken"]);

            var cts = new CancellationTokenSource();
            bot.StartReceiving(
                async (bot, update, token) => await telegramService.RegisterUserAsync(update),
                (bot, exception, token) => Task.CompletedTask,
                cancellationToken: cts.Token
            );

            app.MapControllers(); // ?? ???? ??? Controllers ?????


            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

           // app.UseHttpsRedirection();


            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
