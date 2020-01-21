﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mmm.Platform.IoT.IoTHubManager.Services.Helpers;
using Mmm.Platform.IoT.IoTHubManager.Services.Models;
using Microsoft.Extensions.Logging;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Common.Services.External.StorageAdapter;
using Newtonsoft.Json;
using Mmm.Platform.IoT.Common.Services.Config;

namespace Mmm.Platform.IoT.IoTHubManager.Services
{
    public class DeviceProperties : IDeviceProperties
    {
        public const string CACHE_COLLECTION_ID = "device-twin-properties";
        public const string CACHE_KEY = "cache";
        public const string WHITELIST_TAG_PREFIX = "tags.";
        public const string WHITELIST_REPORTED_PREFIX = "reported.";
        public const string TAG_PREFIX = "Tags.";
        public const string REPORTED_PREFIX = "Properties.Reported.";
        public readonly IStorageAdapterClient storageClient;
        public readonly IDevices devices;
        public readonly ILogger _logger;
        public readonly string whitelist;
        public readonly long ttl;
        public readonly long rebuildTimeout;
        public readonly TimeSpan serviceQueryInterval = TimeSpan.FromSeconds(10);

        private DateTime DevicePropertiesLastUpdated;

        public DeviceProperties(
            IStorageAdapterClient storageClient,
            AppConfig config,
            ILogger<DeviceProperties> logger,
            IDevices devices)
        {
            this.storageClient = storageClient;
            _logger = logger;
            this.whitelist = config.IotHubManagerService.DevicePropertiesCache.Whitelist;
            this.ttl = config.IotHubManagerService.DevicePropertiesCache.Ttl;
            this.rebuildTimeout = config.IotHubManagerService.DevicePropertiesCache.RebuildTimeout;
            this.devices = devices;
        }

        public async Task<List<string>> GetListAsync()
        {
            ValueApiModel response = new ValueApiModel();
            try
            {
                response = await this.storageClient.GetAsync(CACHE_COLLECTION_ID, CACHE_KEY);
            }
            catch (ResourceNotFoundException)
            {
                _logger.LogDebug($"Cache get: cache {CACHE_COLLECTION_ID}:{CACHE_KEY} was not found");
            }
            catch (Exception e)
            {
                throw new ExternalDependencyException(
                    $"Cache get: unable to get device-twin-properties cache", e);
            }

            if (string.IsNullOrEmpty(response?.Data))
            {
                throw new Exception($"StorageAdapter did not return any data for {CACHE_COLLECTION_ID}:{CACHE_KEY}. The DeviceProperties cache has not been created for this tenant yet.");
            }

            DevicePropertyServiceModel properties = new DevicePropertyServiceModel();
            try
            {
                properties = JsonConvert.DeserializeObject<DevicePropertyServiceModel>(response.Data);
            }
            catch (Exception e)
            {
                throw new InvalidInputException("Unable to deserialize deviceProperties from CosmosDB", e);
            }

            List<string> result = new List<string>();
            foreach (string tag in properties.Tags)
            {
                result.Add(TAG_PREFIX + tag);
            }
            foreach (string reported in properties.Reported)
            {
                result.Add(REPORTED_PREFIX + reported);
            }
            return result;
        }

