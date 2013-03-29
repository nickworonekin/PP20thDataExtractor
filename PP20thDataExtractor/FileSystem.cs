using System;
using System.IO;

using PuyoTools;

/*
 * From what I can tell, this is how each entry in the file table is structured:
 * 
 * (4 bytes) - ???
 * (4 bytes) - File offset (big endian)
 * (4 bytes) - File length (big endian)
 * (4 bytes) - ???
 * (4 bytes) - FF FF FF FF
 * (4 bytes) - File number (little endian)
 * 
 * Some entries are 48 bytes or longer! I'm not sure why.
 */

public static class FileSystem
{
    enum GameVersion
    {
        Wii,
        PSP,
        Unknown,
    };

    // Extract GAME.DAT to the specified directory
    public static void Extract(string executable, string gameDat, string outDir)
    {
        Console.Write("\nExtracting ...");

        using (FileStream exeStream = File.OpenRead(executable), gameDatStream = File.OpenRead(gameDat))
        {
            FileStream input = File.OpenRead(executable);
            FileStream gameDatInput = File.OpenRead(gameDat);

            // Determine the game version
            GameVersion gameVersion = GameVersion.Unknown;
            if (exeStream.Length == 2678816) // Wii version
                gameVersion = GameVersion.Wii;
            else if (exeStream.Length == 2716853) // PSP version
                gameVersion = GameVersion.PSP;
            else
                return;

            uint numFiles = 0;
            if (gameVersion == GameVersion.Wii)
                exeStream.Position = 0x18A058;
            else if (gameVersion == GameVersion.PSP)
                exeStream.Position = 0x1978FC;

            // Get the number of files in the file entry table
            exeStream.Position += 20;
            if (gameVersion == GameVersion.Wii)
            {
                numFiles = PTStream.ReadUInt32BE(exeStream);
                exeStream.Position += 152; // Now go to the position of the first file in the file entry table
            }
            else if (gameVersion == GameVersion.PSP)
            {
                numFiles = PTStream.ReadUInt32(exeStream);
                exeStream.Position += 136; // Now go to the position of the first file in the file entry table
            }

            Console.WriteLine(" " + numFiles + " files detected.");

            for (uint i = 0; i < numFiles; i++)
            {
                Console.Write("Extracting file " + i.ToString("D4") + " ... ");

                // Go to the next file entry
                NextFileEntry(exeStream);

                exeStream.Position += 4;

                uint offset = 0, length = 0;
                if (gameVersion == GameVersion.Wii)
                {
                    offset = PTStream.ReadUInt32BE(exeStream);
                    length = PTStream.ReadUInt32BE(exeStream);
                }
                else if (gameVersion == GameVersion.PSP)
                {
                    offset = PTStream.ReadUInt32(exeStream);
                    length = PTStream.ReadUInt32(exeStream);
                }

                exeStream.Position += 12;

                // Now let's extract the file
                ExtractFile(gameDatStream, outDir, i, offset, length);
            }
        }
    }

    private static void ExtractFile(Stream source, string outDir, uint index, uint offset, uint length)
    {
        if (!Directory.Exists(outDir))
            Directory.CreateDirectory(outDir);

        source.Position = offset;
        byte[] buffer = new byte[length];
        source.Read(buffer, 0, (int)length);

        using (FileStream destination = File.Create(Path.Combine(outDir, index.ToString("D4") + DetectFileType(buffer))))
        {
            destination.Write(buffer, 0, (int)length);
        }

        Console.WriteLine("OK");
    }

