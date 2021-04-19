using System;
using System.Text;


namespace FakeFS
{
    public static class BytePrinter
    {
        public static void HexPrint(byte[] byteDump)
        {
            for (int i = 0; i < byteDump.Length; i++)
            {
                Console.Write($" {byteDump[i]:X2}");

                if (i % 32 == 31)
                    Console.WriteLine();
            }
            Console.WriteLine();
        }

        public static void TextPrint(byte[] byteDump)
        {
            Console.WriteLine(Encoding.ASCII.GetString(byteDump).Trim('\0'));
        }
    }
}
