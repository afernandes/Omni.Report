# Política de segurança

## Versões suportadas

O OmniReport ainda é pré-1.0. Correções de segurança saem apenas na **última versão publicada** —
não há backport para versões anteriores enquanto o 1.0 não sair.

| Versão | Suportada |
|---|:--:|
| 0.1.x (última) | ✅ |
| anteriores | ❌ |

## Como relatar uma vulnerabilidade

**Não abra issue pública** para vulnerabilidade. Use o
[Private vulnerability reporting](https://github.com/afernandes/Omni.Report/security/advisories/new)
do GitHub, que cria um canal privado entre você e os mantenedores.

Inclua, no que for possível: versão afetada, pacote (`AndersonN.Omni.Report.*`), passos de reprodução ou
prova de conceito, e o impacto que você enxerga. Um `.repx`/`.rdl` mínimo que dispare o problema ajuda mais
que qualquer descrição.

Retorno esperado: confirmação de recebimento em poucos dias úteis e uma avaliação inicial em seguida. Como o
projeto é mantido em tempo parcial, prazos rígidos não seriam honestos — o andamento fica registrado no
próprio advisory.

## Superfície de risco conhecida

Duas áreas merecem atenção explícita de quem hospeda o OmniReport:

### `Reporting.Expressions.Roslyn` executa C# arbitrário — por design

O elemento `Code` de um relatório pode conter C# que este pacote **compila e executa**. Isso é a feature, não
um defeito: é o equivalente ao bloco `<Code>` do SSRS.

A consequência é que **um relatório é código**. Um `.rdl`/`.repx` de origem não confiável — cenário natural
num produto que importa arquivos SSRS — equivale a executar um programa enviado por essa origem. **Não há
sandbox.**

Trate arquivos de relatório com o mesmo cuidado que trataria um plugin ou script:

- o pacote é **opt-in** — não o registre se seus relatórios não usam `Code`;
- se usar, aceite relatórios apenas de origens em que você confia (autoria interna, storage controlado);
- não deixe usuário final subir `.rdl`/`.repx` arbitrário para um host que tenha o Roslyn habilitado.

### Fontes de dados e segredos

Connection strings suportam placeholders `{secret:NOME}` resolvidos por um `ISecretResolver` (por padrão,
variáveis de ambiente). Prefira isso a embutir credencial na definição do relatório — o `.repx`/`.rdl` é um
artefato que costuma ser versionado e compartilhado.

`WebServiceDataSource` e imagem-por-URL fazem requisições de rede a endereços vindos da definição do
relatório. Se as definições não forem confiáveis, isso é SSRF: restrinja egresso na camada de rede.

## O que este projeto faz do seu lado

- **Dependabot** semanal para NuGet e GitHub Actions (`.github/dependabot.yml`).
- **Auditoria de vulnerabilidade** no CI: `dotnet list package --vulnerable` falha o build em severidade
  alta ou crítica e apenas reporta as demais.
- **Build determinístico** com SourceLink e `.snupkg`, publicação por **Trusted Publishing (OIDC)** — sem
  API key de longa duração armazenada.
