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

### Executar localmente

1. Clone o repositório e navegue até a pasta do projeto:
```bash
git clone https://github.com/isaacgomesds/api-emprestimos-dotnet.git
cd api-emprestimos-dotnet
```

Crie um arquivo `.env` com as variáveis esperadas pelo Compose (exemplo: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`).

```bash
docker compose up -d
```

A API ficará disponível em:

- http://localhost:5000
- https://localhost:7000 (quando habilitado por configuração local)

Endpoints principais:

- `GET /health/live`
- `GET /health/ready`
- `GET /metrics`
- `POST /loans` (com cabeçalho `Idempotency-Key`)

## Migrations e Testes

### EF Core Migrations

```bash
dotnet ef database update --project src/ApiEmprestimos.Infrastructure --startup-project src/ApiEmprestimos.Api
```

Se a ferramenta do EF não estiver instalada globalmente:

```bash
dotnet tool restore
dotnet ef database update --project src/ApiEmprestimos.Infrastructure --startup-project src/ApiEmprestimos.Api
```

### Testes

```bash
dotnet test
```

A suíte cobre testes unitários, testes de integração com PostgreSQL e Redis em containers, cenário de concorrência para o último exemplar e idempotência de empréstimos.

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

A criação de empréstimos usa uma estratégia de atualização condicional em PostgreSQL para garantir que o último exemplar disponível não seja compartilhado por duas transações simultâneas.

O comportamento central é:

- verificar disponibilidade do livro e validade da operação dentro da mesma transação;
- aplicar `UPDATE` condicional em `Books` com filtro `AvailableCopies > 0`;
- validar o número de linhas alteradas para decidir sucesso ou rejeição.

Essa abordagem evita inventário negativo e mantém a fonte de verdade em PostgreSQL, sem depender de lógica de cache para a decisão final de disponibilidade.

### Idempotência com Redis (`Idempotency-Key`)

O endpoint de criação de empréstimo aceita o cabeçalho `Idempotency-Key` e armazena a resposta bem-sucedida em Redis com chave derivada da chave informada.

Requisições repetidas com a mesma chave:

- reutilizam a resposta em cache;
- não reexecutam a lógica de negócio;
- não decrementam novamente a quantidade disponível do livro.

Se a mesma chave for usada com corpo diferente, a API rejeita com erro de conflito, preservando a semântica de idempotência segura.

### Cache de leitura e invalidação

Consultas de catálogo e disponibilidade utilizam Redis para reduzir latência de leitura. O middleware de cache acompanha a versão do catálogo e usa chaves versionadas para evitar inconsistência entre respostas antigas e dados atuais.

Quando ocorrem:

- criação/alteração/desativação de livro;
- empréstimo;
- devolução;
- cancelamento;

o sistema incrementa a versão do catálogo e/ou invalida as chaves relevantes, mantendo as leituras consistentes com o estado atual do domínio.

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

Os manifests em `k8s/` incluem Deployment e Service para a API. A aplicação foi projetada para operar corretamente em múltiplas réplicas, mantendo as regras de concorrência e idempotência mesmo com escala horizontal.

### Comportamento seguro entre 2 e 11 réplicas

- PostgreSQL continua sendo a fonte de verdade para estoque real.
- O lock de disponibilidade é atômico na base de dados, reduzindo risco de race condition entre pods.
- Redis armazena as respostas idempotentes e dados de cache compartilhados entre as réplicas.
- A leitura e a escrita do cache ficam versionadas para evitar resposta desatualizada após alteração de catálogo.
- Health checks e probes garantem que apenas pods prontos recebam tráfego.

Com 2 a 11 réplicas, a solução permanece segura porque a decisão de empréstimo bem-sucedido depende de um controle concorrente consistente no banco, não apenas do estado local de uma instância.

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

A solução balanceia consistência transacional, idempotência, cache de leitura e rastreabilidade de domínio. Essa combinação é essencial para APIs de empréstimo em ambientes com múltiplas réplicas e concorrência real.
