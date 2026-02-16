using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using fiapx_processamento_api.Worker.Configuration;
using fiapx_processamento_api.Worker.Services;

namespace fiapx_processamento_api.Worker.Workers;

public sealed class SqsVideoProcessorWorker : BackgroundService
{
    private readonly ILogger<SqsVideoProcessorWorker> _logger;
    private readonly IAmazonSQS _sqs;
    private readonly IServiceProvider _sp;
    private readonly AwsSqsSettings _sqsSettings;
    private readonly WorkerSettings _workerSettings;

    public SqsVideoProcessorWorker(
        ILogger<SqsVideoProcessorWorker> logger,
        IAmazonSQS sqs,
        IServiceProvider sp,
        IOptions<AwsSqsSettings> sqsSettings,
        IOptions<WorkerSettings> workerSettings)
    {
        _logger = logger;
        _sqs = sqs;
        _sp = sp;
        _sqsSettings = sqsSettings.Value;
        _workerSettings = workerSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado. QueueUrl={QueueUrl}", _sqsSettings.QueueUrl);

        using var semaphore = new SemaphoreSlim(Math.Max(1, _workerSettings.MaxConcurrency));

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse resp;

            try
            {
                resp = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _sqsSettings.QueueUrl,
                    MaxNumberOfMessages = Math.Clamp(_workerSettings.MaxMessagesPerPoll, 1, 10),
                    WaitTimeSeconds = Math.Clamp(_workerSettings.WaitTimeSeconds, 0, 20),
                    VisibilityTimeout = Math.Max(30, _workerSettings.VisibilityTimeoutSeconds)
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao receber mensagens da SQS.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            if (resp.Messages.Count == 0)
                continue;

            foreach (var msg in resp.Messages)
            {
                await semaphore.WaitAsync(stoppingToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _sp.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<IVideoProcessingHandler>();

                        var shouldDelete = await handler.HandleAsync(msg.Body, stoppingToken);

                        if (shouldDelete)
                        {
                            await _sqs.DeleteMessageAsync(_sqsSettings.QueueUrl, msg.ReceiptHandle, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro inesperado ao processar mensagem.");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken);
            }
        }

        _logger.LogInformation("Worker finalizado.");
    }
}
