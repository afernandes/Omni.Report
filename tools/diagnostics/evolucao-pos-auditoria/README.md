# Probe de paginação e preview

Harness exploratório da evolução pós-auditoria, executado em Windows. Usa somente projetos existentes do repositório e fica fora da solução de produto.

Na raiz:

```powershell
New-Item -ItemType Directory -Path artifacts/evolucao-pos-auditoria-2026-09-09 -Force
dotnet run --project tools/diagnostics/evolucao-pos-auditoria/Measurements -c Release -- artifacts/evolucao-pos-auditoria-2026-09-09/measurements-repro.json
```

Executa 90 casos: cinco fluxos, 1/10/100 páginas, concorrência 1/5, três repetições e aquecimento de um relatório por fluxo. Verifica as páginas produzidas, o valor decimal específico de cada sessão e o máximo de três PNGs no cache.

Amostra o processo a cada 20 ms. Alocação inclui o sampler; LOH é da última coleta observada. As coletas forçadas antes/depois não equivalem a análise de raízes. A ordem dos fluxos é fixa e o aquecimento é curto: não usar como benchmark estatístico nem executar junto de builds.

O relatório da rodada e os dados estão em [docs/evolucao-pos-auditoria-2026-09-09.md](../../../docs/evolucao-pos-auditoria-2026-09-09.md).
