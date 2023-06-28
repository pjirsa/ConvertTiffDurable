using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using ImageMagick;

namespace ConvertTiffDurable
{
    public class ConvertImages
    {
        private readonly BlobServiceClient _blobClient;

        public ConvertImages(BlobServiceClient blobClient)
        {
            _blobClient = blobClient;
        }

        [Function(nameof(ConvertImages))]
        public async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ConvertImages));
            logger.LogInformation("Processing files.");

            var storageProps = context.GetInput<StorageProperties>();

            // use Activity to fetch list of files for processing
            var fileList = await context.CallActivityAsync<List<string>>(nameof(GetEntries), storageProps);

            // Call ConvertTiff activity for each file
            
            // use fan-out/fan-in pattern, queue the async Activity Function tasks so we can execute in parallel
            var tasks = new List<Task>();
            foreach (var file in fileList)
            {
                tasks.Add(context.CallActivityAsync(nameof(ConvertTiff), new ConvertTiffProperties { FileName = file, StorageProperties = storageProps}));
            }

            // wait for all tasks to process in parallel
            // control max concurrency settings using hosts file - https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-bindings#hostjson-settings
            await Task.WhenAll(tasks);

            // return desired success message or payload
            return new List<string> { "completed" };
        }        

        [Function(nameof(GetEntries))]
        public async Task<List<string>> GetEntries([ActivityTrigger] StorageProperties storageProps, FunctionContext executionContext)
        {
            var containerClient = _blobClient.GetBlobContainerClient(storageProps.SourceContainer);
            var results = new List<string>();
            await foreach(var entry in containerClient.GetBlobsAsync(prefix: storageProps.SourcePath))
            {
                results.Add(entry.Name);
            }
            
            return results;
        }

        [Function(nameof(ConvertTiff))]
        public async Task ConvertTiff([ActivityTrigger] ConvertTiffProperties tiffProperties, FunctionContext executionContext)
        {
            // get blobclient for source
            var sourceBlobClient = _blobClient.GetBlobContainerClient(tiffProperties.StorageProperties.SourceContainer).GetBlobClient(tiffProperties.FileName);
            var sourceFile = await sourceBlobClient.DownloadContentAsync();
            
            // convert image
            var result = ConvertToJpg(sourceFile.Value.Content.ToArray());


            // get blobclient for destination
            var destinationFileName = tiffProperties.FileName.Replace(".tiff", ".jpg").Replace(tiffProperties.StorageProperties.SourcePath, tiffProperties.StorageProperties.DestinationPath);
            
            var destinationBlobClient = 
                _blobClient.GetBlobContainerClient(tiffProperties.StorageProperties.SourceContainer).GetBlobClient(destinationFileName);
            await destinationBlobClient.UploadAsync(new MemoryStream(result), overwrite: true);
        }

        [Function("ConvertImages_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ConvertImages_HttpStart");
            var reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            var storageProperties = JsonConvert.DeserializeObject<StorageProperties>(reqBody);

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ConvertImages),
                storageProperties);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        private byte[] ConvertToJpg(byte[] tiff)
        {
            using var image = new MagickImage(tiff);
            image.Format = MagickFormat.Jpg;
            return image.ToByteArray();
        }
    }

    public class StorageProperties
    {
        public string SourceContainer { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }

    public class ConvertTiffProperties
    {
        public string FileName { get; set; }
        public StorageProperties StorageProperties { get; set; }
    }
}
