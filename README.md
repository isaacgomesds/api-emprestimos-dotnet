# ApiEmprestimos

🌐 Leia esta documentação em inglês: [README.en.md](./README.en.md)

## Visão Geral

ApiEmprestimos é uma API REST em .NET 10 LTS para gestão de catálogo de livros, usuários e empréstimos de biblioteca. A solução foi desenhada para manter integridade de dados em cenários concorrentes, preservando rastreabilidade de negócio e boa observabilidade operacional.

Stack principal:

- .NET 10 LTS
- ASP.NET Core Web API
- PostgreSQL 17
- Redis
- Docker Compose
- OpenTelemetry + Prometheus
- EF Core + Npgsql

A aplicação expõe endpoints de catálogo, usuários, empréstimos, health checks e métricas, além de um fluxo de auditoria para registrar eventos relevantes do domínio.

## Pré-requisitos e Execução

### Pré-requisitos

- .NET 10 SDK
- Docker + Docker Compose
- PostgreSQL e Redis executando localmente ou via Compose

### Executar com Docker Compose

1. Clone o repositório e navegue até a pasta do projeto:
```bash
git clone https://github.com/isaacgomesds/api-emprestimos-dotnet.git
cd api-emprestimos-dotnet
```

Como alternativa automatizada às etapas manuais abaixo, execute o script na raiz do repositório:

```bash
./setup.sh
```

No Windows PowerShell, use:

```powershell
.\setup.ps1
```

O script cria `.env` a partir de `.env.example` se necessário e executa `docker compose up -d --build`. A API ficará disponível em `http://localhost:5000`.

2. Copie `.env.example` para `.env` antes de executar o Compose:

```bash
# Linux/macOS
cp .env.example .env
```

```powershell
# Windows PowerShell
Copy-Item .env.example .env
```

3. Inicie os serviços, construindo a imagem da API:

```bash
docker compose up --build -d
```

A API ficará disponível em:

- http://localhost:5000
- https://localhost:7000 (quando habilitado por configuração local)

Endpoints principais:

- `GET /users` (lista todos os usuários com id, nome, e-mail e data de criação)
- `POST /users` (cria um usuário)
- `GET /users/{id}/loans` (consulta o histórico de empréstimos do usuário)
- `GET /health/live`
- `GET /health/ready`
- `GET /metrics`
- `POST /loans` (com cabeçalho `Idempotency-Key`)

## Dados de exemplo

O banco é migrado automaticamente ao iniciar a API e inclui estes dados para testes:

**Livros**

- `11111111-1111-1111-1111-111111111111` — The Hobbit
- `11111111-1111-1111-1111-111111111112` — Pride and Prejudice
- `11111111-1111-1111-1111-111111111113` — Nineteen Eighty-Four

**Usuários**

- `22222222-2222-2222-2222-222222222222` — Standard User
- `22222222-2222-2222-2222-222222222223` — Administrator
- `22222222-2222-2222-2222-222222222224` — Librarian

## Migrations e Testes

### EF Core Migrations

Ao iniciar, a API aplica automaticamente as migrations pendentes usando `Database.MigrateAsync()`. Em desenvolvimento, a ferramenta local abaixo permite aplicar migrations manualmente como alternativa a iniciar a API primeiro; não é um passo adicional obrigatório.

```bash
dotnet tool restore
dotnet ef database update --project src/ApiEmprestimos.Infrastructure/ApiEmprestimos.Infrastructure.csproj --startup-project src/ApiEmprestimos.Api/ApiEmprestimos.Api.csproj
```

O manifesto versionado `.config/dotnet-tools.json` fixa `dotnet-ef` em `10.0.13`, compatível com a versão do EF Core usada pelo projeto. Configure `ConnectionStrings__LibraryDatabase` para um banco local de desenvolvimento antes de executar o comando. Não aplique migrations manualmente em produção sem seguir o processo de mudança apropriado.

Para criar uma migration, use `dotnet ef migrations add <NomeDaMigration>` com os mesmos argumentos `--project` e `--startup-project`.

### Testes

```bash
dotnet test
```

A suíte inclui testes unitários e testes de integração com PostgreSQL e Redis em containers para disponibilidade concorrente, devoluções, ISBN duplicado, idempotência, histórico e métricas de empréstimos. Os testes de integração exigem Docker em execução.

### Coleção Postman

Com a API em execução, importe `postman/ApiEmprestimos.postman_collection.json` pelo menu **Import** do Postman. A coleção usa `http://localhost:5000` por padrão; ajuste a variável `baseUrl` da coleção se a API estiver em outro endereço e execute as requisições desejadas.

## Variáveis de Ambiente

Consulte o arquivo `.env.example` para referência de configuração. A estrutura de variáveis esperada inclui, ao menos:

```env
POSTGRES_DB=apiemprestimos
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__LibraryDatabase=Host=postgres;Port=5432;Database=apiemprestimos;Username=postgres;Password=postgres
Redis__ConnectionString=redis:6379
```

Importante:

- não versionar segredos reais;
- manter `ConnectionStrings__LibraryDatabase` e `Redis__ConnectionString` fora do controle de versionamento em ambientes reais;
- usar Secret Manager, variáveis de ambiente do ambiente, ou soluções de secrets do cluster.

## Decisões Arquiteturais

### Concorrência Atômica no PostgreSQL

A criação de empréstimos usa uma estratégia de atualização condicional em PostgreSQL para garantir que o último exemplar disponível não seja compartilhado por duas transações simultâneas. Atualizações de estoque e devoluções também usam expressões atômicas no banco; a mudança do status do empréstimo e o ajuste do estoque de uma devolução/cancelamento participam da mesma transação.

