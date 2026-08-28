# PRAXIS — Backend API

Backend SaaS B2B Multi-tenant para Gestão de Responsabilidade Técnica e Operações de Empresas de Nutrição.

## Tecnologias
- **.NET 8** / ASP.NET Core Web API
- **Entity Framework Core 8** (PostgreSQL & SQLite)
- **QuestPDF** (Relatórios técnicos em PDF de visitas)
- **JWT Bearer Authentication**
- **Swagger / OpenAPI** com suporte a Bearer Token

## Execução Rápida (Local)

Para rodar a API localmente:

```bash
cd backend/src/Praxis.Api
dotnet run
```

A API estará disponível em:
- Swagger UI: `http://localhost:5000` (ou porta informada no terminal)
- Banco de dados inicializado automaticamente com dados de demonstração (Tenant: NutriVida Assessoria).

### Credenciais de Teste Pré-configuradas:
- **Admin**: `admin@nutrivida.com` / `Praxis@123`
- **Nutricionista**: `carla.nutri@nutrivida.com` / `Praxis@123`

## Execução via Docker Compose (com PostgreSQL)

```bash
cd backend
docker-compose up -d --build
```
