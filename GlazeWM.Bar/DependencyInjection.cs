using ManagedShell;
using Microsoft.Extensions.DependencyInjection;

namespace GlazeWM.Bar
{
  public static class DependencyInjection
  {
    public static IServiceCollection AddBarServices(this IServiceCollection services)
    {
      services.AddSingleton<BarService>();
      services.AddSingleton<ShellManager>();

      return services;
    }
  }
}
