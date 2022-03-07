using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DokaponPacker
{
	class Program
	{

		static void WriteInt32(FileStream f, int value)
		{
			f.Write(BitConverter.GetBytes(value), 0, 4);
		}
		static int ReadInt32(FileStream f)
		{
			byte[] tmp = new byte[4];
			f.Read(tmp, 0, 4);
			return BitConverter.ToInt32(tmp, 0);
		}
		unsafe static void Main(string[] args)
		{
			// okay let's iterate over the folder
			string[] files = Directory.GetFiles("Game\\");
			string[] fileNames = new string[files.Length];

			for (int i = 0; i < files.Length; ++i)
			{
				fileNames[i] = Path.GetFileName(files[i]);
			}

			uint[] fileSizes = new uint[files.Length];
			uint[] fileOffs = new uint[files.Length];
			// output PAC
			FileStream PAC, PAH;
			try
			{
				PAC = File.Open("GAME.PAC", FileMode.OpenOrCreate, FileAccess.Write);
			}
			catch
			{
				Console.WriteLine("Failed to open GAME.PAC!");
				return;
			}
			try
			{
				PAH = File.Open("GAME.PAH", FileMode.OpenOrCreate, FileAccess.Write);
			}
			catch
			{
				Console.WriteLine("Failed to open GAME.PAH!");
				return;
			}
			// open all the files and read them
			uint totalFileSize = 0;
			byte[] data = new byte[0];
			byte[] padding = new byte[0x800];
			for (int i = 0; i < files.Length; ++i)
			{
				FileStream fs = File.Open(files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				// get file size
				fs.Seek(0, SeekOrigin.End);
				fileSizes[i] = (uint)fs.Position;
				fileOffs[i] = totalFileSize;
				totalFileSize += (uint)fs.Position;
				// now read the data
				fs.Seek(0, SeekOrigin.Begin);
				if (fileSizes[i] > data.Length)
				{
					data = new byte[fileSizes[i]];
				}
				fs.Read(data, 0, (int)fileSizes[i]);
				// great, done
				fs.Close();
				// write the data to PAC now
				PAC.Write(data, 0, (int)fileSizes[i]);
				// pad it out to nearest 0x800
				int padCount = (int)(0x800 - (PAC.Position % 0x800));
				if (padCount != 0x800)
				{
					PAC.Write(padding, 0, padCount);
					totalFileSize += (uint)padCount;
				}
			}
			PAC.Close();
			// PAC exists, now write PAH
			// write file count
			WriteInt32(PAH, files.Length);
			// always 0x70
			WriteInt32(PAH, 0x70);
			// create alphabetical listings
			List<ushort>[] alphabetIDs = new List<ushort>[26];
			int totalAlphabetSpace = 0; // account for size
			int[] alphabetOffsets = new int[26];
			for (int i = 0; i < 26; ++i)
			{
				alphabetIDs[i] = new List<ushort>();
			}
			for (int i = 0; i < fileNames.Length; ++i)
			{
				byte[] strAscii = ASCIIEncoding.ASCII.GetBytes(fileNames[i]);
				if (strAscii[0] >= 0x61)
				{
					strAscii[0] -= 0x20;
				}
				strAscii[0] -= 0x41;
				alphabetIDs[strAscii[0]].Add((ushort)i);
			}
			// set up offsets for them
			for (int i = 0; i < 26; ++i)
			{
				alphabetOffsets[i] = totalAlphabetSpace + (files.Length * 0x10) + 0x70;
				totalAlphabetSpace += (alphabetIDs[i].Count + 1) * 2;
			}
			// write the alphabet data offsets
			int stringOffs = (files.Length * 0x10) + 0x70 + totalAlphabetSpace;
			for (int i = 0; i < 26; ++i)
			{
				WriteInt32(PAH, alphabetOffsets[i]);
			}
			// write the file data now
			for (int i = 0; i < files.Length; ++i)
			{
				WriteInt32(PAH, (int)fileOffs[i]);
				WriteInt32(PAH, (int)fileSizes[i]);
				// ?
				WriteInt32(PAH, 0);
				// string offset
				WriteInt32(PAH, stringOffs);
				// update string offset
				stringOffs += fileNames[i].Length + 1;
				if (stringOffs % 2 != 0)
				{
					stringOffs += 1;
				}
			}
			// write alphabet data
			for (int i = 0; i < 26; ++i)
			{
				PAH.Write(BitConverter.GetBytes((short)alphabetIDs[i].Count), 0, 2);
				for (int i2 = 0; i2 < alphabetIDs[i].Count; ++i2)
				{
					PAH.Write(BitConverter.GetBytes(alphabetIDs[i][i2]), 0, 2);
				}
			}
			byte[] zero = new byte[1];
			zero[0] = 0;
			// string time
			for (int i = 0; i < files.Length; ++i)
			{
				// write file names simply
				byte[] currFileName = ASCIIEncoding.ASCII.GetBytes(fileNames[i]);
				PAH.Write(currFileName, 0, currFileName.Length);
				// and 1 0
				PAH.Write(zero, 0, 1);
				// and one more if we must
				if (PAH.Position % 2 != 0)
				{
					PAH.Write(zero, 0, 1);
				}
			}
			// great, we're done!
			PAH.Close();
		}
	}
}
