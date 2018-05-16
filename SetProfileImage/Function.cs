using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SetProfileImage
{
    public class Function
    {
        private readonly AmazonS3Client _s3client;

        private string metadataKey = "x-amz-meta-user-profile-id";

        public Function()
        {
            _s3client = new AmazonS3Client();
        }
        
        public async Task FunctionHandler(S3Event @event, ILambdaContext context)
        {
            context.Logger.Log("START");

            context.Logger.Log("S3Event: " + JsonConvert.SerializeObject(@event));

            foreach(var record in @event.Records)
            {
                context.Logger.Log($"Processing file {record.S3.Bucket.Name}:{record.S3.Object.Key}.");

                var metadata = await _s3client.GetObjectMetadataAsync(record.S3.Bucket.Name, record.S3.Object.Key);

                context.Logger.Log("Metadata: " + JsonConvert.SerializeObject(metadata));

                if(metadata.Metadata.Keys.Contains(metadataKey))
                {
                    var value = Convert.ToInt32(metadata.Metadata[metadataKey]);

                    context.Logger.Log($"{metadataKey} = {value}");

                    await UpdateDatabase(value, record.S3.Object.Key);
                }
            }

            context.Logger.Log("END");
        }

        public async Task UpdateDatabase(int id, string fileName)
        {
            IAmazonDynamoDB client = new AmazonDynamoDBClient();
            DynamoDBContext context = new DynamoDBContext(client);

            var table = Table.LoadTable(client, "user-profile");
            var item = await table.GetItemAsync(id);

            item["image"] = fileName;

            await table.PutItemAsync(item);
        }
    }
}
