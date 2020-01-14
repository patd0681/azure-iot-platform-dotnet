using System.Reflection;
using Autofac;
using Mmm.Platform.IoT.Common.Services;
using Mmm.Platform.IoT.AsaManager.Services;

namespace Mmm.Platform.IoT.AsaManager.WebService
{
    public class DependencyResolution : DependencyResolutionBase
    {
        protected override void SetupCustomRules(ContainerBuilder builder)
        {
            var assembly = typeof(StatusService).GetTypeInfo().Assembly;
            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces();
        }
    }
}