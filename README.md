# PRAXIS — Backend API

Backend SaaS B2B Multi-tenant para Gestão de Responsabilidade Técnica e Operações de Empresas de Nutrição.

## Tecnologias
- **.NET 8** / ASP.NET Core Web API
- **Entity Framework Core 8** (PostgreSQL & SQLite)
- **Cloudflare R2 & AWSSDK.S3** (Armazenamento de imagens, documentos e laudos via presigned URLs)
- **QuestPDF** (Relatórios técnicos em PDF de visitas)
- **JWT Bearer Authentication**
- **Swagger / OpenAPI** com suporte a Bearer Token

---

## ☁️ Armazenamento em Nuvem — Cloudflare R2

O PRAXIS utiliza o **Cloudflare R2** (compatível com a API S3) para armazenar fotos de vistorias, documentos e laudos de consultoria.

### 🔒 Variáveis de Ambiente Necessárias:
No ambiente local (`appsettings.json` / variáveis de ambiente) ou no **Render**:

```env
R2__AccountId=your_account_id
R2__AccessKey=your_access_key
R2__SecretKey=your_secret_key
R2__BucketName=file-praxis-sandbox
```

> **IMPORTANTE**: Nenhuma credencial do R2 deve ser incluída no código-fonte ou versionada no Git.

### 📐 Estrutura de ObjectKeys:
- Fotos de Clientes: `tenants/{tenantId}/clients/{clientId}/photos/{year}/{fileId}.{ext}`
- Relatórios e Laudos: `tenants/{tenantId}/clients/{clientId}/reports/{year}/{fileId}.pdf`
- Evidências Técnicas: `tenants/{tenantId}/general/evidences/{year}/{fileId}.{ext}`
- Documentos Gerais: `tenants/{tenantId}/general/documents/{year}/{fileId}.{ext}`

### 🔄 Fluxo de Upload Direto (Presigned PUT):
1. **Frontend solicita URL**: `POST /api/files/upload-url` informando `{ fileName, contentType, size, category, clientId? }`.
2. **Backend valida e assina**: Valida tipo/tamanho/tenant, gera o `ObjectKey` seguro, cria o registro em status `Pending` e retorna a presigned PUT URL (válida por 15 min).
3. **Frontend envia direto para o R2**: Faz `PUT` para a URL retornada com o cabeçalho `Content-Type` exato.
4. **Confirmação**: Frontend chama `POST /api/files/{id}/complete`. O backend verifica a existência do objeto no R2 e atualiza o status para `Uploaded`.

### 📥 Fluxo de Download/Visualização Privada (Presigned GET):
1. **Frontend solicita URL de download**: `GET /api/files/{id}/download-url`.
2. **Backend autoriza**: Valida autenticação, tenant e permissões do usuário e gera uma presigned GET URL temporária (15 min).
3. **Frontend exibe/baixa**: Acessa diretamente a URL temporária fornecida.

---

## Execução Rápida (Local)

Para rodar a API localmente:

```bash
cd backend/src/Praxis.Api
dotnet run
```

A API estará disponível em:
- Swagger UI: `http://localhost:5000` (ou porta informada no terminal)

### Cadastro de Empresas:
- Crie novas contas de consultoria através do endpoint `POST /api/auth/register-tenant`.

## Execução via Docker Compose (com PostgreSQL)

```bash
cd backend
docker-compose up -d --build
```
