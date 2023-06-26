using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(Environment.GetEnvironmentVariable("StorageConnection"));
        });        
    })
    .Build();

host.Run();
