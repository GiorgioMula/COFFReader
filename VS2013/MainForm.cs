using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;


namespace COFF_Reader
{
    public partial class MainForm : Form
    {
        const string Title = "COFF analyzer";
        Coff coff;

        public MainForm(string[] args)
        {
            InitializeComponent();
            if (args.Length > 0)
            {
                // Use first argument as source file
                if (File.Exists(args[0]))
                {
                    coff = OpenCoffFile(args[0]);
                    if (args.Length > 1)
                    {
                        // Use other parameters as section names                        
                        List<Coff.Section> sections = coff.GetSections();

                        for (int index = 1; index < args.Length; ++index)
                        {
                            string sec_name = args[index];
                            foreach (Coff.Section sec in sections)
                            {
                                if (sec.name == sec_name)
                                {
                                    sections.Add(sec);
                                    break;
                                }
                            }                            
                        }
                        toolStripStatusLabel1.Text = UpdateChecksumInformation(coff, sections);
                    }
                }
                else
                {
                    MessageBox.Show("File to open not found !", Title, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenCoffFile(openFileDialog1.FileName);
            }
        }

        private Coff OpenCoffFile(string fileName)
        {
            Coff coff = new Coff(fileName);
            if (coff != null)
            {
                Text = Title + " - " + Path.GetFileName(fileName);

                // Write info
                textBoxInfo.Text = "EXEC CODE ADDR: " + coff.optionalHeader.ExecCodeAddress.ToString("X4") + Environment.NewLine;
                textBoxInfo.Text += "EXEC CODE SIZE: " + coff.optionalHeader.ExecCodeSize + Environment.NewLine;
                textBoxInfo.Text += "BUILD DATE: " + coff.header.Timestamp.ToString() + Environment.NewLine;

                // Write list view
                listViewSections.Items.Clear();
                foreach (Coff.Section section in coff.GetSections())
                {
                    ListViewItem lvi = new ListViewItem(new string[] { section.name, section.PhysicalAddress.ToString("X4"), section.Size.ToString(), section.MemoryPage.ToString() });
                    listViewSections.Items.Add(lvi);
                }                
            }
            return coff;
        }

        private byte[] PrepareRawData(Coff coff, List<Coff.Section> section_list)
        {
            byte[] flashData;
            if (coff.header.VersionID == 0x00c1)
            {
                flashData = new byte[0x10000];
            }
            else
            {
                flashData = new byte[0x20000];
            }

            for (int i = 0; i < flashData.Length; ++i)
            {
                flashData[i] = 0xFF;
            }

            foreach (Coff.Section section in section_list)
            {
                Array.Copy(section.rawData, 0, flashData, section.PhysicalAddress & 0xFFFF, section.Size);
            }
            return flashData;
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void exportToMotorolaFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (coff.header.VersionID == 0x00C1)
            {                
                if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Motorola motFile = new Motorola(16, coff.optionalHeader.EntryPointAddress);
                    List<Coff.Section> sections = coff.GetSections();
                    foreach (int index in listViewSections.SelectedIndices)
                    {
                        string sec_name = listViewSections.Items[index].Text;
                        foreach (Coff.Section sec in sections)
                        {
                            if (sec_name == sec.name)
                            {
                                if (sec.MemoryPage == 0)
                                {
                                    motFile.AddSection(sec);
                                }
                                else
                                {
                                    MessageBox.Show("Section " + sec_name + " skipped (not text area)", "COFF Analyzer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                                break;
                            }
                        }
                    }
                    motFile.Save(saveFileDialog1.FileName);
                }
            }
            else
            {
                MessageBox.Show("Sorry, not supported for this COFF object file", "COFF Analyzer", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }

        private void checksumOnSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Cycle into all selected sections
            List<Coff.Section> checksum_sections = new List<Coff.Section>();
            List<Coff.Section> target_sections = coff.GetSections();
            foreach (int index in listViewSections.SelectedIndices)
            {
                string sec_name = listViewSections.Items[index].Text;
                foreach (Coff.Section sec in target_sections)
                {
                    if (sec_name == sec.name)
                    {
                        checksum_sections.Add(sec);
                        break;
                    }
                }
            }
            toolStripStatusLabel1.Text = UpdateChecksumInformation(coff, checksum_sections);
        }

        /// <summary>
        /// This function simply write checksum information
        /// </summary>
        /// <param name="executable">Coff executable</param>
        /// <param name="sections">List of sections to be used to compute checksum</param>
        string UpdateChecksumInformation(Coff executable, List<Coff.Section> sections)
        {
            byte[] flashData = PrepareRawData(executable, sections);
            Crc32 crc32 = new Crc32();
            crc32.AddData(flashData);
            return string.Format("CRC32: 0x{0:X8}, Checksum32: 0x{1:X8}", crc32.Crc32Value, Checksum32.Calculate(flashData));
        }

        #region DRAG and DROP file open
        private void listViewSections_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            OpenCoffFile(files[0]);
        }

        private void listViewSections_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            OpenCoffFile(files[0]);
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }
        #endregion
    }
}
