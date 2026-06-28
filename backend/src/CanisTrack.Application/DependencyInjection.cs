using CanisTrack.Application.Admin;
using CanisTrack.Application.Community;
using CanisTrack.Application.Dogs;
using CanisTrack.Application.Planning;
using CanisTrack.Application.Sports;
using CanisTrack.Application.Tracking;
using CanisTrack.Application.Training;
using Microsoft.Extensions.DependencyInjection;

namespace CanisTrack.Application;

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
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IGpsTrackService, GpsTrackService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IClubService, ClubService>();
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }
}
