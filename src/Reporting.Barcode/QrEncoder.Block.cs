using System.Text;

namespace Reporting.Barcode;

public static partial class QrEncoder
{

    private sealed partial record Block(byte[] Data, byte[] Ec);
}
