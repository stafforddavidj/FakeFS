using System;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;


namespace FakeFS
{
    public class Program
    {
        public static bool isMounted = false;
        public static string volumeName = string.Empty;
        public static string Command = string.Empty;
        static void Main(string[] args)
        {
            FileSystem fakefs;

            if (args.Length == 0)
            {
                Console.WriteLine("Invalid arguments");
                return;
            }

            void ArgsError(string command)
            {
                Console.WriteLine($"Invalid number of arguments for the {command} command.");
            }

            Command = args[0];

            #region Static volume commands

            //
            // ALLOCATE <VOLUMENAME> <VOLUMESIZE> <PAGESIZE>
            //
            if (Command.Equals("ALLOCATE", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 4)
                {
                    ArgsError("ALLOCATE");
                    return;
                }

                string VolumeName = args[1];
                string VolumeSize = args[2];
                string PageSize = args[3];

                FileSystem.Allocate(VolumeName, int.Parse(VolumeSize), int.Parse(PageSize));
            }
            // DEALLOCATE <VOLUMENAME> 
            //
            else if (Command.Equals("DEALLOCATE", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2)
                {
                    ArgsError("DEALLOCATE");
                    return;
                }

                string VolumeName = args[1];
                FileSystem.Deallocate(VolumeName);
            }
            // TRUNCATE <VOLUMENAME> 
            //
            else if (Command.Equals("TRUNCATE", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2)
                {
                    ArgsError("TRUNCATE");
                    return;
                }

                string VolumeName = args[1];
                FileSystem.Truncate(VolumeName);
            }
            // DUMP <VOLUMENAME> 
            //
            else if (Command.Equals("DUMP", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2)
                {
                    ArgsError("DUMP");
                    return;
                }

                string VolumeName = args[1];
                BytePrinter.HexPrint(FileSystem.Dump(VolumeName));
            }
            // INFO <VOLUMENAME> 
            //
            // Displays the size of the volume, free space, and number of files in the volume
            else if (Command.Equals("INFO", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2)
                {
                    ArgsError("INFO");
                    return;
                }

                string VolumeName = args[1];
                try
                {
                    FileSystem tempfs = FileSystem.Mount(VolumeName);

                    List<Tuple<DirectoryEntry, uint>> contents = tempfs.GetDirContents("");

                    Tuple<long, long> volSpace = tempfs.GetFreeSpace();

                    Console.WriteLine($"  Volume {VolumeName}:");
                    Console.WriteLine($"    Total size:   {volSpace.Item1}");
                    Console.WriteLine($"    Free space:   {volSpace.Item2}");
                    Console.WriteLine($"    Files read:   {contents.Count}");
                }
                catch (ApplicationException err)
                {
                    Console.WriteLine(err.Message);
                }
            }
            #endregion

            #region Mounted volume commands

            // MOUNT <VOLUMENAME> 
            //
            else if (Command.Equals("MOUNT", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2)
                {
                    ArgsError("MOUNT");
                    return;
                }

                if (isMounted)
                {
                    Console.WriteLine($"Volume '{volumeName}' is already mounted.  Unmount it before mounting another volume.");
                    return;
                }

                string VolumeName = args[1];
                string FilePath = "/";
                try
                {
                    fakefs = FileSystem.Mount(VolumeName);
                    Console.WriteLine($"Volume '{VolumeName}' is mounted.");

                    while (fakefs.isMounted)
                    {
                        Console.Write("FakeFS#> ");
                        string[] mountArgs = Console.ReadLine().Trim().Split(' ');
                        try
                        {
                            Command = mountArgs[0];

                            // CREATE <FILEPATH> 
                            //
                            if (Command.Equals("CREATE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("CREATE");
                                    continue;
                                }
                                fakefs.CreateFile(mountArgs[1]);
                            }
                            // MKDIR <FILEPATH>
                            //
                            else if (Command.Equals("MKDIR", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("MKDIR");
                                    continue;
                                }
                                fakefs.CreateFile(mountArgs[1], true);
                            }
                            // MV <IN_FILEPATH> <OUT_FILEPATH>
                            //
                            else if (Command.Equals("MV", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 3)
                                {
                                    ArgsError("MV");
                                    continue;
                                }
                                fakefs.MoveFile(mountArgs[1], mountArgs[2]);
                            }
                            // CD <FILEPATH>
                            //
                            else if (Command.Equals("CD", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length == 1)
                                {
                                    Console.WriteLine($"  {FilePath}");
                                }
                                else if (mountArgs.Length == 2)
                                {
                                    FilePath = fakefs.ChangeDirectory(mountArgs[1]);
                                }
                                else
                                {
                                    ArgsError("CD");
                                }
                            }
                            // LS / DIR / CATALOG
                            //
                            else if (new[] { "LS", "DIR", "CATALOG" }.Contains(Command.ToUpper()))
                            {
                                if (mountArgs.Length != 1 && mountArgs.Length != 2)
                                {
                                    ArgsError("LS");
                                    continue;
                                }

                                List<Tuple<DirectoryEntry, uint>> contents;
                                if (mountArgs.Length == 1)
                                {
                                    contents = fakefs.GetDirContents("");
                                }
                                else
                                {
                                    contents = fakefs.GetDirContents(mountArgs[1]);
                                }
                                for (int i = 0; i < contents.Count; i++)
                                {
                                    DirectoryEntry entry = contents[i].Item1;
                                    Console.Write($" {entry.FileName}");

                                    Console.WriteLine();
                                }
                            }
                            // WRITE <FILEPATH> <START_INDEX> <DATA>
                            //
                            else if (Command.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 4)
                                {
                                    ArgsError("WRITE");
                                    continue;
                                }

                                if (int.TryParse(mountArgs[2], out int start))
                                {
                                    fakefs.FileWrite(mountArgs[1], start,
                                        Encoding.UTF8.GetBytes(mountArgs[3]));
                                }
                                else
                                {
                                    Console.WriteLine("Error parsing write index.");
                                }
                            }
                            // READ <FILEPATH> <START_INDEX> <LENGTH>
                            //
                            else if (Command.Equals("READ", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 4)
                                {
                                    ArgsError("READ");
                                    continue;
                                }

                                if (int.TryParse(mountArgs[2], out int start) &&
                                    int.TryParse(mountArgs[3], out int length))
                                {
                                    byte[] readBytes = fakefs.FileRead(mountArgs[1], start, length);
                                    BytePrinter.TextPrint(readBytes);
                                }
                                else
                                {
                                    Console.WriteLine("Error parsing read indexes.");
                                }
                            }
                            // DELETE <FILEPATH>
                            //
                            else if (Command.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("DELETE");
                                    continue;
                                }
                                fakefs.DeleteFile(mountArgs[1]);
                            }
                            // RMDIR <FILEPATH>
                            //
                            else if (Command.Equals("RMDIR", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("RMDIR");
                                    continue;
                                }
                                fakefs.DeleteFile(mountArgs[1], true);
                            }
                            // TRUNCATE <FILEPATH>
                            //
                            else if (Command.Equals("TRUNCATE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("TRUNCATE");
                                    continue;
                                }
                                fakefs.TruncateFile(mountArgs[1]);
                            }
                            // INFO <FILEPATH>
                            //
                            else if (Command.Equals("INFO", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mountArgs.Length != 2)
                                {
                                    ArgsError("INFO");
                                    continue;
                                }

                                if (fakefs.TryGetFile(mountArgs[1], out Tuple<DirectoryEntry, uint> infoFile))
                                {
                                    DirectoryEntry entry = infoFile.Item1;
                                    Console.Write($"\r\n {entry.FileName}");
                                    PrintAttributes(entry);
                                    Console.WriteLine();
                                    if (entry.Size < 1000)
                                        Console.Write($" SIZE: {entry.Size.ToString(),3:###} Bytes");
                                    else
                                        Console.Write($" SIZE: {(entry.Size / 1000f).ToString(),3:###.#} KB");
                                    Console.WriteLine();
                                    Console.Write($" CRE: {entry.CreateDate} {entry.CreateTime}");
                                    Console.Write($"   MOD: {entry.ModifiedDate} {entry.ModifiedTime}");
                                    Console.WriteLine("\r\n");
                                }
                                else
                                {
                                    Console.WriteLine($"File \"{mountArgs[1]}\" not found.");
                                }
                            }
                            // SET <FILEPATH> <ATTRIBUTE>=<TRUE || FALSE>
                            // SET <>
                            //
                            else if (Command.Equals("SET", StringComparison.OrdinalIgnoreCase))
                            {
                                void attribError()
                                {
                                    Console.WriteLine("Error: Invalid target given.");
                                }
                                if (mountArgs.Length == 3)
                                {
                                    if (fakefs.TryGetFile(mountArgs[1], out Tuple<DirectoryEntry, uint> setFile))
                                    {
                                        string[] attribs = { "READONLY", "HIDDEN", "SYSTEM", "ARCHIVE" };
                                        if (attribs.Any(x => mountArgs[2].ToUpper().StartsWith(x)))
                                        {
                                            string[] setAction = mountArgs[2].Split('=');
                                            if (bool.TryParse(setAction[1], out bool newValue))
                                            {
                                                if (setAction[0].Equals(attribs[0], StringComparison.OrdinalIgnoreCase))
                                                    setFile.Item1.isReadOnly = newValue;
                                                else if (setAction[0].Equals(attribs[1], StringComparison.OrdinalIgnoreCase))
                                                    setFile.Item1.isHidden = newValue;
                                                else if (setAction[0].Equals(attribs[2], StringComparison.OrdinalIgnoreCase))
                                                    setFile.Item1.isSystem = newValue;
                                                else if (setAction[0].Equals(attribs[3], StringComparison.OrdinalIgnoreCase))
                                                    setFile.Item1.isArchive = newValue;
                                                else
                                                {
                                                    attribError();
                                                    continue;
                                                }

                                                fakefs.UpdateDirectoryEntry(setFile);
                                                continue;
                                            }
                                        }

                                        attribError();
                                    }
                                    else
                                    {
                                        Console.WriteLine($"File \"{mountArgs[1]}\" not found.");
                                    }
                                }
                                else if (mountArgs.Length == 1)
                                {
                                    List<Tuple<DirectoryEntry, uint>> contents = fakefs.GetDirContents(mountArgs[1]);
                                    for (int i = 0; i < contents.Count; i++)
                                    {
                                        Console.Write($" {contents[i].Item1.FileName}");
                                        PrintAttributes(contents[i].Item1);
                                        Console.WriteLine();
                                    }
                                }
                                else
                                    ArgsError("SET");
                            }
                            // EXIT / UNMOUNT
                            //
                            else if (new[] { "EXIT", "UNMOUNT" }.Contains(Command.ToUpper()))
                            {
                                fakefs.UnMount();
                            }
                            else
                            {
                                Console.WriteLine("Error: unrecognized command.");
                            }

                            void PrintAttributes(DirectoryEntry entry)
                            {
                                if (entry.isReadOnly)
                                    Console.Write($" R");
                                else
                                    Console.Write($" -");

                                if (entry.isHidden)
                                    Console.Write($" H");
                                else
                                    Console.Write($" -");

                                if (entry.isSystem)
                                    Console.Write($" S");
                                else
                                    Console.Write($" -");

                                if (entry.isDirectory)
                                    Console.Write($" D");
                                else
                                    Console.Write($" -");

                                if (entry.isArchive)
                                    Console.Write($" A");
                                else
                                    Console.Write($" -");
                            }
                        }
                        catch (ApplicationException err)
                        {
                            Console.WriteLine($" {err.Message}");
                        }
                    }
                }
                catch (ApplicationException err)
                {
                    Console.WriteLine($" {err.Message}");
                }
            }
            #endregion

