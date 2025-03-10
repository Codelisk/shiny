﻿#if PLATFORM
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.SpeechRecognition;

namespace Shiny;


public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpeechRecognition(this IServiceCollection services)
    {
        services.TryAddSingleton<ISpeechRecognizer, SpeechRecognizerImpl>();
        return services;
    }
}
#endif
