using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using fiapx_processamento_api.Worker.Infrastructure.Persistence;
using fiapx_processamento_api.Worker.Infrastructure.Repositories;
using fiapx_processamento_api.Worker.Services;
using fiapx_processamento_api.Worker.Workers;
using fiapx_processamento_api.Worker.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AwsS3Settings>(builder.Configuration.GetSection("AWS:S3"));
builder.Services.Configure<AwsSqsSettings>(builder.Configuration.GetSection("AWS:SQS"));
builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// DB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});
builder.Services.AddScoped<IVideoRepository, VideoRepository>();

// AWS clients (supports env vars / profiles; if keys provided in config, use them)
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var s3 = sp.GetRequiredService<IOptions<AwsS3Settings>>().Value;
    var region = RegionEndpoint.GetBySystemName(s3.Region);

    if (!string.IsNullOrWhiteSpace(s3.AccessKey) && !string.IsNullOrWhiteSpace(s3.SecretKey))
        return new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), region);

    return new AmazonS3Client(region);
});

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var sqs = sp.GetRequiredService<IOptions<AwsSqsSettings>>().Value;
    var region = RegionEndpoint.GetBySystemName(sqs.Region);

    if (!string.IsNullOrWhiteSpace(sqs.AccessKey) && !string.IsNullOrWhiteSpace(sqs.SecretKey))
        return new AmazonSQSClient(new BasicAWSCredentials(sqs.AccessKey, sqs.SecretKey), region);

    return new AmazonSQSClient(region);
});

// App services
builder.Services.AddSingleton<IVideoFrameExtractor, FfmpegVideoFrameExtractor>();
builder.Services.AddSingleton<IS3StorageService, S3StorageService>();
builder.Services.AddSingleton<IZipService, ZipService>();
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

builder.Services.AddScoped<IVideoProcessingHandler, VideoProcessingHandler>();

// Worker
builder.Services.AddHostedService<SqsVideoProcessorWorker>();

var host = builder.Build();
host.Run();
