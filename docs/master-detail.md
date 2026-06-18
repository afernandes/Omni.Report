# Master-Detail e Sub-Bandas

Este guia explica como configurar relatórios master-detail no OmniReport,
comparando com as engines tradicionais (Crystal Reports, SSRS, FastReport,
DevExpress XtraReports, Stimulsoft).

## Os dois modelos canônicos do mercado

A indústria sedimentou **dois padrões** equivalentes para apresentar dados em
hierarquia pai → filho. O OmniReport suporta os dois — escolha pelo seu modelo
mental.

### 1) Modelo de **grupo** (Crystal Reports / SSRS)

```
PageHeader
GroupHeader1 (chave = Cliente.id)   ← header do agrupamento
  Detail (linha por Pedido)         ← detalhe iterando o filho
GroupFooter1 (subtotal)              ← rodapé do agrupamento
PageFooter
```

- A iteração principal é sobre **Pedidos** (o filho).
- O motor detecta troca de chave do grupo (Cliente.id) e dispara
  `GroupHeader` / `GroupFooter` quando o cliente muda.
- Vantagem: cabe naturalmente uma agregação `Sum(Total, scope:Group)` no
  rodapé do grupo.
- Trade-off: pede mais setup (criar grupo, escolher chave).

### 2) Modelo de **sub-banda** (FastReport / DevExpress / Stimulsoft)

```
PageHeader
Detail (iterando Cliente)               ← banda "master"
  SubDetail "PedidosDeCliente"          ← banda "child" filtrada pela relação
    (linha por Pedido daquele cliente)
PageFooter
```

- A iteração principal é sobre **Clientes** (o pai).
- A sub-banda declara `DataMember = "PedidosDeCliente"` (nome da relação) e
  o motor itera apenas os Pedidos cujo `cliente_id` bate com o cliente atual.
- Vantagem: a topologia da hierarquia é **explícita** — você "vê" a relação na
  estrutura do relatório, sem precisar adivinhar pela chave do grupo.
- Trade-off: agregações no nível do pai precisam ser calculadas via expressão
  (`Sum(Fields.quantidade * Fields.preco_unitario, scope:Group)`).

## Sub-Detail no OmniReport — passo a passo

Este é o modelo usado pelo seed `DB · SQLite` em `samples/Reporting.Samples.BlazorServer/DemoSqliteReport.cs`.

### Configuração mínima

1. **Cadastre as fontes** (pai e filha):
    ```csharp
    state.DataSources.Add(new DesignerDataSource("Clientes", …));
    state.DataSources.Add(new DesignerDataSource("Pedidos", …));
    ```

2. **Declare a relação** entre elas (uma vez só):
    ```csharp
    state.Relations.Add(new DesignerRelation(
        name: "PedidosDeCliente",
        parentSource: "Clientes", parentField: "id",
        childSource:  "Pedidos",  childField:  "cliente_id"));
    ```

3. **Detail** vira a master (itera Clientes — porque Clientes é a primária):
    ```csharp
    var dt = vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(16)));
    dt.AddElement(TextBox(…, expression: "{Fields.nome}"));   // campo do pai
    dt.AddElement(TextBox(…, expression: "{Fields.cidade}")); // campo do pai
    ```

4. **SubDetail** vira a child, apontando para o NOME DA RELAÇÃO:
    ```csharp
    var sd = vm.AddBand(new BandViewModel(DesignerBandKind.SubDetail, Unit.FromMm(6))
    {
        DataMember = "PedidosDeCliente",   // ← chave da resolução
        PrintIfEmpty = false,              // pular Clientes sem pedidos
    });
    sd.AddElement(TextBox(…, expression: "{Fields.produto}"));    // campo do filho
    sd.AddElement(TextBox(…, expression: "{Fields.quantidade}")); // campo do filho
    ```

A ordem das bandas no canvas importa: o `SubDetail` deve vir **imediatamente
após** o `Detail` ao qual está vinculado (e antes do próximo `GroupFooter` /
`PageFooter`).

### Resolução de `DataMember`

O paginator resolve `SubDetail.DataMember` em duas etapas:

1. **Primeira** procura por uma relação com esse nome declarada na fonte primária
   (`primaryDef.Relations`). Bate? Itera o filho filtrando por
   `parentField → childField`.
2. **Senão**, cai para o registro global de fontes. Se `DataMember` for o nome
   de uma fonte registrada, itera **todas** as suas linhas (sem filtro — é uma
   sub-banda solta, raro).

Por isso o NOME DA RELAÇÃO ("PedidosDeCliente") é o que entra em `DataMember`,
não o nome da fonte ("Pedidos").

### Resolução de campos (`Fields.X`)

Dentro do Detail/SubDetail, `{Fields.X}` funciona tanto qualificado quanto não:

- `{Fields.Clientes.nome}` → sempre o pai (qualificação explícita).
- `{Fields.Pedidos.produto}` → sempre o filho (qualificação explícita).
- `{Fields.nome}` → fallback: tenta a linha viva (a fonte da banda atual),
  depois varre as outras fontes ativas. Crystal/SSRS/FastReport fazem isso
  implicitamente via metadata de binding; o OmniReport faz no runtime.

Isso significa: você pode **arrastar um campo do pai dentro do SubDetail** que
ele resolve, e vice-versa.

### `PrintIfEmpty`

Quando `false` (padrão, igual a Crystal/DevExpress): clientes sem pedidos no
período são **omitidos** do relatório.

Quando `true`: o SubDetail ainda renderiza Header/Footer mesmo com zero filhos
— útil para um aviso "Nenhum pedido neste período".

## Comparação rápida com outras engines

| Conceito              | OmniReport            | Crystal           | SSRS                | FastReport            | DevExpress              |
|-----------------------|------------------------|-------------------|---------------------|------------------------|-------------------------|
| Master (pai)          | `Detail` da primária   | Group Header     | Row group           | MasterData             | Detail                  |
| Child (filho)         | `SubDetail`            | Detail            | Child group         | DataBand vinculado     | DetailReportBand        |
| Vínculo               | `DataMember` = relação | Group key         | Relationship        | MasterDataBand        | Source link             |
| Filtro automático     | sim, pela relação      | sim, pela chave   | sim                 | sim                    | sim                     |
| Agregação no pai      | `Sum(..., Group)`      | Group footer      | Group total        | GroupFooter            | GroupFooter             |

## Quando usar grupo vs sub-banda

- Use **grupo** quando a hierarquia é "achatada" — todos os dados vêm de uma
  query única (joins no SQL) e você quer agrupar por uma coluna.
- Use **sub-banda** quando o pai e o filho são fontes **separadas** (queries
  diferentes, repositórios diferentes), o que é o caso real da maioria das
  aplicações de negócio.

O OmniReport otimiza o caso da sub-banda: cada fonte é materializada uma vez
em memória e o filtro `cliente_id == cliente.id` roda in-process. Não há "N+1"
de consultas.
