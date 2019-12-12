﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Mmm.Platform.IoT.IdentityGateway.Services.Models;
using Mmm.Platform.IoT.IdentityGateway.Services.Test.Helpers.Builders;
using Mmm.Platform.IoT.Common.Services.External.TableStorage;
using Mmm.Platform.IoT.Common.TestHelpers;
using Moq;
using Newtonsoft.Json;
using TestStack.Dossier;
using TestStack.Dossier.Lists;
using Xunit;

namespace Mmm.Platform.IoT.IdentityGateway.Services.Test
{
    public class UserTenantContainerTest
    {
        private UserTenantContainer userTenantContainer;
        private Mock<ITableStorageClient> mockTableStorageClient;
        private const int dynamicTableEntityCount = 100;
        private Random random = new Random();
        private UserTenantInput someUserTenantInput = Builder<UserTenantInput>.CreateNew().Build();
        private IList<DynamicTableEntity> dynamicTableEntities;
        private AnonymousValueFixture any = new AnonymousValueFixture();

        public UserTenantContainerTest()
        {
            mockTableStorageClient = new Mock<ITableStorageClient> { DefaultValue = DefaultValue.Mock };
            userTenantContainer = new UserTenantContainer(mockTableStorageClient.Object);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void GetAllReturnsExpectedUserTenantList()
        {
            // Arrange
            dynamicTableEntities = DynamicTableEntityBuilder
                .CreateListOfSize(dynamicTableEntityCount)
                .All()
                .WithRandomRolesProperty()
                .TheLast(random.Next(0, dynamicTableEntityCount))
                .Set(dte => dte.PartitionKey, someUserTenantInput.UserId)
                .BuildList();

            mockTableStorageClient
                .Setup(m => m.QueryAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<TableQuery<UserTenantModel>>(),
                    It.Is<CancellationToken>(t => t == default(CancellationToken))))
                .ReturnsAsync(dynamicTableEntities
                    .Where(dte => dte.PartitionKey == someUserTenantInput.UserId)
                    .Select(e => new UserTenantModel(e))
                    .ToList());

            // Act
            var result = await userTenantContainer.GetAllAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.QueryAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<TableQuery<UserTenantModel>>(),
                    It.Is<CancellationToken>(t => t == default(CancellationToken))),
                Times.Once);

