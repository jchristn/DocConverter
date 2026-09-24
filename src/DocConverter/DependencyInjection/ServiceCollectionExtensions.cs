namespace DocConverter.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers DocConverter with Microsoft.Extensions.DependencyInjection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register a singleton IConverter (and Converter) built from settings you configure here.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Optional settings callback.</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
        public static IServiceCollection AddDocConverter(this IServiceCollection services, Action<ConverterSettings>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            ConverterSettings settings = new ConverterSettings();
            if (configure != null) configure(settings);
            Converter converter = new Converter(settings);
            services.AddSingleton<Converter>(converter);
            services.AddSingleton<IConverter>(converter);
            return services;
        }
    }
}
