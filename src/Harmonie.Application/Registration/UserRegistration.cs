using Harmonie.Application.Common;
using Harmonie.Application.Features.Users.DeleteMyAvatar;
using Harmonie.Application.Features.Users.GetMyProfile;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Features.Users.UpdateMyProfile;
using Harmonie.Application.Features.Users.UpdateUserStatus;
using Harmonie.Application.Features.Users.UploadMyAvatar;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class UserRegistration
{
    public static IServiceCollection AddUserHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<Unit, GetMyProfileResponse, GetMyProfileHandler>();
        services.AddAuthenticatedHandler<UpdateMyProfileRequest, UpdateMyProfileResponse, UpdateMyProfileHandler>();
        services.AddAuthenticatedHandler<Unit, bool, DeleteMyAvatarHandler>();
        services.AddAuthenticatedHandler<UploadMyAvatarInput, UploadMyAvatarResponse, UploadMyAvatarHandler>();
        services.AddAuthenticatedHandler<SearchUsersRequest, SearchUsersResponse, SearchUsersHandler>();
        services.AddAuthenticatedHandler<UpdateUserStatusRequest, UpdateUserStatusResponse, UpdateUserStatusHandler>();

        return services;
    }
}
