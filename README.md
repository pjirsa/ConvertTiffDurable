# ConvertTiffDurable
Azure Durable function using fan-out pattern to convert a batch of tiff images to jpg (or other formats) using [Magick.NET](https://github.com/dlemstra/Magick.NET).

This function is triggered with an HTTP POST request using the following body:

```JSON
{
    "SourceContainer": "source",
    "SourcePath": "folder_with_tiffs",
    "DestinationPath": "folder_for_jpgs"
}
```

## Prerequisites
- Add Azure Storage Account connection string to local settings file or appsettings of deployed function: `"StorageConnection": "<connection_string_value>"`

## Other Considerations
If you are processing a large number of files, consider controlling parallelization using ['maxConcurrentActivityFunctions'](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-bindings#hostjson-settings)