            // Assert
            Assert.Equal("gettenants", result.batchMethod.ToLowerInvariant());
            Assert.NotNull(result.models);
            Assert.Equal(dynamicTableEntities.Count(dte => dte.PartitionKey == someUserTenantInput.UserId), result.models.Count);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void GetAllUsersReturnsExpectedUserTenantList()
        {
            // Arrange
            dynamicTableEntities = DynamicTableEntityBuilder
                .CreateListOfSize(dynamicTableEntityCount)
                .All()
                .WithRandomRolesProperty()
                .TheLast(random.Next(0, dynamicTableEntityCount))
                .Set(dte => dte.RowKey, someUserTenantInput.Tenant)
                .BuildList();

            // Act
            var result = await userTenantContainer.GetAllAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.QueryAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<TableQuery<UserTenantModel>>(),
                    It.Is<CancellationToken>(t => t == default(CancellationToken))),
                Times.Once);

            // Assert
            Assert.Equal("gettenants", result.batchMethod.ToLowerInvariant());
            Assert.NotNull(result.models);
            Assert.Equal(dynamicTableEntities.Count(dte => dte.PartitionKey == someUserTenantInput.UserId), result.models.Count);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void GetAllReturnsEmptyUserTenantList()
        {
            // Arrange
            dynamicTableEntities = new List<DynamicTableEntity>();

            // Act
            var result = await userTenantContainer.GetAllAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.QueryAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<TableQuery<UserTenantModel>>(),
                    It.Is<CancellationToken>(t => t == default(CancellationToken))),
                Times.Once);

            // Assert
            Assert.Equal("gettenants", result.batchMethod.ToLowerInvariant());
            Assert.NotNull(result.models);
            Assert.Empty(result.models);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void GetReturnsExpectedUserTenant()
        {
            // Arrange
            dynamicTableEntities = DynamicTableEntityBuilder
                .CreateListOfSize(dynamicTableEntityCount)
                .All()
                .WithRandomRolesProperty()
                .TheLast(1)
                .Set(dte => dte.PartitionKey, someUserTenantInput.UserId)
                .Set(dte => dte.RowKey, someUserTenantInput.Tenant)
                .BuildList();

            // Act
            var result = await userTenantContainer.GetAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()),
                Times.Once);

            // Assert
            AssertUserTenantMatchesInput(result);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void GetReturnsEmptyUserTenant()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync((UserTenantModel)null);

            // Act
            var result = await userTenantContainer.GetAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()),
                Times.Once);

            // Assert
            Assert.Null(result);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void CreateReturnsExpectedUserTenant()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync((UserTenantModel)null);

            mockTableStorageClient
                .Setup(m => m.InsertAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.Is<UserTenantModel>(t => t.TenantId == someUserTenantInput.Tenant && t.UserId == someUserTenantInput.UserId)))
                .ReturnsAsync(new UserTenantModel(someUserTenantInput));

            // Act
            var result = await userTenantContainer.CreateAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()),
                Times.Once);

            mockTableStorageClient
                .Verify(m => m.InsertAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.Is<UserTenantModel>(u => u.TenantId == someUserTenantInput.Tenant && u.UserId == someUserTenantInput.UserId)),
                Times.Once);

            // Assert
            AssertUserTenantMatchesInput(result);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void CreateHandlesNullUserIdAndReturnsExpectedUserTenant()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync((UserTenantModel)null);

            someUserTenantInput.UserId = null;

            mockTableStorageClient
                .Setup(m => m.InsertAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.Is<UserTenantModel>(u => u.TenantId == someUserTenantInput.Tenant)))
                .ReturnsAsync(new UserTenantModel(someUserTenantInput));

            // Act
            var result = await userTenantContainer.CreateAsync(someUserTenantInput);

            mockTableStorageClient
                .Verify(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()),
                Times.Once);

            mockTableStorageClient
                .Verify(m => m.InsertAsync(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.Is<UserTenantModel>(u => u.TenantId == someUserTenantInput.Tenant && u.UserId == someUserTenantInput.UserId)),
                Times.Once);

            // Assert
            AssertUserTenantMatchesInput(result);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void CreateThrowsWhenUserTenantAlreadyExist()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync(new UserTenantModel(someUserTenantInput));

            // Act
            Func<Task> a = async () => await userTenantContainer.CreateAsync(someUserTenantInput);

            // Assert
            await Assert.ThrowsAsync<StorageException>(a);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void UpdateReturnsExpectedUserTenant()
        {
            // Arrange
            someUserTenantInput = Builder<UserTenantInput>.CreateNew().Set(uti => uti.Roles, JsonConvert.SerializeObject(new[] { "someRole", "someOtherRole" })).Build();

            // Act
            var result = await userTenantContainer.UpdateAsync(someUserTenantInput);

            // Assert
            AssertUserTenantMatchesInput(result);
        }

        public static IEnumerable<object[]> GetRoleLists()
        {
            yield return new object[] { null };
            yield return new object[] { string.Empty };
            yield return new object[] { " " };
        }

        [Theory, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        [MemberData(nameof(GetRoleLists))]
        public async void UpdateDoesNotThrowWhenUserTenantRoleListIsNullOrEmptyOrWhitespace(string roles)
        {
            // Arrange
            someUserTenantInput.Roles = roles;

            // Act
            // Assert
            await userTenantContainer.UpdateAsync(someUserTenantInput);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void DeleteReturnsExpectedUserTenant()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync(new UserTenantModel(someUserTenantInput));

            // Act
            var result = await userTenantContainer.DeleteAsync(someUserTenantInput);

            // Assert
            AssertUserTenantMatchesInput(result);
        }
        
        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void DeleteThrowsWhenUserTenantDoesNotExist()
        {
            // Arrange
            mockTableStorageClient
                .Setup(m => m.RetrieveAsync<UserTenantModel>(
                    It.Is<String>(n => n == userTenantContainer.TableName),
                    It.IsAny<String>(),
                    It.IsAny<String>()))
                .ReturnsAsync((UserTenantModel)null);

            // Act
            Func<Task> a = async () => await userTenantContainer.DeleteAsync(someUserTenantInput);

            // Assert
            await Assert.ThrowsAsync<StorageException>(a);
        }

        [Fact, Trait(Constants.TYPE, Constants.UNIT_TEST)]
        public async void DeleteAllReturnsExpectedUserTenantList()
        {
            // Arrange
            dynamicTableEntities = DynamicTableEntityBuilder
                .CreateListOfSize(dynamicTableEntityCount)
                .All()
                .WithRandomRolesProperty()
                .TheLast(random.Next(0, dynamicTableEntityCount))
                .Set(dte => dte.RowKey, someUserTenantInput.Tenant)
                .BuildList();

            // Act
            var result = await userTenantContainer.DeleteAllAsync(someUserTenantInput);
            
            // Assert
            Assert.Equal("delete", result.batchMethod.ToLowerInvariant());
            Assert.NotNull(result.models);
            Assert.Equal(dynamicTableEntities.Count(dte => dte.PartitionKey == someUserTenantInput.UserId), result.models.Count);
        }

        private void AssertUserTenantMatchesInput(UserTenantModel userTenant)
        {
            Assert.NotNull(userTenant);
            userTenant = new UserTenantModel(someUserTenantInput);
            Assert.Equal(userTenant.Name, userTenant.Name);
            Assert.Equal(userTenant.RoleList, userTenant.RoleList);
            Assert.Equal(userTenant.Roles, userTenant.Roles);
            Assert.Equal(userTenant.TenantId, userTenant.TenantId);
            Assert.Equal(userTenant.Type, userTenant.Type);
            Assert.Equal(userTenant.UserId, userTenant.UserId);
        }
    }
}
