using System.Reflection;
using FluentValidation;
using Harmonie.Application.Features.Auth.Login;
using Harmonie.Application.Features.Auth.LogoutAll;
using Harmonie.Application.Features.Auth.Logout;
using Harmonie.Application.Features.Auth.RefreshToken;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application;

/// <summary>
/// Extension methods for configuring Application layer services
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register feature handlers
        // Auth features
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<LogoutAllHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateGuildHandler>();
        services.AddScoped<ListUserGuildsHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<GetGuildChannelsHandler>();
        services.AddScoped<GetGuildMembersHandler>();
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<GetMessagesHandler>();
        services.AddScoped<GetMyProfileHandler>();
        services.AddScoped<UpdateMyProfileHandler>();
        // Add more handlers as features are created

        return services;
    }
}
