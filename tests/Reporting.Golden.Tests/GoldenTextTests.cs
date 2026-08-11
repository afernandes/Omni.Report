using FluentAssertions;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// The golden formats are line-oriented and quote report text, so escaping is what keeps a value
/// from breaking the file's structure. Cheap to test directly, and the failure it prevents (a golden
/// silently going ambiguous) is expensive to notice later.
/// </summary>
public class GoldenTextTests
{
    [Theory]
    [InlineData("simples", "\"simples\"")]
    [InlineData("Aspas \"X\"", "\"Aspas \\\"X\\\"\"")]
    [InlineData("barra \\ solta", "\"barra \\\\ solta\"")]
    [InlineData("duas\nlinhas", "\"duas\\nlinhas\"")]
    [InlineData("com\ttab", "\"com\\ttab\"")]
    [InlineData("crlf\r\naqui", "\"crlf\\r\\naqui\"")]
    public void Quotes_and_escapes(string input, string expected)
        => GoldenText.Quote(input).Should().Be(expected);

    [Fact]
    public void Backslash_is_escaped_before_the_quote_it_precedes()
    {
        // Ordem importa: escapar a aspa antes da barra produziria \\" — barra escapada seguida de
        // aspa CRUA, que fecha o token no meio do valor. O correto é \\ seguido de \".
        GoldenText.Quote(@"a\""b").Should().Be("\"a\\\\\\\"b\"");
    }

    [Fact]
    public void Result_never_contains_a_raw_newline_or_unescaped_quote()
    {
        var quoted = GoldenText.Quote("qualquer \"coisa\"\ncom\r\nquebras \\ e barras");

        quoted.Should().NotContain("\n").And.NotContain("\r",
            "uma quebra crua dividiria a primitiva em duas linhas do golden");
        // Fora das aspas delimitadoras, toda aspa interna tem de vir precedida de barra.
        var inner = quoted[1..^1];
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '"')
            {
                i.Should().BeGreaterThan(0);
                inner[i - 1].Should().Be('\\', "toda aspa interna precisa estar escapada");
            }
            else if (inner[i] == '\\')
            {
                i++; // consome o par escapado
            }
        }
    }
}
