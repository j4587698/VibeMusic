using System;
using System.IO;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            string dir = @"e:\music\CSharp\KuGouMusicAvalonia\KuGouMusicAvalonia\Views";
            var files = Directory.GetFiles(dir, "*.axaml", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                if (text.Contains("<Button.Icon>"))
                {
                    text = text.Replace("<Button.Icon>", "<lumina:LuminaButton.Icon>");
                    text = text.Replace("</Button.Icon>", "</lumina:LuminaButton.Icon>");
                    File.WriteAllText(file, text);
                    Console.WriteLine("Fixed: " + Path.GetFileName(file));
                }
            }
        }
    }
}
