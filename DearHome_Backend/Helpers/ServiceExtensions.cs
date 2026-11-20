using System;
using DearHome_Backend.Services.Interfaces;
using DearHome_Backend.Services.Implementations;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Repositories.Implementations;
using Microsoft.AspNetCore.Identity;
using DearHome_Backend.Models;
using DearHome_Backend.Config;
using DearHome_Backend.Services.External;
using Microsoft.Extensions.Options;

namespace DearHome_Backend.Helpers;

public static class ServiceExtensions
{
    public static void AddScopedServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IVerificationCodeService, VerificationCodeService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAttributeService, AttributeService>();
        services.AddScoped<IFirebaseStorageService, FirebaseStorageService>();
        services.AddScoped<IPlacementService, PlacementService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAttributeService, AttributeService>();
        services.AddScoped<IVariantService, VariantService>();
        services.AddScoped<IGoodReceivedNoteService, GoodReceivedNoteService>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPayOSClient, PayOSClient>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IStatisticService, StatisticService>();
        services.AddSingleton<NatsService>();
    }

    public static void AddScopedRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IAttributeRepository, AttributeRepository>();
        services.AddScoped<IPlacementRepository, PlacementRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IAttributeValueRepository, AttributeValueRepository>();
        services.AddScoped<IVariantRepository, VariantRepository>();
        services.AddScoped<IVariantAttributeRepository, VariantAttributeRepository>();
        services.AddScoped<IGoodReceivedNoteRepository, GoodReceivedNoteRepository>();
        services.AddScoped<IGoodReceivedItemRepository, GoodReceivedItemRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
    }

    /// <summary>
    /// Binds strongly-typed settings classes (consumed via <see cref="IOptions{TOptions}"/>).
    /// </summary>
    public static void AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NatsConfig>(configuration.GetSection("Nats"));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoshipOptions>(configuration.GetSection(GoshipOptions.SectionName));
        services.Configure<PayOSOptions>(configuration.GetSection(PayOSOptions.SectionName));
    }

    /// <summary>
    /// Typed HttpClients for external APIs (transient, handlers pooled by IHttpClientFactory).
    /// </summary>
    public static void AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IGoshipClient, GoshipClient>((serviceProvider, client) =>
        {
            var baseUrl = serviceProvider.GetRequiredService<IOptions<GoshipOptions>>().Value.BaseUrl;
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new InvalidOperationException("GoShip:BaseUrl is not configured in application settings.");
            }
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddHttpClient<IGoogleAuthService, GoogleAuthService>();
    }
}
