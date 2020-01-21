using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using Mmm.Platform.IoT.Common.Services.Config;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Config.Services.Models;
using Mmm.Platform.IoT.IoTHubManager.Services.Helpers;
using Mmm.Platform.IoT.IoTHubManager.Services.Models;
using Newtonsoft.Json.Linq;
using static Mmm.Platform.IoT.Config.Services.Models.DeviceStatusQueries;

namespace Mmm.Platform.IoT.IoTHubManager.Services
{
    public class Deployments : IDeployments
    {
        private const int MaxDeployments = 20;
        private const string DeploymentNameLabel = "Name";
        private const string DeploymentGroupIdLabel = "DeviceGroupId";
        private const string DeploymentGroupNameLabel = "DeviceGroupName";
        private const string DeploymentPackageNameLabel = "PackageName";
        private const string RmCreatedLabel = "RMDeployment";
        private const string DeviceGroupIdParameter = "deviceGroupId";
        private const string DeviceGroupQueryParameter = "deviceGroupQuery";
        private const string NameParameter = "name";
        private const string PackageContentParameter = "packageContent";
        private const string ConfigurationTypeParameter = "configType";
        private const string PriorityParameter = "priority";
        private const string DeviceIdKey = "DeviceId";
        private const string EdgeManifestSchema = "schemaVersion";
        private readonly ILogger logger;
        private ITenantConnectionHelper tenantHelper;

        public Deployments(AppConfig config, ILogger<Deployments> logger, ITenantConnectionHelper tenantConnectionHelper)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            this.tenantHelper = tenantConnectionHelper;

            this.logger = logger;
        }

        public Deployments(ITenantConnectionHelper tenantHelper)
        {
            this.tenantHelper = tenantHelper ?? throw new ArgumentNullException("tenantHelper");
        }

        /// <summary>
        /// Schedules a deployment of the provided package, to the given group.
        /// </summary>
        /// <returns>Scheduled deployment</returns>
        public async Task<DeploymentServiceModel> CreateAsync(DeploymentServiceModel model)
        {
            if (string.IsNullOrEmpty(model.DeviceGroupId))
            {
                throw new ArgumentNullException(DeviceGroupIdParameter);
            }

            if (string.IsNullOrEmpty(model.DeviceGroupQuery))
            {
                throw new ArgumentNullException(DeviceGroupQueryParameter);
            }

            if (string.IsNullOrEmpty(model.Name))
            {
                throw new ArgumentNullException(NameParameter);
            }

            if (string.IsNullOrEmpty(model.PackageContent))
            {
                throw new ArgumentNullException(PackageContentParameter);
            }

            if (model.PackageType.Equals(PackageType.DeviceConfiguration)
                && string.IsNullOrEmpty(model.ConfigType))
            {
                throw new ArgumentNullException(ConfigurationTypeParameter);
            }

            if (model.Priority < 0)
            {
                throw new ArgumentOutOfRangeException(
                    PriorityParameter,
                    model.Priority,
                    "The priority provided should be 0 or greater");
            }

            var configuration = ConfigurationsHelper.ToHubConfiguration(model);

            // TODO: Add specific exception handling when exception types are exposed
            // https://github.com/Azure/azure-iot-sdk-csharp/issues/649
            return new DeploymentServiceModel(await tenantHelper.GetRegistry().AddConfigurationAsync(configuration));
        }

        /// <summary>
        /// Retrieves all deployments that have been scheduled on the iothub.
        /// Only deployments which were created by RM will be returned.
        /// </summary>
        /// <returns>All scheduled deployments with RMDeployment label</returns>
        public async Task<DeploymentServiceListModel> ListAsync()
        {
            // TODO: Currently they only support 20 deployments
            var deployments = await tenantHelper.GetRegistry().GetConfigurationsAsync(MaxDeployments);

            if (deployments == null)
            {
                throw new ResourceNotFoundException($"No deployments found for {tenantHelper.GetIotHubName()} hub.");
            }

            List<DeploymentServiceModel> serviceModelDeployments =
                deployments.Where(this.CheckIfDeploymentWasMadeByRM)
                           .Select(config => new DeploymentServiceModel(config))
                           .OrderBy(conf => conf.Name)
                           .ToList();

            return new DeploymentServiceListModel(serviceModelDeployments);
        }

