# Testes de integração dos conectores SQL

Os três conectores de produção — `SqlServer`, `PostgreSql` e `MySql` — não eram referenciados por
nenhum projeto de teste. São o tipo de shim "óbvio demais para errar" onde erro de mapeamento de tipo
custa caro e passa despercebido: um `DBNull` vazando, um `decimal` virando `double`, uma data perdendo
a hora.

Aqui eles rodam contra os bancos **de verdade**, em contêiner (Testcontainers).

## Como rodar

Precisa de um daemon Docker em modo Linux. Com ele de pé, basta:

```bash
dotnet test tests/Reporting.DataSources.Integration.Tests
```

Na primeira execução o pull das três imagens leva alguns minutos; depois ficam em cache.

Sem Docker, os testes **se pulam** com a razão no log, em vez de falhar — uma suíte vermelha por
ausência de ferramenta deixa de ser lida. Para pular mesmo tendo Docker:

```bash
OMNIREPORT_SKIP_DB_TESTS=1 dotnet test tests/Reporting.DataSources.Integration.Tests
```

## Como rodam no CI

Só no workflow [`db-integration.yml`](../../.github/workflows/db-integration.yml), que dispara em PR
que toca conector (ou o shim `AdoNet` que os três compartilham), em push na `main`, semanalmente, e sob
demanda. O gate é a variável `OMNIREPORT_DB_TESTS`, checada no
[`DockerFactAttribute`](DockerFactAttribute.cs): em CI, sem ela, os testes se pulam.

Isso é deliberado. O job Linux do `ci.yml` deriva a lista de projetos automaticamente, então sem esse
gate ele passaria a puxar três imagens de banco em todo PR. Manter a decisão no atributo — e não no
workflow — deixa o comportamento independente de como cada job escolhe seus projetos.

O workflow ainda **falha se algum teste for pulado**. Um job cuja única razão de existir é rodar contra
banco real não pode ficar verde tendo pulado tudo; sem essa checagem, perder o Docker no runner viraria
um verde silencioso.

## O que é verificado

Um contrato só, executado contra os três motores — eles são o mesmo shim três vezes, então uma cópia
por motor iria divergir, e um comportamento que só um deles acerta é justamente o bug que interessa.

| Verificação | Por quê |
|---|---|
| Abre conexão e lê todas as linhas | o básico, que ninguém cobria |
| Schema com nomes na ordem | `IndexOf` de coluna inexistente deve ser `-1`, não exceção |
| Nulo vira `null`, não `DBNull` | `DBNull` não é nulo: toda checagem a jusante falha calada e o valor renderiza como `System.DBNull` |
| `decimal` preserva tipo e escala | chegando como `double`, `1450.90` vira `1450.8999999999999` num relatório financeiro |
| `DateTime` preserva a hora | truncar para meia-noite é silencioso e só aparece no relatório |
| GUID vira `Guid` | cada motor soletra diferente (`UNIQUEIDENTIFIER`, `UUID`, `CHAR(36)`) e o MySQL só mapeia com opt-in na connection string; como string, compara e formata errado parecendo plausível |
| Booleano vira `bool` | MySQL não tem boolean — `TINYINT(1)` é a convenção, e o driver precisa mapear |
| Parâmetro liga por nome | os três usam prefixo `@` |
| Leitura preguiçosa | quem bufferiza tudo passa numa contagem de linhas e estoura a memória numa tabela real |
| Cancelamento interrompe | `CancellationToken` ignorado só aparece em produção, sob carga |

As imagens são **pinadas por tag** — com `latest`, a suíte mudaria de comportamento sem nenhum commit:

| Motor | Imagem |
|---|---|
| SQL Server | `mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04` |
| PostgreSQL | `postgres:17.2-alpine` |
| MySQL | `mysql:8.4.3` |

## Achado da primeira execução

`AdoNetDataSource` delegava o cancelamento inteiramente ao token passado para
`DbDataReader.ReadAsync`. Quando o driver já tem as linhas em buffer local, esse método retorna de
forma **síncrona sem consultar o token** — então o cancelamento era honrado por acidente de driver: o
SqlClient consulta, o Npgsql e o MySqlConnector não. Um render cancelado continuava drenando o
resultado inteiro no PostgreSQL e no MySQL.

Os três motores rodando o mesmo teste foi o que tornou isso visível: o mesmo código passava em um e
falhava em dois. Corrigido com uma checagem explícita por linha.
