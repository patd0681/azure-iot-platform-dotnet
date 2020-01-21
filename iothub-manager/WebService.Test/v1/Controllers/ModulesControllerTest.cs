using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Common.TestHelpers;
using Mmm.Platform.IoT.IoTHubManager.Services;
using Mmm.Platform.IoT.IoTHubManager.Services.Models;
using Mmm.Platform.IoT.IoTHubManager.WebService.Controllers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Mmm.Platform.IoT.IoTHubManager.WebService.Test.Controllers
{
    public class ModulesControllerTest : IDisposable
    {
        private const string ContinuationTokenName = "x-ms-continuation";
        private readonly ModulesController modulesController;
        private readonly Mock<IDevices> devicesMock;
        private readonly HttpContext httpContext;
        private bool disposedValue = false;

        public ModulesControllerTest()
        {
            this.devicesMock = new Mock<IDevices>();
            this.httpContext = new DefaultHttpContext();
            this.modulesController = new ModulesController(this.devicesMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = this.httpContext,
                },
            };
        }

        [Theory]
        [Trait(Constants.Type, Constants.UnitTest)]
        [InlineData("", "", true)]
        [InlineData("deviceId", "", true)]
        [InlineData("", "moduleId", true)]
        [InlineData("deviceId", "moduleId", false)]
        public async Task GetSingleModuleTwinTest(string deviceId, string moduleId, bool throwsException)
        {
            if (throwsException)
            {
                await Assert.ThrowsAsync<InvalidInputException>(async () =>
                    await this.modulesController.GetModuleTwinAsync(deviceId, moduleId));
            }
            else
            {
                // Arrange
                var twinResult = ModulesControllerTest.CreateTestTwin(deviceId, moduleId);
                this.devicesMock.Setup(x => x.GetModuleTwinAsync(deviceId, moduleId))
                    .ReturnsAsync(twinResult);

                // Act
                var module = await this.modulesController.GetModuleTwinAsync(deviceId, moduleId);

                // Assert
                Assert.Equal(moduleId, module.ModuleId);
                Assert.Equal(deviceId, module.DeviceId);
                Assert.Equal("v2", module.Desired["version"]);
                Assert.Equal("v1", module.Reported["version"]);
            }
        }

        [Theory]
        [Trait(Constants.Type, Constants.UnitTest)]
        [InlineData("", "")]
        [InlineData("my module query", "continuationToken")]
        public async Task GetModuleTwinsTest(string query, string continuationToken)
        {
            const string resultToken = "nextToken";

            var twinList = new List<TwinServiceModel>() { ModulesControllerTest.CreateTestTwin("d", "m") };
            var twins = new TwinServiceListModel(twinList, resultToken);

            this.devicesMock.Setup(x => x.GetModuleTwinsByQueryAsync(query, continuationToken))
                .ReturnsAsync(twins);
            this.httpContext.Request.Headers.Add(
                ContinuationTokenName,
                new StringValues(continuationToken));

            // Act
            var moduleTwins = await this.modulesController.GetModuleTwinsAsync(query);

            // Assert
            var moduleTwin = moduleTwins.Items[0];
            Assert.Equal("d", moduleTwin.DeviceId);
            Assert.Equal("m", moduleTwin.ModuleId);
            Assert.Equal(resultToken, moduleTwins.ContinuationToken);
            Assert.Equal("v2", moduleTwin.Desired["version"]);
            Assert.Equal("v1", moduleTwin.Reported["version"]);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    modulesController.Dispose();
                }

                disposedValue = true;
            }
        }

        private static TwinServiceModel CreateTestTwin(string deviceId, string moduleId)
        {
            return new TwinServiceModel()
            {
                DeviceId = deviceId,
                ModuleId = moduleId,
                DesiredProperties = new Dictionary<string, JToken>()
                {
                    { "version", JToken.Parse("'v2'") },
                },
                ReportedProperties = new Dictionary<string, JToken>()
                {
                    { "version", JToken.Parse("'v1'") },
                },
            };
        }
    }
}