    // Build GAME.DAT using files from the specified directory
    public static void Build(string executable, string inDir, string gameDat)
    {
        Console.Write("\nBuilding ...");

        using (FileStream exeStream = File.Open(executable, FileMode.Open, FileAccess.ReadWrite))
        {
            // Determine the game version
            GameVersion gameVersion = GameVersion.Unknown;
            if (exeStream.Length == 2678816) // Wii version
                gameVersion = GameVersion.Wii;
            else if (exeStream.Length == 2716853) // PSP version
                gameVersion = GameVersion.PSP;
            else
                return;

            uint numFiles = 0;
            if (gameVersion == GameVersion.Wii)
                exeStream.Position = 0x18A058;
            else if (gameVersion == GameVersion.PSP)
                exeStream.Position = 0x1978FC;

            // Get the number of files in the file entry table
            exeStream.Position += 20;
            if (gameVersion == GameVersion.Wii)
            {
                numFiles = PTStream.ReadUInt32BE(exeStream);
                exeStream.Position += 152; // Now go to the position of the first file in the file entry table
            }
            else if (gameVersion == GameVersion.PSP)
            {
                numFiles = PTStream.ReadUInt32(exeStream);
                exeStream.Position += 136; // Now go to the position of the first file in the file entry table
            }

            string[] fileList = new string[numFiles];

            Console.WriteLine(" " + numFiles + " files detected.");

            // Now let's make sure all the files exist
            for (uint i = 0; i < numFiles; i++)
            {
                string[] file = Directory.GetFiles(inDir, i.ToString("D4") + ".*");
                if (file.Length == 0)
                {
                    Console.WriteLine("{0} does not exist. Terminating.", i.ToString("D4"));
                    return;
                }
                else if (file.Length > 1)
                {
                    Console.WriteLine("Multiple copies of {0} exist. Terminating.", i.ToString("D4"));
                    return;
                }

                fileList[i] = file[0];
            }

            // Ok, looks like we're good. Let's build GAME.DAT
            using (FileStream gameDatStream = File.Create(gameDat))
            {
                uint offset = 0;
                for (uint i = 0; i < numFiles; i++)
                {
                    Console.Write("Adding file " + Path.GetFileName(fileList[i]) + " ... ");

                    // Go to the next file entry
                    NextFileEntry(exeStream);

                    exeStream.Position += 4;

                    using (FileStream inStream = File.OpenRead(fileList[i]))
                    {
                        if (gameVersion == GameVersion.Wii)
                        {
                            PTStream.WriteUInt32BE(exeStream, offset);
                            PTStream.WriteUInt32BE(exeStream, (uint)inStream.Length);
                        }
                        else if (gameVersion == GameVersion.PSP)
                        {
                            PTStream.WriteUInt32(exeStream, offset);
                            PTStream.WriteUInt32(exeStream, (uint)inStream.Length);
                        }

                        PTStream.CopyPartToPadded(inStream, gameDatStream, (int)inStream.Length, 2048, 0);

                        offset += (uint)PTMethods.RoundUp((int)inStream.Length, 2048);
                    }

                    exeStream.Position += 12;

                    Console.WriteLine("OK");
                }
            }
        }
    }

    private static string DetectFileType(byte[] input)
    {
        // Formats used in both versions
        if (input.Length > 4 && input[0] == 'F' && input[1] == 'N' && input[2] == 'T' && input[3] == 0) // FNT
            return ".fnt";
        if (input.Length > 2 && input[0] == 0xD && input[1] == 0xA) // PSS
            return ".pss";
        if (input.Length > 12 && input[0] == 0 && input[1] == 0 && input[2] == 0 && input[3] == 0) // ACX
        {
            uint offset = (uint)(input[8] << 24 | input[9] << 16 | input[10] << 8 | input[11]);
            if (input.Length > offset + 1 && input[offset] == 128)
                return ".acx";
        }

        // Wii version specific formats
        if (input.Length > 4 && input[0] == 'U' && input[1] == 0xAA && input[2] == '8' && input[3] == 0x2D) // ARC
            return ".arc";

        // PSP version specific formats
        if (input.Length > 2 && input[0] == 'P' && input[1] == 'K') // ZIP
            return ".zip";
        if (input.Length > 4 && input[0] == 'a' && input[1] == 't' && input[2] == '3' && input[3] == 'c') // AT3 Collection
            return ".a3c";
        if (input.Length > 4 && input[0] == 'R' && input[1] == 'I' && input[2] == 'F' && input[3] == 'F') // AT3
            return ".at3";
        if (input.Length > 7 && input[0] == '#' && input[1] == 'i' && input[2] == 'f' && input[3] == 'n' &&
            input[4] == 'd' && input[5] == 'e' && input[6] == 'f') // C++ Header File
            return ".h";

        return "";
    }

    private static void NextFileEntry(Stream input)
    {
        input.Position += 16;
        while (input.Position < input.Length)
        {
            if (PTStream.ReadUInt32(input) == 0xFFFFFFFF)
                break;

            input.Position -= 3;
        }
        input.Position -= 20;
    }
}