# fiapx_processamento_api

Microserviço **apartado** responsável por:
- Consumir mensagens da fila **SQS FIFO** (QueueVideo.fifo)
- Baixar o arquivo do **S3**
- Extrair frames (via **ffmpeg**) e gerar um **.zip**
- Subir o zip para o **S3**
- Atualizar o status no **MySQL** (tabela `Videos`)

## Executar localmente
```bash
cd src/fiapx_processamento_api.Worker
dotnet run
```

## Executar via Docker
```bash
docker build -t fiapx_processamento_api .
docker run --rm -e AWS_ACCESS_KEY_ID=... -e AWS_SECRET_ACCESS_KEY=... -e AWS_REGION=... fiapx_processamento_api
```

## Sobre notificação por e-mail
O envio por SMTP está implementado, mas **desabilitado por padrão** (`Email:Enabled=false`).

Para enviar para o e-mail real do usuário, seguir o passo a passo abaixo:
1. Gravar o e-mail (ou userId) no registro de `Video` no upload; ou
2. Integrar aqui uma chamada para a `fiapx_usuario_api` para resolver `userId -> email`.

No código atual, o destinatário está como placeholder `user@example.com` para não assumir o contrato de usuário.

## Idempotência / não perder requisições
- Se o vídeo já estiver `Processed`, o worker deleta a mensagem (idempotência).
- Em erro, o worker **não deleta a mensagem** (SQS fará retry).
