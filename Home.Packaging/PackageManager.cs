using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Home.Base;

namespace Home.Packaging
{
    //Package Manager uses free and open-source library SevenZipSharp (http://sevenzipsharp.codeplex.com/) and 7z.dll (http://www.7-zip.org/)

    public class PackageManager
    {
        private SevenZip.SevenZipExtractor extractor;
        private string root;

        public PackageManager()
        {
            if (Environment.Is64BitOperatingSystem)
                SevenZip.SevenZipExtractor.SetLibraryPath(E.Root + "\\7z64.dll");
            else
                SevenZip.SevenZipExtractor.SetLibraryPath(E.Root + "\\7z.dll");
        }

        ExtractionWindow window;

        public void BeginUnpack(string file, string dir)
        {
            if (!System.IO.File.Exists(file))
                return;
            root = dir;
            extractor = new SevenZip.SevenZipExtractor(file);
            extractor.Extracting += ExtractorExtracting;
            extractor.ExtractionFinished += ExtractorExtractionFinished;
            extractor.FileExtractionStarted += ExtractorFileExtractionStarted;
            extractor.BeginExtractArchive(dir);
            window = new ExtractionWindow();
            window.ShowDialog();
        }

        public void Unpack(string file, string dir)
        {
            if (!System.IO.File.Exists(file))
                return;
            root = dir;
            extractor = new SevenZip.SevenZipExtractor(file);
            extractor.ExtractArchive(dir);
        }

        void ExtractorFileExtractionStarted(object sender, SevenZip.FileInfoEventArgs e)
        {
            if (window != null)
                window.CurrentFileTextBlock.Text = e.FileInfo.FileName;
            if (File.Exists(root + e.FileInfo.FileName))
            {
                var file = root + e.FileInfo.FileName;
                while (File.Exists(file))
                    file = file + ".bak";
                File.Move(root + e.FileInfo.FileName, file);
            }
        }

        void ExtractorExtractionFinished(object sender, EventArgs e)
        {
            if (window != null)
            {
                window.HeaderTextBlock.Text = Properties.Resources.InstallationFinished;
                window.CurrentFileTextBlock.Text = "";
                window.CurrentFileTextBlock.Visibility = System.Windows.Visibility.Collapsed;
                window.CloseButton.IsEnabled = true;
            }
            extractor.Extracting -= ExtractorExtracting;
            extractor.ExtractionFinished -= ExtractorExtractionFinished;
            extractor.FileExtractionStarted -= ExtractorFileExtractionStarted;
            extractor.Dispose();
        }

        void ExtractorExtracting(object sender, SevenZip.ProgressEventArgs e)
        {
            if (window != null)
                window.ProgressBar.Value = e.PercentDone;
        }
    }
}
