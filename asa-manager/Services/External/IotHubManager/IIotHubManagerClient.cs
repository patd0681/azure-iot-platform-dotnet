using System.Collections.Generic;
using System.Threading.Tasks;
using Mmm.Platform.IoT.AsaManager.Services.Models.DeviceGroups;
using Mmm.Platform.IoT.Common.Services;

namespace Mmm.Platform.IoT.AsaManager.Services.External.IotHubManager
{
    public interface IIotHubManagerClient : IStatusOperation
    {
        Task<DeviceListModel> GetListAsync(IEnumerable<DeviceGroupConditionModel> conditions, string tenantId);
    }
}