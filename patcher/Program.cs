using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace patcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var regex = new Regex(@"(?<=\.assembly extern /\*\d+\*/ Metrics..){.*?}", RegexOptions.Singleline);
            var pubToken = new Regex(@"(?<=\.publickeytoken = \()(.*?)(?: \))");
            var str = File.ReadAllText(args[0]);
            Console.WriteLine(args[0]);
            string token;
            if(args.Length > 1)
            {
                token = args[1];
            }
            else
            {
                token = Console.ReadLine();
            }
            Console.WriteLine("Token: {0}", token);

            if(String.IsNullOrEmpty(token))
            {
                Console.WriteLine("Invalid token.");
                return;
            }

            Match match = regex.Match(str);
            if (match.Success)
            {
                var str2 = str.Substring(match.Index, match.Length);
                var idx1 = match.Index;
                var idx2 = idx1 + match.Length;
                Console.WriteLine("idx: {0} Len: {1}\nStr: {2}", match.Index, match.Length,
                    str2);

                match = pubToken.Match(str2);
                if (match.Success)
                {
                    //Console.WriteLine(str2.Substring(match.Index, match.Length));
                    //Console.WriteLine(str2.Substring(match.Groups[1].Index, match.Groups[1].Length));
                    str2 = str2.Substring(0, match.Groups[1].Index) + token + str2.Substring(match.Groups[1].Index + match.Groups[1].Length);
                }
                else
                {
                    match = Regex.Match(str2, @"\.ver");
                    str2 = str2.Insert(match.Index, ".publickeytoken = (" + token + " )\n  ");
                }
                Console.WriteLine("Patched: {0}", str2);

                File.WriteAllText(args[0], str.Substring(0, idx1) + str2 + str.Substring(idx2));
            }
        }
    }
}
