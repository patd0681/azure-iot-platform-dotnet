﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Mmm.Platform.IoT.Config.Services.Models;
using Mmm.Platform.IoT.Common.WebService.v1;
using Newtonsoft.Json;

namespace Mmm.Platform.IoT.Config.WebService.v1.Models
{
    public class DeviceGroupListApiModel
    {
        public IEnumerable<DeviceGroupApiModel> Items { get; set; }

        [JsonProperty("$metadata")]
        public Dictionary<string, string> Metadata { get; set; }

        public DeviceGroupListApiModel(IEnumerable<DeviceGroup> models)
        {
            this.Items = models.Select(m => new DeviceGroupApiModel(m));

            this.Metadata = new Dictionary<string, string>
            {
                { "$type", $"DeviceGroupList;{Version.NUMBER}" },
                { "$url", $"/{Version.PATH}/devicegroups" }
            };
        }
    }
}
