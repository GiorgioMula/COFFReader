using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace COFF_Reader
{
    /// <summary>
    /// Motorola S19 file format. The class allow to add coff sections an export 
    /// binary in S19 file format.
    /// </summary>
    /// <remarks>Complete for address size > 16 bit</remarks>
    class Motorola
    {
        int targetNumBit, entryPointAddress;
        string output;

        public Motorola(int in_targetNumBit, int in_entryPoint = 0)
        {
            targetNumBit = in_targetNumBit;
            entryPointAddress = in_entryPoint;
            output = WriteLine("S0", new byte[] { 0, 0, 0, 0, 0 });
        }

        public void AddSection(Coff.Section newSection)
        {
            const int maxBytesInLine = 30;
            int address = 0;
            int bytesToWrite = newSection.Size;
            while (bytesToWrite > 0)
            {
                int num_byte = Math.Min(maxBytesInLine, bytesToWrite);
                byte[] buffer = new byte[num_byte + 2 /* address */];
                // copy big endian address
                buffer[0] = (byte)((newSection.PhysicalAddress + address) >> 8);
                buffer[1] = (byte)(newSection.PhysicalAddress + address);
                // copy data
                Array.Copy(newSection.rawData, address, buffer, 2, num_byte);
                output += WriteLine("S1", buffer);
                bytesToWrite -= num_byte;
                address += num_byte;
            }
        }

        public void Save(string fileName)
        {
            // Close output
            byte[] address = new byte[2];
            address[0] = (byte)(entryPointAddress >> 8);
            address[1] = (byte)(entryPointAddress);
            output += WriteLine("S9", address);

            // Save
            using (FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate))
            {
                StreamWriter sw = new StreamWriter(fs);
                sw.Write(output);
                sw.Close();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private string WriteLine(string type, byte[] data)
        {
            string newLine = type;
            newLine += (data.Length + 1).ToString("X2");
            byte checksum = (byte)(data.Length + 1);
            foreach (byte b in data)
            {
                checksum += b;
                newLine += b.ToString("X2");
            }
            checksum = (byte)~checksum;
            newLine += checksum.ToString("X2");
            newLine += Environment.NewLine;
            return newLine;
        }
    }
}
