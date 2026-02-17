# fiapx_processamento_api (Worker .NET)

Microserviço **apartado** responsável por:
- Consumir mensagens da fila **SQS FIFO** (QueueVideo.fifo)
- Baixar o arquivo do **S3**
- Extrair frames (via **ffmpeg**) e gerar um **.zip**
- Subir o zip para o **S3**
- Atualizar o status no **MySQL** (tabela `Videos`)

## Requisitos
- .NET 8 SDK
- Acesso ao MySQL do `VideoUploadDb`
- Acesso à AWS (preferencialmente via variáveis de ambiente / profile)

> ⚠️ NÃO comite AccessKey/SecretKey no repositório.
> Use variáveis de ambiente (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`) ou profiles do AWS CLI.

## Configuração
Edite `src/fiapx_processamento_api.Worker/appsettings.json`:

- `ConnectionStrings:DefaultConnection`
- `AWS:S3` e `AWS:SQS`
- `Worker:*` (concorrência e long polling)
- `Email:*` (opcional)

## Executar local
```bash
cd src/fiapx_processamento_api.Worker
dotnet run
```

## Docker
```bash
docker build -t fiapx_processamento_api .
docker run --rm -e AWS_ACCESS_KEY_ID=... -e AWS_SECRET_ACCESS_KEY=... -e AWS_REGION=us-east-2 fiapx_processamento_api
```

## Sobre notificação por e-mail
O envio por SMTP está implementado, mas **desabilitado por padrão** (`Email:Enabled=false`).

Para enviar para o e-mail real do usuário, você pode:
1. Gravar o e-mail (ou userId) no registro de `Video` no upload; ou
2. Integrar aqui uma chamada para a `fiapx_usuario_api` para resolver `userId -> email`.

No código atual, o destinatário está como placeholder `user@example.com` para não assumir o contrato de usuário.

## Idempotência / não perder requisições
- Se o vídeo já estiver `Processed`, o worker deleta a mensagem (idempotência).
- Em erro, o worker **não deleta a mensagem** (SQS fará retry). Recomenda-se configurar **DLQ** na infra.