            #region Testing suite

            // Series of operations that verifies command functions
            else if (Command.Equals("TEST", StringComparison.OrdinalIgnoreCase))
            {
                Guid testHash = Guid.NewGuid();
                string testVolName = $"Test{testHash}";
                string testFile1 = "emptyFile";
                string testFile2 = "writeFile";
                string testDir = "directory";
                Console.WriteLine("\r\n** Test suite engaged. **");

                Console.WriteLine("\r\n  Testing data structures...");
                DirectoryEntry testFile = new DirectoryEntry("TestFile", 0);
                byte[] fileDump = testFile.Serialize();
                DirectoryEntry testCopy = new DirectoryEntry(fileDump);
                Debug.Assert(testFile.Equals(testCopy));
                Console.WriteLine("    Entry serialization success.");

                Console.WriteLine("\r\n  Testing volume functions...");
                FileSystem.Allocate(testVolName, 256, 8);
                Console.WriteLine("    Volume creation success.");
                fakefs = FileSystem.Mount(testVolName);
                Debug.Assert(fakefs != null);
                Console.WriteLine("    Volume mount success.");

                Console.WriteLine("\r\n  Testing file functions...");
                fakefs.CreateFile(testFile1);
                fakefs.CreateFile(testFile2);
                Debug.Assert(
                    fakefs.GetDirContents("").Any(x =>
                        testFile1.Equals(x.Item1.FileName.Trim())) &&
                    fakefs.GetDirContents("").Any(x => 
                        testFile2.Equals(x.Item1.FileName.Trim())));
                Console.WriteLine("    File creation success.");

                string testString = "A testing string to write to the file.";
                byte[] writeTest = Encoding.ASCII.GetBytes(testString);
                fakefs.FileWrite(testFile2, 0, writeTest);
                byte[] readTest = fakefs.FileRead(testFile2, 0, writeTest.Length);
                Debug.Assert(writeTest.SequenceEqual(readTest));
                Console.WriteLine("    File read/write success.");

                Tuple<DirectoryEntry, uint> setFile = fakefs.GetDirContents("").Where(x =>
                    x.Item1.FileName.Trim() == testFile1).FirstOrDefault();
                setFile.Item1.isReadOnly = true;
                fakefs.UpdateDirectoryEntry(setFile);
                bool failed = false;
                try
                {
                    fakefs.DeleteFile(testFile1);
                }
                catch (ApplicationException)
                {
                    failed = true;
                }
                Debug.Assert(failed);
                Console.WriteLine("    File set attributes success.");

                setFile.Item1.isReadOnly = false;
                fakefs.UpdateDirectoryEntry(setFile);
                fakefs.DeleteFile(testFile1);
                Debug.Assert(!fakefs.GetDirContents("").Any(x =>
                    testFile1.Equals(x.Item1.FileName.Trim())));
                Console.WriteLine("    File delete success.");

                fakefs.TruncateFile(testFile2);
                Tuple<DirectoryEntry, uint> truncFile = fakefs.GetDirContents("").Where(x =>
                    x.Item1.FileName.Trim() == testFile2).FirstOrDefault();
                Debug.Assert(truncFile.Item1.Size == 0);
                Console.WriteLine("    File truncate success.");

                Console.WriteLine("\r\n  Testing directory functions...");
                fakefs.CreateFile(testDir, true);
                Tuple<DirectoryEntry, uint> newDir = fakefs.GetDirContents("").Where(x =>
                    x.Item1.FileName.Trim() == testDir).FirstOrDefault();
                Debug.Assert(newDir != null && newDir.Item1.isDirectory);
                Console.WriteLine("    Directory creation success.");

                string folderString = $"/{testDir}";
                fakefs.MoveFile(testFile2, $"{folderString}/{testFile2}");
                Tuple<DirectoryEntry, uint> oldLoc = fakefs.GetDirContents("").Where(x =>
                    x.Item1.FileName.Trim() == testFile2).FirstOrDefault();
                Tuple<DirectoryEntry, uint> newLoc = fakefs.GetDirContents(folderString).Where(x =>
                    x.Item1.FileName.Trim() == testFile2).FirstOrDefault();
                Debug.Assert(oldLoc == null && newLoc != null);
                Console.WriteLine("    File movement success.");

                string fsLocation = fakefs.ChangeDirectory(folderString);
                Tuple<DirectoryEntry, uint> movedFile = fakefs.GetDirContents("").Where(x =>
                    x.Item1.FileName.Trim() == testFile2).FirstOrDefault();
                Debug.Assert(movedFile != null && fsLocation == folderString);
                Console.WriteLine("    Directory traversal success.");

                fakefs.ChangeDirectory("..");
                try
                {
                    fakefs.DeleteFile(testDir, true);
                }
                catch (ApplicationException)
                {
                    failed = true;
                }
                Debug.Assert(failed);
                fakefs.DeleteFile($"{folderString}/{testFile2}");
                fakefs.DeleteFile(testDir, true);
                Debug.Assert(fakefs.GetDirContents("").Count == 0);
                Console.WriteLine("    Directory delete success.");

                Console.WriteLine("\r\n  Testing volume cleanup functions...");
                fakefs.UnMount();
                Debug.Assert(!fakefs.isMounted);
                Console.WriteLine("    Volume unmount success.");

                byte[] outputState = FileSystem.Dump(testVolName);
                Debug.Assert(outputState != null);
                Console.WriteLine("    Volume dump success.");

                FileSystem.Truncate(testVolName);
                fakefs = FileSystem.Mount(testVolName);
                Debug.Assert(fakefs.GetDirContents("").Count == 0);
                Tuple<long, long> volSpace = fakefs.GetFreeSpace();
                Debug.Assert(volSpace.Item1 == volSpace.Item2);
                fakefs.UnMount();
                Console.WriteLine("    Volume truncate success.");

                FileSystem.Deallocate(testVolName);
                try
                {
                    FileSystem.Mount(testFile1);
                }
                catch (ApplicationException)
                {
                    failed = true;
                }
                Debug.Assert(failed);
                Console.WriteLine("    Volume deallocate success.");

                Console.WriteLine($"\r\n\r\n Dumped data of Volume {testVolName}:\r\n");
                BytePrinter.HexPrint(outputState);
                Console.WriteLine("** All tests complete! **");
            }
            #endregion
            else
            {
                Console.WriteLine("Error: unrecognized command.");
                return;
            }
        }
    }
}