O comportamento central é:

- verificar disponibilidade do livro e validade da operação dentro da mesma transação;
- aplicar `UPDATE` condicional em `Books` com filtro `AvailableCopies > 0`;
- validar o número de linhas alteradas para decidir sucesso ou rejeição;
- persistir somente os campos de metadados alterados, sem sobrescrever estoque lido anteriormente;
- atualizar o estoque com base nos valores atuais da linha, evitando read-modify-write em memória.

Essa abordagem evita inventário negativo e mantém a fonte de verdade em PostgreSQL, sem depender de lógica de cache para a decisão final de disponibilidade.

### Idempotência com Redis (`Idempotency-Key`)

O endpoint de criação de empréstimo aceita o cabeçalho `Idempotency-Key`. Redis coordena requisições concorrentes e acelera o replay, enquanto o hash da requisição e a resposta bem-sucedida são persistidos em PostgreSQL na mesma transação do empréstimo, baixa de estoque e auditoria.

Requisições repetidas com a mesma chave:

- reutilizam a resposta persistida pelo cache Redis ou, em caso de cache miss/expiração, pelo registro durável no PostgreSQL;
- não reexecutam a lógica de negócio;
- não decrementam novamente a quantidade disponível do livro.

Uma restrição única no banco e a inserção condicional do registro idempotente serializam também os casos em que o cache expira ou uma instância reinicia. Se a mesma chave for usada com corpo diferente, a API rejeita com erro de conflito.

### Métricas de negócio

O endpoint `/metrics` expõe estes contadores Prometheus, sem atributos de alta cardinalidade:

- `api_loans_created_total`: empréstimos efetivamente persistidos; incrementa após a transação ser confirmada, não em replay.
- `api_loans_rejected_no_available_copies_total`: tentativas rejeitadas especificamente por falta de exemplares; outros conflitos e erros não incrementam este contador.
- `api_loans_idempotent_replays_total`: requisições repetidas que reutilizam uma resposta armazenada, seja pelo cache Redis ou pelo registro durável no PostgreSQL.

Os contadores são locais ao processo. O Prometheus deve coletar cada réplica e agregar as séries para obter os totais do serviço.

### Cache de leitura e invalidação

Consultas de catálogo e disponibilidade utilizam Redis para reduzir latência de leitura. O middleware de cache acompanha a versão do catálogo e usa chaves versionadas. Ele invalida a versão antes da mutação e novamente após uma resposta bem-sucedida para limitar a janela de respostas desatualizadas. Como PostgreSQL e Redis não compartilham uma transação, uma leitura muito próxima ao commit ainda pode observar uma entrada antiga; a decisão final de disponibilidade sempre usa o PostgreSQL.

Quando ocorrem:

- criação/alteração/desativação de livro;
- empréstimo;
- devolução;
- cancelamento;

o sistema incrementa a versão do catálogo e/ou invalida as chaves relevantes para reduzir a janela de respostas desatualizadas. A decisão de disponibilidade para empréstimos continua sendo feita no PostgreSQL.

### Trilha de Auditoria de Domínio

Todo evento relevante do negócio é registrado em tabela de auditoria, com campos como:

- entidade;
- identificador da entidade;
- ação executada;
- ator;
- timestamp em UTC;
- correlationId;
- payload detalhado em JSON.

Isso preserva uma trilha de negócios útil para auditoria, suporte e investigação operacional, sem depender apenas de logs técnicos.

## Escalabilidade em Kubernetes

Os manifests em `k8s/` incluem Deployment e Service. Para operar com mais de uma réplica, todas devem apontar para o mesmo PostgreSQL e Redis. A integridade do estoque usa atualizações condicionais/atômicas no PostgreSQL; a idempotência também usa índice único e resposta persistida no PostgreSQL, enquanto Redis coordena o caminho rápido e o cache compartilhado.

A suíte de integração contém cenários destinados a validar dois hosts da API compartilhando PostgreSQL e Redis. Ainda precisam ser executados com Docker; quando executados, exercitam invariantes entre hosts no processo de teste, mas não constituem teste de carga nem validam uma capacidade específica de réplicas em produção. A disponibilidade de Redis continua necessária para o middleware atual.

## Limitações Conhecidas e Próximos Passos

### Limitações conhecidas

- A implementação atual de cache é eficiente para leitura de catálogo, mas ainda pode ser refinada para cache mais granular por entidade e por query específica.
- A auditoria foi modelada para registrar eventos do domínio, mas não integra ainda alertas externos ou exportação para data lake.
- O conjunto de métricas e traces atende ao desafio, mas pode ser ampliado para SLOs e dashboards mais sofisticados.
- A solução usa Redis e PostgreSQL em containers locais; para produção, seria recomendado usar serviços gerenciados e secrets padronizados.

### Próximos passos para produção

- mover configurações sensíveis para Secret Manager/Cluster Secret;
- adicionar pipeline CI/CD com build, testes e análise de qualidade;
- utilizar Redis RedisCluster ou gerenciado com TTLs e observabilidade mais refinadas;
- expandir métricas de negócio para taxa de sucesso de empréstimos, rejeições por indisponibilidade e p95 de latência;
- considerar autenticação/autorizações, rate limiting e tracing distribuído entre serviços.

## Conclusão

A implementação concentra as atualizações de estoque e a persistência da idempotência no PostgreSQL, usando Redis para coordenação e cache de leitura. A confirmação dos cenários de concorrência depende da execução dos testes de integração com Docker.
