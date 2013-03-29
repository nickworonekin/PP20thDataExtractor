using System;
using System.IO;

public class PP20thDataExtractor
{
    public static void Main(string[] args)
    {
        if (args.Length == 4)
        {
            if (args[0] == "extract")
            {
                if (!File.Exists(args[1]))
                {
                    Console.WriteLine("{0} does not exist.", args[1]);
                }

                if (!File.Exists(args[2]))
                {
                    Console.WriteLine("{0} does not exist.", args[2]);
                }

                FileSystem.Extract(args[1], args[2], args[3]);
            }

            else if (args[0] == "build")
            {
                if (!File.Exists(args[1]))
                {
                    Console.WriteLine("{0} does not exist.", args[1]);
                }

                if (!Directory.Exists(args[2]))
                {
                    Console.WriteLine("{0} does not exist.", args[2]);
                }

                FileSystem.Build(args[1], args[2], args[3]);
            }

            else
            {
                ShowUsage();
            }
        }
        else
        {
            ShowUsage();
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("PP20th Data Extractor");
        Console.WriteLine("An extractor/builder for GAME.DAT");
        Console.WriteLine();
        Console.WriteLine("Usage");
        Console.WriteLine();
        Console.WriteLine("\tPP20thDataExtractor extract <executable> <GAME.DAT> <output>");
        Console.WriteLine("\tPP20thDataExtractor build <executable> <input> <GAME.DAT>");
        Console.WriteLine();
        Console.WriteLine("<executable> - Name of the executable (usually main.dol or NPJH50492.BIN)");
        Console.WriteLine("<GAME.DAT>   - Name of the GAME.DAT file");
        Console.WriteLine("<output>     - Name of the directory to extract the contents to");
        Console.WriteLine("<input>      - Name of the directory containing the files to add");
    }
}