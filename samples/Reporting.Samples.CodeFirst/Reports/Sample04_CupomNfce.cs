using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Cupom NFC-e em bobina térmica 80mm. Demonstra: papel contínuo, layout estreito,
/// agrupamento implícito sem detail (todos elementos no header/footer), formato monoespaçado
/// no detalhe. NOTA: este é um cupom de demonstração visual — NÃO substitui a emissão fiscal
/// real, que requer assinatura digital + transmissão à SEFAZ.
/// </summary>
public static class Sample04_CupomNfce
{
    public static Report Build(IEnumerable<NfceItem>? itens = null) =>
        ReportBuilder
            .Create("Cupom NFC-e (demo)")
            .Page(p => p.Thermal80().Margins(2))
            .DataSource("Itens", itens ?? NfceSampleData.Itens())
            .ReportHeader(h => h.Height(45)
                // Identificação do emitente
                .Text(NfceSampleData.NomeEmitente)
                    .At(0, 0).Size(76, 5).Center().Bold().Font("Consolas", 9)
                .Text("CNPJ: " + NfceSampleData.CnpjEmitente)
                    .At(0, 5).Size(76, 4).Center().Font("Consolas", 8)
                .Text(NfceSampleData.EnderecoEmitente)
                    .At(0, 9).Size(76, 8).Center().Font("Consolas", 7)
                .Line().From(0, 18).To(76, 18).Thickness(0.5)
                // Título
                .Text("Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica")
                    .At(0, 20).Size(76, 8).Center().Font("Consolas", 7)
                .Line().From(0, 30).To(76, 30).Thickness(0.5)
                // Cabeçalho da tabela de itens
                .Text("# Cód  Descrição")
                    .At(0, 32).Size(76, 4).Bold().Font("Consolas", 8)
                .Text("Qtd UN x  Vl.Unit    Total")
                    .At(0, 37).Size(76, 4).Bold().Font("Consolas", 8)
                .Line().From(0, 42).To(76, 42).Thickness(0.25))
            .Detail(d => d.Height(9)
                .Text("{Fields.Numero:000} {Fields.Codigo} {Fields.Descricao}")
                    .At(0, 0).Size(76, 4).Font("Consolas", 8)
                .Text("    {Fields.Quantidade:N3} {Fields.Unidade} x {Fields.ValorUnitario:N2}")
                    .At(0, 4).Size(48, 4).Font("Consolas", 8)
                .Text("{Fields.ValorTotal:N2}")
                    .At(48, 4).Size(28, 4).AlignRight().Bold().Font("Consolas", 8))
            .ReportFooter(f => f.Height(60)
                .Line().From(0, 0).To(76, 0).Thickness(0.5)
                .Text("VALOR TOTAL R$")
                    .At(0, 2).Size(40, 6).Bold().Font("Consolas", 10)
                .Text("{Sum(Fields.ValorTotal):N2}")
                    .At(40, 2).Size(36, 6).AlignRight().Bold().Font("Consolas", 10)
                .Text("Forma de pagamento: Cartão Débito")
                    .At(0, 10).Size(76, 4).Font("Consolas", 8)
                .Line().From(0, 16).To(76, 16).Thickness(0.25)
                // QR Code seria gerado como Barcode(QrCode) — placeholder visual aqui
                .Text("Consulte pela chave de acesso:")
                    .At(0, 18).Size(76, 4).Center().Font("Consolas", 7)
                .Text(FormatChaveAcesso(NfceSampleData.ChaveAcesso))
                    .At(0, 22).Size(76, 8).Center().Font("Consolas", 8).Bold()
                .Text("Protocolo: " + NfceSampleData.ProtocoloAutorizacao)
                    .At(0, 32).Size(76, 4).Center().Font("Consolas", 7)
                .Text("Tributos totais (Lei 12.741/12): R$ {Sum(Fields.ValorTotal) * 0.18:N2}")
                    .At(0, 38).Size(76, 4).Center().Font("Consolas", 7).Color(Color.Gray)
                .Line().From(0, 44).To(76, 44).Thickness(0.25)
                .Text("Emitido por OmniReport · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                    .At(0, 46).Size(76, 4).Center().Font("Consolas", 7).Color(Color.Gray))
            .Build();

    /// <summary>Formata a chave de acesso de 44 dígitos em blocos de 4 (padrão NF-e).</summary>
    private static string FormatChaveAcesso(string chave)
    {
        if (chave.Length != 44)
        {
            return chave;
        }
        var sb = new System.Text.StringBuilder(54);
        for (int i = 0; i < 44; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append(' ');
            }
            sb.Append(chave[i]);
        }
        return sb.ToString();
    }
}
