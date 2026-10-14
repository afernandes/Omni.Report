using System.Text;

namespace Reporting.Barcode;

public static partial class QrEncoder
{

    private sealed class BitBuffer : List<int>
    {
        public int Length => Count;
        public void AppendBits(int val, int len)
        {
            for (int i = len - 1; i >= 0; i--) Add((val >> i) & 1);
        }
    }
}