        public async Task<bool> TryRecreateListAsync(bool force = false)
        {
            var @lock = new StorageWriteLock<DevicePropertyServiceModel>(
                this.storageClient,
                CACHE_COLLECTION_ID,
                CACHE_KEY,
                (c, b) => c.Rebuilding = b,
                m => this.ShouldCacheRebuild(force, m));

            while (true)
            {
                var locked = await @lock.TryLockAsync();
                if (locked == null)
                {
                    _logger.LogWarning("Cache rebuilding: lock failed due to conflict. Retry soon");
                    continue;
                }

                if (!locked.Value)
                {
                    return false;
                }

                // Build the cache content
                var twinNamesTask = this.GetValidNamesAsync();

                try
                {
                    Task.WaitAll(twinNamesTask);
                }
                catch (Exception)
                {
                    _logger.LogWarning("Some underlying service is not ready. Retry after {interval}.", this.serviceQueryInterval);
                    try
                    {
                        await @lock.ReleaseAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Cache rebuilding: Unable to release lock");
                    }
                    await Task.Delay(this.serviceQueryInterval);
                    continue;
                }

                var twinNames = twinNamesTask.Result;
                try
                {
                    var updated = await @lock.WriteAndReleaseAsync(
                        new DevicePropertyServiceModel
                        {
                            Tags = twinNames.Tags,
                            Reported = twinNames.ReportedProperties
                        });
                    if (updated)
                    {
                        this.DevicePropertiesLastUpdated = DateTime.Now;
                        return true;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Cache rebuilding: Unable to write and release lock");
                }
                _logger.LogWarning("Cache rebuilding: write failed due to conflict. Retry soon");
            }
        }

        public async Task<DevicePropertyServiceModel> UpdateListAsync(
            DevicePropertyServiceModel deviceProperties)
        {
            // To simplify code, use empty set to replace null set
            deviceProperties.Tags = deviceProperties.Tags ?? new HashSet<string>();
            deviceProperties.Reported = deviceProperties.Reported ?? new HashSet<string>();

            string etag = null;
            while (true)
            {
                ValueApiModel model = null;
                try
                {
                    model = await this.storageClient.GetAsync(CACHE_COLLECTION_ID, CACHE_KEY);
                }
                catch (ResourceNotFoundException)
                {
                    _logger.LogInformation($"Cache updating: cache {CACHE_COLLECTION_ID}:{CACHE_KEY} was not found");
                }

                if (model != null)
                {
                    DevicePropertyServiceModel devicePropertiesFromStorage;

                    try
                    {
                        devicePropertiesFromStorage = JsonConvert.
                            DeserializeObject<DevicePropertyServiceModel>(model.Data);
                    }
                    catch
                    {
                        devicePropertiesFromStorage = new DevicePropertyServiceModel();
                    }
                    devicePropertiesFromStorage.Tags = devicePropertiesFromStorage.Tags ??
                        new HashSet<string>();
                    devicePropertiesFromStorage.Reported = devicePropertiesFromStorage.Reported ??
                        new HashSet<string>();

                    deviceProperties.Tags.UnionWith(devicePropertiesFromStorage.Tags);
                    deviceProperties.Reported.UnionWith(devicePropertiesFromStorage.Reported);
                    etag = model.ETag;
                    // If the new set of deviceProperties are already there in cache, return
                    if (deviceProperties.Tags.Count == devicePropertiesFromStorage.Tags.Count &&
                        deviceProperties.Reported.Count == devicePropertiesFromStorage.Reported.Count)
                    {
                        return deviceProperties;
                    }
                }

                var value = JsonConvert.SerializeObject(deviceProperties);
                try
                {
                    var response = await this.storageClient.UpdateAsync(
                        CACHE_COLLECTION_ID, CACHE_KEY, value, etag);
                    return JsonConvert.DeserializeObject<DevicePropertyServiceModel>(response.Data);
                }
                catch (ConflictingResourceException)
                {
                    _logger.LogInformation("Cache updating: failed due to conflict. Retry soon");
                }
                catch (Exception e)
                {
                    _logger.LogInformation(e, "Cache updating: failed");
                    throw new Exception("Cache updating: failed");
                }
            }
        }

        private async Task<DeviceTwinName> GetValidNamesAsync()
        {
            ParseWhitelist(this.whitelist, out var fullNameWhitelist, out var prefixWhitelist);

            var validNames = new DeviceTwinName
            {
                Tags = fullNameWhitelist.Tags,
                ReportedProperties = fullNameWhitelist.ReportedProperties
            };

            if (prefixWhitelist.Tags.Any() || prefixWhitelist.ReportedProperties.Any())
            {
                DeviceTwinName allNames = new DeviceTwinName();
                try
                {
                    // Get list of DeviceTwinNames from IOT-hub
                    allNames = await this.devices.GetDeviceTwinNamesAsync();
                }
                catch (Exception e)
                {
                    throw new ExternalDependencyException("Unable to fetch IoT devices", e);
                }
                validNames.Tags.UnionWith(allNames.Tags.
                    Where(s => prefixWhitelist.Tags.Any(s.StartsWith)));

                validNames.ReportedProperties.UnionWith(
                    allNames.ReportedProperties.Where(
                        s => prefixWhitelist.ReportedProperties.Any(s.StartsWith)));
            }

            return validNames;
        }

        private static void ParseWhitelist(
            string whitelist,
            out DeviceTwinName fullNameWhitelist,
            out DeviceTwinName prefixWhitelist)
        {
            /// <example>
            /// whitelist = "tags.*, reported.Protocol, reported.SupportedMethods,
            ///                 reported.DeviceMethodStatus, reported.FirmwareUpdateStatus"
            /// whitelistItems = [tags.*,
            ///                   reported.Protocol,
            ///                   reported.SupportedMethods,
            ///                   reported.DeviceMethodStatus,
            ///                   reported.FirmwareUpdateStatus]
            /// </example>
            var whitelistItems = whitelist.Split(',').Select(s => s.Trim());

            /// <example>
            /// tags = [tags.*]
            /// </example>
            var tags = whitelistItems
                .Where(s => s.StartsWith(WHITELIST_TAG_PREFIX, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Substring(WHITELIST_TAG_PREFIX.Length));

            /// <example>
            /// reported = [reported.Protocol,
            ///             reported.SupportedMethods,
            ///             reported.DeviceMethodStatus,
            ///             reported.FirmwareUpdateStatus]
            /// </example>
            var reported = whitelistItems
                .Where(s => s.StartsWith(WHITELIST_REPORTED_PREFIX, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Substring(WHITELIST_REPORTED_PREFIX.Length));

            /// <example>
            /// fixedTags = []
            /// </example>
            var fixedTags = tags.Where(s => !s.EndsWith("*"));
            /// <example>
            /// fixedReported = [reported.Protocol,
            ///                  reported.SupportedMethods,
            ///                  reported.DeviceMethodStatus,
            ///                  reported.FirmwareUpdateStatus]
            /// </example>
            var fixedReported = reported.Where(s => !s.EndsWith("*"));

            /// <example>
            /// regexTags = [tags.]
            /// </example>
            var regexTags = tags.Where(s => s.EndsWith("*")).Select(s => s.Substring(0, s.Length - 1));
            /// <example>
            /// regexReported = []
            /// </example>
            var regexReported = reported.
                Where(s => s.EndsWith("*")).
                Select(s => s.Substring(0, s.Length - 1));

            /// <example>
            /// fullNameWhitelist = {Tags = [],
            ///                      ReportedProperties = [
            ///                         reported.Protocol,
            ///                         reported.SupportedMethods,
            ///                         reported.DeviceMethodStatus,
            ///                         reported.FirmwareUpdateStatus]
            ///                      }
            /// </example>
            fullNameWhitelist = new DeviceTwinName
            {
                Tags = new HashSet<string>(fixedTags),
                ReportedProperties = new HashSet<string>(fixedReported)
            };

            /// <example>
            /// prefixWhitelist = {Tags = [tags.],
            ///                    ReportedProperties = []}
            /// </example>
            prefixWhitelist = new DeviceTwinName
            {
                Tags = new HashSet<string>(regexTags),
                ReportedProperties = new HashSet<string>(regexReported)
            };
        }

        private bool ShouldCacheRebuild(bool force, ValueApiModel valueApiModel)
        {
            if (force)
            {
                _logger.LogInformation("Cache will be rebuilt due to the force flag");
                return true;
            }

            if (valueApiModel == null)
            {
                _logger.LogInformation("Cache will be rebuilt since no cache was found");
                return true;
            }
            DevicePropertyServiceModel cacheValue = new DevicePropertyServiceModel();
            DateTimeOffset timstamp;
            try
            {
                cacheValue = JsonConvert.DeserializeObject<DevicePropertyServiceModel>(valueApiModel.Data);
                timstamp = DateTimeOffset.Parse(valueApiModel.Metadata["$modified"]);
            }
            catch
            {
                _logger.LogInformation("DeviceProperties will be rebuilt because the last one is broken.");
                return true;
            }

            if (cacheValue.Rebuilding)
            {
                if (timstamp.AddSeconds(this.rebuildTimeout) < DateTimeOffset.UtcNow)
                {
                    _logger.LogDebug("Cache will be rebuilt because last rebuilding had timedout");
                    return true;
                }
                else
                {
                    _logger.LogDebug("Cache rebuilding skipped because it is being rebuilt by other instance");
                    return false;
                }
            }
            else
            {
                if (cacheValue.IsNullOrEmpty())
                {
                    _logger.LogInformation("Cache will be rebuilt since it is empty");
                    return true;
                }

                if (timstamp.AddSeconds(this.ttl) < DateTimeOffset.UtcNow)
                {
                    _logger.LogInformation("Cache will be rebuilt because it has expired");
                    return true;
                }
                else
                {
                    _logger.LogDebug("Cache rebuilding skipped because it has not expired");
                    return false;
                }
            }
        }
    }
}
