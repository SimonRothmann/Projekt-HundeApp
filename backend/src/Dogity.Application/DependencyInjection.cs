using Dogity.Application.Admin;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Notifications;
using Dogity.Application.Planning;
using Dogity.Application.Sports;
using Dogity.Application.Stats;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDogService, DogService>();
        services.AddScoped<ISportCatalogService, SportCatalogService>();
        services.AddScoped<IExerciseManagementService, ExerciseManagementService>();
        services.AddScoped<IRegulationImportService, RegulationImportService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IExerciseMasteryService, ExerciseMasteryService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IGpsTrackService, GpsTrackService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IClubService, ClubService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IStatsService, StatsService>();
        return services;
    }
}
