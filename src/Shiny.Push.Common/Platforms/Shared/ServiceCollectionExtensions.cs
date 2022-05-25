﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.Notifications;
using Shiny.Push;
using Shiny.Push.Infrastructure;

namespace Shiny;


public static class ServiceCollectionExtensions
{
    public static bool AddPush(this IServiceCollection services, Type pushManagerType, Type delegateType)
    {
#if IOS || MACCATALYST
        // TODO: can I hook these differently dynamically with selector?
        AppleExtensions.AssertAppDelegateHook(
            "application:didReceiveRemoteNotification:fetchCompletionHandler:",
            "[SHINY] AppDelegate.DidReceiveRemoteNotification is not hooked - background notifications will not work without this!"
        );

        // application:didRegisterForRemoteNotificationsWithDeviceToken:"
        AppleExtensions.AssertAppDelegateHook(
            "application:didRegisterForRemoteNotificationsWithDeviceToken:",
            "[SHINY] AppDelegate.RegisteredForRemoteNotifications is not hooked. This is a necessary hook for Shiny Push"
        );

        //application: didFailToRegisterForRemoteNotificationsWithError
        AppleExtensions.AssertAppDelegateHook(
            "application:didFailToRegisterForRemoteNotificationsWithError:",
            "[SHINY] AppDelegate.FailedToRegisterForRemoteNotifications is not hooked. This is a necessary hook for Shiny Push"
        );
#endif

#if IOS || MACCATALYST || ANDROID
        services.AddChannelManager();
        services.AddShinyService(typeof(IPushDelegate), delegateType);
        services.TryAddSingleton(typeof(IPushManager), pushManagerType);
        services.AddShinyService<PushContainer>();
        services.AddShinyServiceWithLifecycle<INativeAdapter, NativeAdapter>();
        return true;
#else
        return false;
#endif
    }
}