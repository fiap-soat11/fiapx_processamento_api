using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.SQS;
using fiapx_processamento_api.Worker.Infrastructure.Persistence;
using fiapx_processamento_api.Worker.Services;
using fiapx_processamento_api.Worker.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração de porta / health endpoint
builder.WebHost.UseUrls("http://0.0.0.0:8080");

// Banco
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// AWS
var awsRegion =
    builder.Configuration["AWS:Region"]
    ?? builder.Configuration["AWS:SQS:Region"]
    ?? builder.Configuration["AWS:S3:Region"]
    ?? Environment.GetEnvironmentVariable("AWS_REGION")
    ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
    ?? "us-east-2";

var region = RegionEndpoint.GetBySystemName(awsRegion);

var awsCredentials = ResolveAwsCredentials(builder.Configuration, builder.Environment);

builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(awsCredentials, region));
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(awsCredentials, region));

// Serviços da aplicação
builder.Services.AddScoped<IVideoProcessingHandler, VideoProcessingHandler>();
builder.Services.AddScoped<IS3StorageService, S3StorageService>();
builder.Services.AddScoped<IVideoFrameExtractor, FfmpegVideoFrameExtractor>();
builder.Services.AddScoped<IZipFileService, ZipFileService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Worker
builder.Services.AddHostedService<SqsVideoProcessorWorker>();

var app = builder.Build();

// Healthcheck simples para Docker/K8s
app.MapGet("/api/Health", () => Results.Ok(new { status = "ok" }));

app.Run();

static AWSCredentials ResolveAwsCredentials(IConfiguration configuration, IWebHostEnvironment environment)
{
    // 1) appsettings.json / appsettings.{Environment}.json
    var accessKey =
        configuration["AWS:AccessKey"]
        ?? configuration["AWS:SQS:AccessKey"]
        ?? configuration["AWS:S3:AccessKey"];

    var secretKey =
        configuration["AWS:SecretKey"]
        ?? configuration["AWS:SQS:SecretKey"]
        ?? configuration["AWS:S3:SecretKey"];

    var sessionToken =
        configuration["AWS:SessionToken"]
        ?? configuration["AWS:Session_Token"]
        ?? configuration["AWS:SQS:SessionToken"]
        ?? configuration["AWS:SQS:Session_Token"]
        ?? configuration["AWS:S3:SessionToken"]
        ?? configuration["AWS:S3:Session_Token"];

    if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
    {
        return !string.IsNullOrWhiteSpace(sessionToken)
            ? new SessionAWSCredentials(accessKey, secretKey, sessionToken)
            : new BasicAWSCredentials(accessKey, secretKey);
    }

    // 2) Variáveis de ambiente
    accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");

    if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
    {
        return !string.IsNullOrWhiteSpace(sessionToken)
            ? new SessionAWSCredentials(accessKey, secretKey, sessionToken)
            : new BasicAWSCredentials(accessKey, secretKey);
    }

    // 3) ~/.aws/credentials com profile
    var profileName =
        Environment.GetEnvironmentVariable("AWS_PROFILE")
        ?? configuration["AWS:Profile"]
        ?? "default";

    try
    {
        var chain = new CredentialProfileStoreChain();
        if (chain.TryGetAWSCredentials(profileName, out var profileCredentials))
        {
            return profileCredentials;
        }
    }
    catch
    {
        // segue para fallback
    }

    // 4) Fallback padrão do SDK
    return FallbackCredentialsFactory.GetCredentials();
}
