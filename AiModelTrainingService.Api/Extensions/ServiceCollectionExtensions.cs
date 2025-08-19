
using Microsoft.EntityFrameworkCore;
using AiModelTrainingService.Core.Interfaces;
using AiModelTrainingService.Infrastructure.Data;
using AiModelTrainingService.Infrastructure.Repositories;
using AiModelTrainingService.Services.Interfaces;
using AiModelTrainingService.Services.Implementations;

namespace AiModelTrainingService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Entity Framework
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("AiModelTrainingDb"));

        // Add repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Add memory cache
        services.AddMemoryCache();

        // Add existing business services
        services.AddScoped<IModelTrainerService, ModelTrainerService>();
        services.AddScoped<IDatasetService, DatasetService>();

        // Add new trading-specific services
        services.AddScoped<IModelTrainingService, ModelTrainingService>();
        services.AddScoped<IDataLoader, DataLoaderService>();
        services.AddScoped<IFeatureEngineering, FeatureEngineeringService>();
        services.AddScoped<IModelRepository, ModelRepositoryService>();
        services.AddScoped<IModelEvaluator, ModelEvaluatorService>();

        return services;
    }
}
