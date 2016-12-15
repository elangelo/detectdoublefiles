using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommandLine;
using System.Linq;

namespace ConsoleApplication
{
    class Options
    {
        [Option('d', "directory", Required = true)]
        public IEnumerable<string> InputDirectories { get; set; }

        [Option('i', "ignore", Required = false)]
        public IEnumerable<string> IgnoreDirectories { get; set; }

        [OptionAttribute('l', "log", Required = false)]
        public string LogFile { get; set; }
    }

    public class LogHelper
    {
        public bool LogToConsole;
        public StreamWriter sw;

        public LogHelper(string logfile)
        {
            if (string.IsNullOrWhiteSpace(logfile))
            {
                LogToConsole = true;
            }
            else
            {
                var logFile = File.Create(logfile);
                sw = new StreamWriter(logFile);
                sw.AutoFlush = true;
                LogToConsole = false;
            }
        }

        public void Log(string message)
        {
            if (LogToConsole)
            {
                System.Console.WriteLine(message);
            }
            else
            {
                sw.WriteLine(message);
                sw.Flush();
            }
        }
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            ParserResult<Options> parserResult = CommandLine.Parser.Default.ParseArguments<Options>(args);

            var exitCode = parserResult.MapResult(
                options =>
                {
                    dothework(options.InputDirectories, options.IgnoreDirectories.ToList(), options.LogFile);
                    return 0;
                },
                errors =>
                {
                    Console.Write(errors);
                    return 1;
                }
            );

            return exitCode;
        }
        public static LogHelper logHelper;

        public static void dothework(IEnumerable<string> inputDirectories, List<string> ignoreDirectories, string logFile)
        {
            logHelper = new LogHelper(logFile);

            // var isValid = CommandLine.Parser.Default.ParseArguments(args);

            Dictionary<string, string> minimalhashes = new Dictionary<string, string>();
            Dictionary<string, string> mediumhashes = new Dictionary<string, string>();
            Dictionary<string, string> fullhashes = new Dictionary<string, string>();

            int doubles = 0;
            int counter = 0;
            using (MD5 md5Hash = MD5.Create())
            {
                using (var ctx = new DatabaseContext())
                {
                    ctx.Database.EnsureCreated();

                    foreach (var path in inputDirectories)
                    {
                        if (Directory.Exists(path))
                        {
                            foreach (var img in findImgs(new DirectoryInfo(path), ignoreDirectories))
                            {
                                string mediumhash = "", fullhash = "";
                                var hash = GetMd5Hash(md5Hash, img, hashes.minimal);

                                //System.Console.WriteLine(hash);
                                if (minimalhashes.ContainsKey(hash))
                                {
                                    //calculate mediumhashes for the other matching minimal hash;
                                    var otherfilename = minimalhashes[hash];
                                    if (otherfilename != "EMPTY")
                                    {
                                        var mediumhashforotherfilename = GetMd5Hash(md5Hash, img, hashes.medium);
                                        mediumhashes.Add(mediumhashforotherfilename, otherfilename);
                                        //clear filename on minimal hash so later on we know we already have a medium hash for this one
                                        minimalhashes[hash] = "EMPTY";
                                    }

                                    mediumhash = GetMd5Hash(md5Hash, img, hashes.medium);
                                    if (mediumhashes.ContainsKey(mediumhash))
                                    {
                                        var otherfilename2 = mediumhashes[mediumhash];
                                        if (otherfilename2 != "EMPTY")
                                        {
                                            var fullhashforotherfilename = GetMd5Hash(md5Hash, otherfilename2, hashes.full);
                                            fullhashes.Add(fullhashforotherfilename, otherfilename2);
                                            //clear filename on minimal hash so later on we know we already have a medium hash for this one
                                            mediumhashes[mediumhash] = "EMPTY";
                                        }

                                        fullhash = GetMd5Hash(md5Hash, img, hashes.full);
                                        if (fullhashes.ContainsKey(fullhash))
                                        {
                                            logHelper.Log($"Doubles: {fullhashes[fullhash]} AND {img} are EQUAL");
                                            doubles++;
                                        }
                                        else
                                        {
                                            fullhashes.Add(fullhash, img);
                                        }
                                    }
                                    else
                                    {
                                        mediumhashes.Add(mediumhash, img);
                                    }
                                }
                                else
                                {
                                    minimalhashes.Add(hash, img);
                                }

                                counter++;
                                ctx.Images.Add(new ImageItem() { Path = img, Checksum1 = hash, Checksum2 = mediumhash, Checksum3 = fullhash });
                                if (counter % 1000 == 0)
                                {
                                    ctx.SaveChanges();
                                }
                            }

                            ctx.SaveChanges();
                        }
                    }
                }
            }
            System.Console.WriteLine($"doubles found: {doubles}");
        }

        public static List<string> imgExtenstions = new List<string>() { ".jpg", ".jpeg", ".tif", ".tiff", ".dng" };

        private static IEnumerable<string> findImgs(DirectoryInfo dirinfo, List<string> ignoreDirectories)
        {
            if (!ignoreDirectories.Contains(dirinfo.FullName))
            {
                foreach (var dir in dirinfo.GetDirectories().OrderBy(dir => dir.FullName))
                {
                    foreach (var file in findImgs(dir, ignoreDirectories))
                    {
                        yield return file;
                    }
                }

                foreach (var fileinfo in dirinfo.GetFiles().OrderBy(file => file.FullName))
                {
                    if (imgExtenstions.Contains(fileinfo.Extension.ToLowerInvariant()))
                    {
                        yield return fileinfo.FullName;
                    }
                }
            }
        }

        enum hashes
        {
            minimal,
            medium,
            full
        }
        static string GetMd5Hash(MD5 hasher, string path, hashes kind)
        {
            using (var filestream = File.OpenRead(path))
            {
                if (filestream.CanRead)
                {
                    switch (kind)
                    {
                        case hashes.minimal:
                            {
                                int maxbufferlength = GetMaxBufferLength(filestream.Length, 1000);
                                Console.WriteLine(maxbufferlength);
                                byte[] buffer = new byte[maxbufferlength];
                                filestream.Read(buffer, 0, maxbufferlength);
                                var hash = hasher.ComputeHash(buffer);
                                return ToHex(hash);
                            }
                        case hashes.medium:
                            {
                                int maxbufferlength = GetMaxBufferLength(filestream.Length, 10000);
                                byte[] buffer = new byte[maxbufferlength];
                                filestream.Read(buffer, 0, maxbufferlength);
                                var hash = hasher.ComputeHash(buffer);
                                return ToHex(hash);
                            }
                        case hashes.full:
                            {
                                filestream.Seek(0, SeekOrigin.Begin);
                                var hash = hasher.ComputeHash(filestream);
                                return ToHex(hash);
                            }
                        default:
                            {

                                var hash = hasher.ComputeHash(filestream);
                                return ToHex(hash);
                            }
                    }
                }
                else
                {
                    throw new Exception($"Can't read file: {path}");
                }
            }
        }

        private static int GetMaxBufferLength(long filestreamlength, int maxLength)
        {
            if (filestreamlength > maxLength)
            {
                return maxLength;
            }
            else
            {
                return (int)(filestreamlength - 1);
            }
        }

        public static string ToHex(byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("X2"));
            return result.ToString();
        }
    }
}
