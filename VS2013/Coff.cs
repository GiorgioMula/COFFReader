using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace COFF_Reader
{
    public class Coff
    {
        public struct Header
        {
            public ushort VersionID;               // 0-1 indicates version of COFF file structure
            public ushort NumberSectionHeaders;    // 2-3 Unsigned short Number of section headers
            public DateTime Timestamp;                  // 4-7 Integer Time and date stamp; indicates when the file was created
            public int SymbolTableStart;           // 8-11 Integer File pointer; contains the symbol table's starting address
            public int SymbolNumEntries;           // 12-15 Integer Number of entries in the symbol table
            public ushort OptionalHeaderNumBytes;  // 16-17 Unsigned short Number of bytes in the optional header. This field is either 0 or 28; if it is 0, there is no optional file header.
            public ushort Flags;                   // 18-19 Unsigned short Flags (see Table 2)
            public ushort TargetID;                // 20-21 Unsigned short Target ID; magic number (see Table 3) indicates the file can be executed in a specific TI system

            public string GetDescription()
            {
                return String.Format("Version: {0}, TargetID: {1:X4}\nOptionalHeaderNumBytes: {2}", VersionID, TargetID, OptionalHeaderNumBytes);
            }
        }
        
        public struct OptionalFileHeader
        {
            //  0-1 Short Optional file header magic number (0108h)
            public short Version;              // 2-3 Short Version stamp
            public int ExecCodeSize;           // 4-7 Long(1) Size (in bytes) of executable code
            public int InitializedDataSize;    // 8-11 Long(1) Size (in bytes) of initialized data
            public int NotInitializedDataSize; //12-15 Long(1) Size (in bytes) of uninitialized data
            public int EntryPointAddress;      //16-19 Long(1) Entry point
            public int ExecCodeAddress;        //20-23 Long(1) Beginning address of executable code
            public int InitDataAddress;        //24-27 Long(1) Beginning address of initialized data

            public string GetDescription()
            {
                return String.Format("Version: {0}, ExecCodeAddress: {1:X4} ExecCodeSize: {2}", Version, ExecCodeAddress, ExecCodeSize);
            }
        }

        public struct Section
        {
            // COFF1 or 2
            public string name;            // 0-7 Character This field contains one of the following: 1) An 8-character section name padded with nulls. 2) A pointer into the string table if the symbol name is longer than eight characters.
            public int PhysicalAddress;    // 8-11 Long(1) Section's physical address
            public int VirtualAddress;     // 12-15 Long(1) Section's virtual address
            public int Size;               // 16-19 Long(1) Section size in bytes (C6000, C55x, TMS470 and TMS430) or words (C2800,C5400)
            public int RawDataPointer;     // 20-23 Long(1) File pointer to raw data
            public int RelocationPointer;     // 24-27 Long(1) File pointer to relocation entries
            public ushort MemoryPage;
            // 28-31 Long(1) Reserved
            // 32-33 Unsigned short Number of relocation entries
            // 34-35 Unsigned short Reserved
            // 36-37 Unsigned short Flags (see Table 7)
            // 38 Char Reserved
            // 39 Char Memory page number
            public byte[] rawData;

            public Section(byte[] sec_data)
            {
                name = Encoding.ASCII.GetString(sec_data, 0, 8);
                while (name[name.Length - 1] == '\0')
                {
                    name = name.Substring(0, name.Length - 1);
                }
                PhysicalAddress = 2 * BitConverter.ToInt32(sec_data, 8);
                VirtualAddress = 2 * BitConverter.ToInt32(sec_data, 12);
                RawDataPointer = BitConverter.ToInt32(sec_data, 20);
                RelocationPointer = BitConverter.ToInt32(sec_data, 24);
                if (sec_data.Length == 40)
                {
                    Size = 2 * BitConverter.ToInt32(sec_data, 16);
                    MemoryPage = sec_data[39];
                }
                else
                {
                    Size = BitConverter.ToInt32(sec_data, 16);
                    MemoryPage = BitConverter.ToUInt16(sec_data, 46);
                }

                rawData = new byte[Size];
            }

            public void CopyData(byte[] source)
            {
                Array.Copy(source, rawData, rawData.Length);
            }

            public string GetDescription()
            {
                return String.Format("Name: {0}", name) + string.Format(",RawDataPointer: {0:X4}, Size: {1}",
                    RawDataPointer, Size);
            }
        }

        public Header header;
        public OptionalFileHeader optionalHeader;
        
        Dictionary<string, Section> sections;
        
        public Coff(string fileName)
        {
            sections = new Dictionary<string, Section>();

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
            {
                byte[] fileHeader = new byte[22];
                fs.Read(fileHeader, 0, 22);
                header.VersionID = BitConverter.ToUInt16(fileHeader, 0);
                header.NumberSectionHeaders = BitConverter.ToUInt16(fileHeader, 2);
                header.Timestamp = DateTime.Parse("01/01/1970").AddSeconds(BitConverter.ToInt32(fileHeader, 4));
                header.SymbolTableStart = BitConverter.ToInt32(fileHeader, 8);
                header.SymbolNumEntries = BitConverter.ToInt32(fileHeader, 12);
                header.OptionalHeaderNumBytes = BitConverter.ToUInt16(fileHeader, 16);
                header.Flags = BitConverter.ToUInt16(fileHeader, 18);
                header.TargetID = BitConverter.ToUInt16(fileHeader, 20);

                if (header.OptionalHeaderNumBytes == 28)   // only 0 or 28 allowed
                {
                    byte[] optionalFileHeader = new byte[28];
                    fs.Read(optionalFileHeader, 0, 28);
                    if (BitConverter.ToUInt16(optionalFileHeader, 0) != 0x0108)
                    {
                        throw new Exception("Wrong COFF file format (optional header signature)");
                    }

                    optionalHeader.Version = BitConverter.ToInt16(optionalFileHeader, 2);
                    optionalHeader.ExecCodeSize = 2 * BitConverter.ToInt32(optionalFileHeader, 4);
                    optionalHeader.ExecCodeAddress = 2 * BitConverter.ToInt32(optionalFileHeader, 20);
                }

                byte[] sectionData = null;
                if (header.VersionID == 0x00c1)
                {
                    sectionData = new byte[40];
                }
                else if (header.VersionID == 0x00c2)
                {
                    sectionData = new byte[48];
                    // skip first and last
                    fs.Read(sectionData, 0, sectionData.Length);
                    header.NumberSectionHeaders--;
                }      
                else throw new Exception("Wrong COFF file version, found " + header.VersionID.ToString("X4"));
                for (int i = 0; i < header.NumberSectionHeaders; ++i)
                {
                    fs.Read(sectionData, 0, sectionData.Length);
                    Section sec = new Section(sectionData);
                    sections[sec.name] = sec;
                }

                // copy raw data into each section
                
                foreach (KeyValuePair<string, Section> sec in sections)
                {
                    byte[] data = new byte[sec.Value.Size];
                    fs.Seek(sec.Value.RawDataPointer, SeekOrigin.Begin);
                    fs.Read(data, 0, sec.Value.Size);
                    sec.Value.CopyData(data);
                }                           
            }
        }

        public List<Section> GetSections()
        {
            List<Section> sectionList = new List<Section>();
            foreach (KeyValuePair<string, Section> element in sections)
            {
                sectionList.Add(element.Value);
            }
            return sectionList;
        }
    }
}
