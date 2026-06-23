# RDL 2016/01 — XML Schema (oficial, vendorizado)

`ReportDefinition.xsd` é o **schema oficial da Microsoft** para o RDL versão **2016/01**
(namespace `http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition`).

Usado por `RdlXsdValidationTests` para garantir que todo `.rdl` produzido pelo `RdlExporter`
é **válido pelo XSD** — a garantia real de "abre no SQL Server Reporting Services / Report Builder",
além do round-trip por valor.

## Proveniência

A Microsoft **não publica** o XSD do 2016/01 em URL limpa (apenas até 2010/01); o 2016/01 é servido
pelo report server ou consta na especificação **[MS-RDL]**. Este arquivo foi extraído da seção
*"RDL XML Schema for Version 2016/01"* do PDF normativo [MS-RDL] (Microsoft Open Specifications).

## Ajustes aplicados (mínimos, documentados)

Para que o schema **compile** no `XmlSchemaSet` do .NET a partir do texto do PDF:

1. **Bloco `<xsd:annotation>` removido** — é documentação livre (texto/aspas), não afeta validação.
2. **Quebras de linha dentro de valores de atributo** (artefato de extração do PDF) colapsadas —
   URIs/QNames do XSD nunca contêm espaço interno.
3. **Extensões 2016 em sub-namespaces neutralizadas** (2 declarações `xmlns` inline em
   `DefaultFontFamily` e `AuthoringMetadata`, que apontam para `.../reportdefinition/<sub>`):
   sem os schemas-companheiros elas não compilam, e o OmniReport **nunca emite** esses elementos
   opcionais — removendo o `xmlns` inline, resolvem no namespace principal.

Nenhuma regra estrutural relevante (content models, tipos, cardinalidades) foi alterada — o que o
OmniReport emite é validado contra o schema oficial.
