using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Dictionary<string, string> minimalhashes = new Dictionary<string, string>();
            Dictionary<string, string> mediumhashes = new Dictionary<string, string>();
            Dictionary<string, string> fullhashes = new Dictionary<string, string>();

            var basepaths = args;
            int doubles = 0;
            int counter = 0;
            using (MD5 md5Hash = MD5.Create())
            {
                using (var ctx = new DatabaseContext())
                {
                    ctx.Database.EnsureCreated();

                    foreach (var path in basepaths)
                    {
                        if (Directory.Exists(path))
                        {
                            foreach (var img in findImgs(new DirectoryInfo(path)))
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

                                    doubles++;
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
                                            System.Console.WriteLine($"Doubles: {fullhashes[fullhash]} AND {img} are EQUAL");
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

        private static IEnumerable<string> findImgs(DirectoryInfo dirinfo)
        {
            foreach (var dir in dirinfo.GetDirectories())
            {
                foreach (var file in findImgs(dir))
                {
                    yield return file;
                }
            }

            foreach (var fileinfo in dirinfo.GetFiles())
            {
                if (imgExtenstions.Contains(fileinfo.Extension.ToLowerInvariant()))
                {
                    yield return fileinfo.FullName;
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
                switch (kind)
                {
                    case hashes.minimal:
                        {
                            int maxbufferlength = GetMaxBufferLength(filestream.Length, 1000);
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
