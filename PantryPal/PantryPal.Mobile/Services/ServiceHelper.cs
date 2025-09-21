using Microsoft.Extensions.DependencyInjection;

namespace PantryPal.Mobile.Services
{
    public static class ServiceHelper
    {
        private static IServiceProvider? _provider;

        public static void Init(IServiceProvider provider) => _provider = provider;

        private static IServiceProvider? TryCurrentAppServices()
        {
            return Application.Current?.Handler?.MauiContext?.Services;
        }

        private static IServiceProvider GetProviderOrThrow()
        {
            var p = _provider ?? TryCurrentAppServices();
            if (p is null)
                throw new InvalidOperationException("Service provider not initialized.");
            return p;
        }

        public static T Get<T>() where T : notnull
            => GetProviderOrThrow().GetRequiredService<T>();
    }
}
