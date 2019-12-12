using Mmm.Platform.IoT.Common.Services.External.StorageAdapter;

namespace Mmm.Platform.IoT.AsaManager.Services.Models
{
    public class ConversionApiModel
    {
        public string BlobFilePath { get; set; }
        public string TenantId { get; set; }
        public string OperationId { get; set; }
        public ValueListApiModel Entities { get; set; }

        public ConversionApiModel()
        {
        }
    }
}