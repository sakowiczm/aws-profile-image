using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.Serialization.Json;
using Amazon.S3;
using Amazon.S3.Model;
using TinyPng;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CreateThumbnail
{
    public class Function
    {
        private string _metadataKey = "x-amz-meta-user-profile-id";
        private readonly string _imageType = ".jpg";
        private readonly AmazonS3Client _s3Client;
        private static readonly JsonSerializer _jsonSerializer = new JsonSerializer();

        public Function()
        {
            _s3Client = new AmazonS3Client();
        }

        public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
        {
            context.Logger.Log("START");

            foreach (var record in dynamoEvent.Records)
            {
                context.Logger.Log($"Event ID: {record.EventID}");
                context.Logger.Log($"Event Name: {record.EventName}");
                string streamRecordJson = SerializeObject(record.Dynamodb);
                Console.WriteLine($"DynamoDB Record:");
                Console.WriteLine(streamRecordJson);                

                if(!record.Dynamodb.NewImage.ContainsKey("image") || !record.Dynamodb.Keys.ContainsKey("id"))
                {
                    context.Logger.Log("Missing data.");
                    continue;
                }

                string id = record.Dynamodb.Keys["id"].N;
                string filePath = record.Dynamodb.NewImage["image"].S;

                string fileName = Path.GetFileName(filePath);

                context.Logger.Log($"Record id: {id}");
                context.Logger.Log($"File name: {fileName}");
                
                if (_imageType != Path.GetExtension(fileName).ToLower())
                {
                    context.Logger.Log($"Not a supported image type");
                    continue;
                }

                try
                {
                    string bucketName = Environment.GetEnvironmentVariable("BucketName");
                    var tinyPngKey = Environment.GetEnvironmentVariable("TinyPngKey");

                    using (var objectResponse = await _s3Client.GetObjectAsync(bucketName + "/original", fileName))
                    using (Stream responseStream = objectResponse.ResponseStream)
                    {
                        TinyPngClient tinyPngClient = new TinyPngClient(tinyPngKey);

                        using (var downloadResponse = await tinyPngClient.Compress(responseStream).Resize(150, 150).GetImageStreamData())
                        {
                            var putRequest = new PutObjectRequest
                            {
                                BucketName = bucketName + "/thumbnails",
                                Key = fileName,
                                InputStream = downloadResponse,
                                TagSet = new List<Tag>
                                {
                                    new Tag
                                    {
                                        Key = "Thumbnail", Value = "true"
                                    },
                                },
                            };

                            putRequest.Metadata.Add(_metadataKey, id);

                            await _s3Client.PutObjectAsync(putRequest);
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.Log($"Exception: {ex}");

            // catch (AmazonS3Exception amazonS3Exception)
            // {
            //     if (amazonS3Exception.ErrorCode != null &&
            //         (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
            //         ||
            //         amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
            //     {
            //         Console.WriteLine("Check the provided AWS Credentials.");
            //         Console.WriteLine(
            //             "For service sign up go to http://aws.amazon.com/s3");
            //     }
            //     else
            //     {
            //         Console.WriteLine(
            //             "Error occurred. Message:'{0}' when writing an object"
            //             , amazonS3Exception.Message);
            //     }
            // }

                }


            }

            context.Logger.Log("END");
        }

        private string SerializeObject(object streamRecord)
        {
            using (var ms = new MemoryStream())
            {
                _jsonSerializer.Serialize(streamRecord, ms);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }        

    }
}

