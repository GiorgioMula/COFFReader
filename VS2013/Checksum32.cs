using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace COFF_Reader
{
    class Checksum32
    {
        public static UInt32 Calculate(byte[] raw_data)
        {
            UInt32 totalChecksum = 0;
            for (int i = 0; i < raw_data.Length; i = i + 2)
            {
                UInt16 val1 = BitConverter.ToUInt16(raw_data, i);
                totalChecksum += val1;
            }
            return totalChecksum;
        }
    }
}