        /// <summary>
        /// Retrieve information on a single deployment given its id.
        /// If includeDeviceStatus is included additional queries are created to retrieve the status of
        /// the deployment per device.
        /// </summary>
        /// <returns>Deployment for the given id</returns>
        public async Task<DeploymentServiceModel> GetAsync(string deploymentId, bool includeDeviceStatus = false)
        {
            if (string.IsNullOrEmpty(deploymentId))
            {
                throw new ArgumentNullException(nameof(deploymentId));
            }

            var deployment = await tenantHelper.GetRegistry().GetConfigurationAsync(deploymentId);

            if (deployment == null)
            {
                throw new ResourceNotFoundException($"Deployment with id {deploymentId} not found.");
            }

            if (!this.CheckIfDeploymentWasMadeByRM(deployment))
            {
                throw new ResourceNotSupportedException($"Deployment with id {deploymentId}" + @" was
                                                        created externally and therefore not supported");
            }

            IDictionary<string, DeploymentStatus> deviceStatuses = this.GetDeviceStatuses(deployment);

            return new DeploymentServiceModel(deployment)
            {
                DeploymentMetrics =
                {
                    DeviceMetrics = CalculateDeviceMetrics(deviceStatuses),
                    DeviceStatuses = includeDeviceStatus ? deviceStatuses : null,
                },
            };
        }

        public async Task DeleteAsync(string deploymentId)
        {
            if (string.IsNullOrEmpty(deploymentId))
            {
                throw new ArgumentNullException(nameof(deploymentId));
            }

            await tenantHelper.GetRegistry().RemoveConfigurationAsync(deploymentId);
        }

        private bool CheckIfDeploymentWasMadeByRM(Configuration conf)
        {
            return conf.Labels != null &&
                   conf.Labels.ContainsKey(RmCreatedLabel) &&
                   bool.TryParse(conf.Labels[RmCreatedLabel], out var res) && res;
        }

        private IDictionary<string, DeploymentStatus> GetDeviceStatuses(Configuration deployment)
        {
            string deploymentType = null;
            if (ConfigurationsHelper.IsEdgeDeployment(deployment))
            {
                deploymentType = PackageType.EdgeManifest.ToString();
            }
            else
            {
                deploymentType = PackageType.DeviceConfiguration.ToString();
            }

            deployment.Labels.TryGetValue(ConfigurationsHelper.ConfigTypeLabel, out string configType);
            var queries = GetQueries(deploymentType, configType);

            string deploymentId = deployment.Id;
            var appliedDevices = this.GetDevicesInQuery(queries[QueryType.APPLIED], deploymentId);

            var deviceWithStatus = new Dictionary<string, DeploymentStatus>();

            if (!ConfigurationsHelper.IsEdgeDeployment(deployment) && !configType.Equals(ConfigType.Firmware.ToString()))
            {
                foreach (var devices in appliedDevices)
                {
                    deviceWithStatus.Add(devices, DeploymentStatus.Unknown);
                }

                return deviceWithStatus;
            }

            var successfulDevices = this.GetDevicesInQuery(queries[QueryType.SUCCESSFUL], deploymentId);
            var failedDevices = this.GetDevicesInQuery(queries[QueryType.FAILED], deploymentId);

            foreach (var successfulDevice in successfulDevices)
            {
                deviceWithStatus.Add(successfulDevice, DeploymentStatus.Succeeded);
            }

            foreach (var failedDevice in failedDevices)
            {
                deviceWithStatus.Add(failedDevice, DeploymentStatus.Failed);
            }

            foreach (var device in appliedDevices)
            {
                if (!successfulDevices.Contains(device) && !failedDevices.Contains(device))
                {
                    deviceWithStatus.Add(device, DeploymentStatus.Pending);
                }
            }

            return deviceWithStatus;
        }

        private HashSet<string> GetDevicesInQuery(string hubQuery, string deploymentId)
        {
            var query = string.Format(hubQuery, deploymentId);
            var queryResponse = tenantHelper.GetRegistry().CreateQuery(query);
            var deviceIds = new HashSet<string>();

            try
            {
                while (queryResponse.HasMoreResults)
                {
                    // TODO: Add pagination with queryOptions
                    var resultSet = queryResponse.GetNextAsJsonAsync();
                    foreach (var result in resultSet.Result)
                    {
                        var deviceId = JToken.Parse(result)[DeviceIdKey];
                        deviceIds.Add(deviceId.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting status of devices in query {query}", query);
            }

            return deviceIds;
        }

        private IDictionary<DeploymentStatus, long> CalculateDeviceMetrics(
            IDictionary<string,
            DeploymentStatus> deviceStatuses)
        {
            if (deviceStatuses == null)
            {
                return null;
            }

            IDictionary<DeploymentStatus, long> deviceMetrics = new Dictionary<DeploymentStatus, long>();

            deviceMetrics[DeploymentStatus.Succeeded] = deviceStatuses.Where(item =>
                                                            item.Value == DeploymentStatus.Succeeded).LongCount();

            deviceMetrics[DeploymentStatus.Failed] = deviceStatuses.Where(item =>
                                                            item.Value == DeploymentStatus.Failed).LongCount();

            deviceMetrics[DeploymentStatus.Pending] = deviceStatuses.Where(item =>
                                                            item.Value == DeploymentStatus.Pending).LongCount();

            return deviceMetrics;
        }
    }
}