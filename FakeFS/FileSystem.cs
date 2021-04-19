using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace FakeFS
{
    public class FileSystem
    {
        // Version 0.1, increment on changes
        // Constants
        private static int HEAD_SIZE = 32;
        private static int FAT_ENTRY_SIZE = 4;
        private static int FAT_PAGE_SIZE = 512;
        private static int DIR_ENTRY_SIZE = DirectoryEntry.EntrySize;
        private static int DIR_PAGE_SIZE = 1024;

        private static int FILE_SIG = 0xFA4B;
        private static int BLOCK_SIG = 0xFFFF;
        private static uint FAT_SIG = 0xFFFFFA4B;
        private static uint EOF_SIG = 0xFFFFFFFF;
        private static ulong DIR_SIG = 0xFFFFFFFFFFFFFFFF;

        private static long VOL_SIZE;
        private static long PAGE_SIZE;
        private static List<string> DIR_PATH;
        private static FileStream m_file = null;
        private static FileSystem m_fs = null;
        private static float version = 0.1f;

        #region Mount/Unmount

        private FileSystem(string VolumeName)
        {
            m_file = new FileStream(VolumeName, FileMode.Open);
        }

        // Assert volume header is correct and load, returning an instance of a file system
        public static FileSystem Mount(string VolumeName)
        {
            if (File.Exists(VolumeName))
            {
                void VolErr()
                {
                    throw new ApplicationException($"Error: Volume is improper format or corrupt.");
                }

                FileInfo volFile = new FileInfo(VolumeName);

                using (BinaryReader reader = new BinaryReader(volFile.Open(FileMode.Open)))
                {
                    // signature, version, volume size, page size
                    if (Convert.ToInt32(reader.ReadUInt16()) == FILE_SIG)
                    {
                        short leftSide = reader.ReadByte();
                        short rightSide = reader.ReadByte();
                        if ((float)(leftSide + (rightSide * .1)) != version)
                            VolErr();

                        VOL_SIZE = reader.ReadInt64();
                        PAGE_SIZE = reader.ReadInt64();

                        // start directory pointer at root
                        DIR_PATH = new List<string>();
                        DIR_PATH.Add("");
                    }
                    else
                        VolErr();
                    // bypass all of the padding bytes
                    reader.ReadBytes(HEAD_SIZE - (2 * sizeof(byte))
                        - (2 * sizeof(UInt16)) - (2 * sizeof(long)));
                    // verify tail is in expected position
                    if (Convert.ToInt32(reader.ReadUInt16()) != BLOCK_SIG)
                        VolErr();
                }

                m_fs = new FileSystem(VolumeName);

                return m_fs;
            }
            else
            {
                throw new ApplicationException($"Volume {VolumeName} not found.");
            }
        }

        public void UnMount()
        {
            if (m_file != null)
            {
                m_file.Close();
                m_file.Dispose();
                m_file = null;

                m_fs = null;
            }
        }
        #endregion

        #region Static volume methods

        public static void Allocate(string VolumeName, long VolumeSize, long PageSize)
        {
            using (FileStream writer = File.Open(VolumeName, FileMode.Create))
            {
                WriteVolumeHead(writer, VolumeSize, PageSize);
            }
            if (!File.Exists(VolumeName))
            {
                throw new ApplicationException("Error: Volume not created!?");
            }
        }

        public static void Deallocate(string VolumeName)
        {
            if (File.Exists(VolumeName))
            {
                File.Delete(VolumeName);
            }
            else
            {
                throw new ApplicationException("Error: Volume does not exist.");
            }
        }

        public static void Truncate(string VolumeName)
        {
            if (File.Exists(VolumeName))
            {
                FileSystem tempfs = FileSystem.Mount(VolumeName);
                tempfs.UnMount();

                using (FileStream writer = File.Open(VolumeName, FileMode.Open))
                {
                    WriteVolumeHead(writer, VOL_SIZE, PAGE_SIZE);
                }
            }
            else
            {
                throw new ApplicationException("Error: Volume does not exist.");
            }
        }

        public static byte[] Dump(string VolumeName)
        {
            if (File.Exists(VolumeName))
            {
                return File.ReadAllBytes(VolumeName);
            }
            else
            {
                throw new ApplicationException($"Volume {VolumeName} not found.");
            }
        }

        public bool isMounted
        {
            get
            {
                return (m_fs != null); 
            }
        }
        #endregion

        #region Volume entry helpers

        // Returns <total space, free space> of volume
        public Tuple<long, long> GetFreeSpace()
        {
            // start immediately after volume head
            m_file.Seek(HEAD_SIZE, SeekOrigin.Begin);

            int totalBlocks = (int)(VOL_SIZE / PAGE_SIZE);
            int freeBlocks = 0;
            for (int i = 0; i < totalBlocks; i++)
            {
                byte[] intBuffer = new byte[FAT_ENTRY_SIZE];
                m_file.Read(intBuffer, 0, FAT_ENTRY_SIZE);

                uint clusterValue = BitConverter.ToUInt32(intBuffer, 0);
                if (clusterValue == 0)
                {
                    freeBlocks++;
                }
                // End of FAT page delimiter, jump to next page and decrement index
                else if (clusterValue == FAT_SIG)
                {
                    m_file.Read(intBuffer, 0, sizeof(int));
                    uint nextPage = BitConverter.ToUInt32(intBuffer, 0);
                    if (nextPage != 0)
                    {
                        m_file.Seek(nextPage, SeekOrigin.Begin);
                        i--;
                    }
                    else
                    {
                        freeBlocks += (totalBlocks - i);
                        break;
                    }
                }
            }

            return new Tuple<long, long>(VOL_SIZE, freeBlocks * PAGE_SIZE);
        }
        private static void WriteVolumeHead(FileStream volume, long volSize, long pageSize)
        {
            volume.Seek(0, SeekOrigin.Begin);
            // Write out volume header:
            //   file signature: FA4B
            //   (2) 8-bit numbers that represent to a version number
            //   64-bit number: volume size
            //   64-bit number: page size
            // end with padding and delimiter (10 bytes of padding/future features)
            volume.Write(BitConverter.GetBytes(Convert.ToUInt16(FILE_SIG)), 0, sizeof(UInt16));
            volume.WriteByte((byte)0);
            volume.WriteByte((byte)1);
            volume.Write(BitConverter.GetBytes(volSize), 0, sizeof(Int64));
            volume.Write(BitConverter.GetBytes(pageSize), 0, sizeof(Int64));

            int headPad = HEAD_SIZE - (2 * sizeof(byte)) - (2 * sizeof(UInt16)) 
                - (2 * sizeof(long));
            for (int i = 0; i < headPad; i++)
            {
                volume.WriteByte((byte)'\0');
            }

            volume.Write(BitConverter.GetBytes(Convert.ToUInt16(BLOCK_SIG)), 0, sizeof(UInt16));

            // Write out a FAT page and root Dir entry
            WriteFATPage(volume);
            WriteDirPage(volume);

            for (int i = 0; i < volSize; i++)
            {
                volume.WriteByte((byte)'\0');
            }
        }
        private static void WriteFATPage(FileStream stream)
        {
            int fatTailIndex = ((FAT_PAGE_SIZE / FAT_ENTRY_SIZE) - 2) * FAT_ENTRY_SIZE;

            for (long i = 0; i < fatTailIndex; i++)
            {
                stream.WriteByte((byte)'\0');
            }

            stream.Write(BitConverter.GetBytes(FAT_SIG), 0, sizeof(int));

            for (long i = fatTailIndex + sizeof(UInt16) * 2; i < FAT_PAGE_SIZE; i++)
            {
                stream.WriteByte((byte)'\0');
            }
        }
        private static void WriteDirPage(FileStream stream)
        {
            int dirTailIndex = (int)Math.Floor((double)(DIR_PAGE_SIZE / DIR_ENTRY_SIZE)) * DIR_ENTRY_SIZE;

            for (long i = 0; i < dirTailIndex; i++)
            {
                stream.WriteByte((byte)'\0');
            }

            stream.Write(BitConverter.GetBytes(DIR_SIG), 0, sizeof(UInt64));

            for (long i = dirTailIndex + sizeof(UInt64); i < DIR_PAGE_SIZE; i++)
            {
                stream.WriteByte((byte)'\0');
            }
        }
        // Iterate through cluster entries until a blank one is found, return index
        // of the empty cluster after one is found
        private uint GetFreeCluster()
        {
            // start immediately after volume head
            m_file.Seek(HEAD_SIZE, SeekOrigin.Begin);

            uint pageZero = (uint)HEAD_SIZE;

            int clusterIndex = 0;
            uint address = 1;
            while (address != 0)
            {
                byte[] fatBuffer = new byte[FAT_ENTRY_SIZE];
                m_file.Read(fatBuffer, 0, FAT_ENTRY_SIZE);

                address = BitConverter.ToUInt32(fatBuffer, 0);
                // End of FAT page delimiter, jump to next page
                if (address == FAT_SIG)
                {
                    m_file.Read(fatBuffer, 0, FAT_ENTRY_SIZE);
                    uint nextPage = Convert.ToUInt32(fatBuffer);
                    // Create new page if one does not exist
                    if (nextPage == 0)
                    {
                        uint pageIndex = (uint)m_file.Seek(0, SeekOrigin.End);
                        WriteFATPage(m_file);
                        byte[] pageAddress = BitConverter.GetBytes(pageIndex);
                        m_file.Write(pageAddress, 0, pageAddress.Length);
                        nextPage = Convert.ToUInt32(pageAddress);
                    }
                    // Jump cursor to next page and store address
                    m_file.Seek(nextPage, SeekOrigin.Begin);
                    pageZero = nextPage;
                    clusterIndex = 0;
                    // Prevent index increment
                    continue;
                }

                if (address != 0)
                    clusterIndex++;
            }

            return (uint)((clusterIndex * FAT_ENTRY_SIZE) + pageZero);
        }
        // Iterate through directory entries until a blank one is found, return index
        // of the empty slot after one is found
        private uint GetFreeDirEntry(uint dirZero)
        {
            uint pageZero = dirZero;
            m_file.Seek(pageZero, SeekOrigin.Begin);

            int clusterIndex = 0;
            bool entryFound = false;
            while (!entryFound)
            {
                byte[] dirEntry = new byte[DIR_ENTRY_SIZE];
                m_file.Read(dirEntry, 0, DIR_ENTRY_SIZE);

                ulong firstLong = BitConverter.ToUInt64(dirEntry.Take(8).ToArray(), 0);
                // End of Directory page delimiter, jump to next page
                if (firstLong == DIR_SIG)
                {
                    byte[] intBuffer = new byte[sizeof(int)];
                    m_file.Read(intBuffer, 0, sizeof(int));
                    uint nextPage = Convert.ToUInt32(intBuffer);
                    // Create new page if one does not exist
                    if (nextPage == 0)
                    {
                        uint pageIndex = (uint)m_file.Seek(0, SeekOrigin.End);
                        WriteDirPage(m_file);
                        byte[] pageAddress = BitConverter.GetBytes(pageIndex);
                        m_file.Write(pageAddress, 0, pageAddress.Length);
                        nextPage = Convert.ToUInt32(pageAddress);
                    }
                    // Jump cursor to next page and store address
                    m_file.Seek(nextPage, SeekOrigin.Begin);
                    pageZero = nextPage;
                    clusterIndex = 0;
                    // Prevent index increment
                    continue;
                }

                if (dirEntry.All(x => x.Equals(0)))
                {
                    entryFound = true;
                }

                if (!entryFound)
                    clusterIndex++;
            }

            return (uint)((clusterIndex * DIR_ENTRY_SIZE) + pageZero);
        }
        #endregion

        #region File functions

        private List<Tuple<string, uint>> ResolvePath(string[] FilePath)
        {
            // Work off of path list in memory
            if (FilePath.Length == 0 || (FilePath.Length == 1 && FilePath[0] == ""))
            {
                FilePath = DIR_PATH.ToArray();
            }
            // Absolute- trace input path
            else if (FilePath[0] == "")
            {
                // Use input values
            }
            // Relative- add input path to current position
            else
            {
                var totalPath = new List<string>();
                totalPath.AddRange(DIR_PATH.ToArray());
                totalPath.AddRange(FilePath);
                FilePath = totalPath.ToArray();
            }

            var addressChain = new List<Tuple<string, uint>>();
            addressChain.Add(new Tuple<string, uint>("", (uint)(HEAD_SIZE + FAT_PAGE_SIZE)));

            for (int i = 1; i < FilePath.Length; i++)
            {
                if (FilePath[i] == "..")
                {
                    if (addressChain.Count > 1)
                    {
                        addressChain.RemoveAt(addressChain.Count - 1);
                        continue;
                    }
                    else
                    {
                        throw new ApplicationException($"Maximum folder height reached.");
                    }
                }
                else
                {
                    List<Tuple<DirectoryEntry, uint>> contents = GetDirContents(
                        addressChain[addressChain.Count - 1].Item2);
                    Tuple<DirectoryEntry, uint> entry = contents.Where(x =>
                        FilePath[i].Equals(x.Item1.FileName.Trim())
                        && x.Item1.isDirectory).FirstOrDefault();
                    if (entry == null)
                    {
                        throw new ApplicationException($"Folder {FilePath[i]} not found.");
                    }
                    else
                    {
                        addressChain.Add(new Tuple<string, uint>(FilePath[i], 
                            (uint)entry.Item1.StartCluster));
                    }
                }
            }

            return addressChain;
        }

        public bool TryGetFile(string FilePath, out Tuple<DirectoryEntry, uint> foundFile)
        {
            string[] filePath = FilePath.Split('/');
            string FileName = filePath[filePath.Length - 1];
            uint dirAddress;

            var addressPath = ResolvePath(filePath.Take(filePath.Length - 1).ToArray());
            dirAddress = addressPath[addressPath.Count - 1].Item2;

            // Verify file exists
            foundFile = GetDirContents(dirAddress).Where(x =>
                x.Item1.FileName.Trim() == FileName).FirstOrDefault();

            if (foundFile == null)
                return false;
            else
                return true;
        }

        public void CreateFile(string FilePath, bool isDir = false)
        {
            string[] filePath = FilePath.Split('/');
            string FileName = filePath[filePath.Length - 1];

            var addressPath = ResolvePath(filePath.Take(filePath.Length - 1).ToArray());
            uint dirAddress = addressPath[addressPath.Count - 1].Item2;

            if (TryGetFile(FilePath, out _))
            {
                throw new ApplicationException($"File {FilePath} already exists.");
            }

            if (isDir)
            {
                uint pageAddress = (uint)m_file.Seek(0, SeekOrigin.End);
                WriteDirPage(m_file);
                DirectoryEntry newEntry = new DirectoryEntry(FileName, pageAddress);
                newEntry.isDirectory = true;

                // Overwrite open entry with new file's metadata
                uint entryAddress = GetFreeDirEntry(dirAddress);
                m_file.Seek(entryAddress, SeekOrigin.Begin);
                m_file.Write(newEntry.Serialize(), 0, DIR_ENTRY_SIZE);
            }
            else
            {
                if (GetFreeSpace().Item2 < PAGE_SIZE)
                {
                    throw new ApplicationException("Not enough free space to create file.");
                }

                uint freeCluster = GetFreeCluster();
                DirectoryEntry newEntry = new DirectoryEntry(FileName, freeCluster);

                // Overwrite open entry with new file's EOF
                byte[] EOF = BitConverter.GetBytes(EOF_SIG);
                m_file.Seek(freeCluster, SeekOrigin.Begin);
                m_file.Write(EOF, 0, FAT_ENTRY_SIZE);

                // Overwrite open entry with new file's metadata
                uint entryAddress = GetFreeDirEntry(dirAddress);
                m_file.Seek(entryAddress, SeekOrigin.Begin);
                m_file.Write(newEntry.Serialize(), 0, DIR_ENTRY_SIZE);
            }
        }

        public void MoveFile(string InFilePath, string OutFilePath)
        {

            if (TryGetFile(OutFilePath, out _))
            {
                throw new ApplicationException($"File {OutFilePath} already exists.");
            }

            if (TryGetFile(InFilePath, out Tuple<DirectoryEntry, uint> mvFile))
            {
                string[] inPath = InFilePath.Split('/');
                string[] outPath = OutFilePath.Split('/');
                var inDir = ResolvePath(inPath.Take(inPath.Length - 1).ToArray());
                var outDir = ResolvePath(outPath.Take(outPath.Length - 1).ToArray());
                
                // Just rename file
                if (inDir.SequenceEqual(outDir))
                {
                    mvFile.Item1.FileName = outPath[outPath.Length - 1];
                    UpdateDirectoryEntry(mvFile);
                }
                else
                {
                    // Find open address in destination directory entry page and write
                    // entry of file there
                    if (outPath[outPath.Length - 1] != inPath[inPath.Length - 1])
                    {
                        mvFile.Item1.FileName = outPath[outPath.Length - 1];
                    }

                    uint dirAddress = outDir[outDir.Count - 1].Item2;
                    uint newEntryAddress = GetFreeDirEntry(dirAddress);
                    var movedFile = new Tuple<DirectoryEntry, uint>(mvFile.Item1, 
                        newEntryAddress);
                    UpdateDirectoryEntry(movedFile);

                    // Overwrite old entry with null characters
                    uint oldAddress = mvFile.Item2;
                    m_file.Seek(oldAddress, SeekOrigin.Begin);
                    for (int i = 0; i < DIR_ENTRY_SIZE; i++)
                    {
                        m_file.WriteByte((byte)'\0');
                    }
                }
            }
            else
            {
                throw new ApplicationException($"File \"{InFilePath}\" not found.");
            }
        }

        // TODO: copy file

        // TODO: FS links

        public string ChangeDirectory(string FilePath)
        {
            var pathAddress = ResolvePath(FilePath.Split('/'));

            DIR_PATH.Clear();
            for (int i = 0; i < pathAddress.Count; i++)
            {
                DIR_PATH.Add(pathAddress[i].Item1);
            }

            return string.Join("/", DIR_PATH.ToArray());
        }

        // Return a list of Tuples with files' directory entry and the address of 
        // their entries.
        public List<Tuple<DirectoryEntry, uint>> GetDirContents(uint pageAddress)
        {
            m_file.Seek(pageAddress, SeekOrigin.Begin);
            var contents = new List<Tuple<DirectoryEntry, uint>>();

            uint entryAddress = pageAddress;
            while (true)
            {
                byte[] dirEntry = new byte[DIR_ENTRY_SIZE];
                m_file.Read(dirEntry, 0, DIR_ENTRY_SIZE);

                // Check first 8 bytes of block for end of Directory page delimiter
                ulong firstLong = BitConverter.ToUInt64(dirEntry.Take(8).ToArray(), 0);
                if (firstLong == DIR_SIG)
                {
                    // Check next 4 to see if another page exists
                    uint nextPage = BitConverter.ToUInt32(dirEntry.Skip(8).Take(4).ToArray(), 0);
                    // End if no next page
                    if (nextPage == 0)
                    {
                        break;
                    }
                    // Jump cursor to next page to continue iteration
                    else
                    {
                        m_file.Seek(nextPage, SeekOrigin.Begin);
                        entryAddress = nextPage;
                    }
                }
                else if (dirEntry.All(x => x.Equals(0)))
                {
                    // Skip if blank area found to search whole pages
                    // (will account for deleted entries in folder)
                }
                else
                {
                    contents.Add(new Tuple<DirectoryEntry, uint>(
                        new DirectoryEntry(dirEntry), entryAddress));
                }
                // Increment address tracker by entry size
                entryAddress += (uint)DIR_ENTRY_SIZE;
            }

            return contents;
        }
        public List<Tuple<DirectoryEntry, uint>> GetDirContents(string FilePath)
        {
            // TODO: recursive directory listing

            uint dirAddress;

            var addressPath = ResolvePath(FilePath.Split('/'));
            dirAddress = addressPath[addressPath.Count - 1].Item2;

            return GetDirContents(dirAddress);
        }

        public void UpdateDirectoryEntry(Tuple<DirectoryEntry, uint> entry, long? size = null)
        {
            entry.Item1.Update(size);
            m_file.Seek(entry.Item2, SeekOrigin.Begin);
            m_file.Write(entry.Item1.Serialize(), 0, DIR_ENTRY_SIZE);
        }

        private uint GetFileIndexAddress(DirectoryEntry entry, int index)
        {
            // Find index of location in file
            uint indAddress;
            if (index > PAGE_SIZE)
            {
                // Find page number and find its index
                int targetPage = (int)(index / PAGE_SIZE);
                uint pageIndex = (uint)entry.StartCluster;

                byte[] intBuffer = new byte[sizeof(int)];
                for (int i = 0; i < targetPage; i++)
                {
                    if (pageIndex == EOF_SIG)
                    {
                        throw new ApplicationException("Selected index outside bounds of file.");
                    }
                    m_file.Seek(pageIndex, SeekOrigin.Begin);
                    m_file.Read(intBuffer, 0, sizeof(int));
                    pageIndex = BitConverter.ToUInt32(intBuffer, 0);
                }

                // Find index of page start and add modulo
                uint pageAddress = (uint)((pageIndex - HEAD_SIZE) / 4 * PAGE_SIZE
                    + HEAD_SIZE + FAT_PAGE_SIZE + DIR_PAGE_SIZE);
                indAddress = (uint)(index % PAGE_SIZE) + pageAddress;
            }
            else
            {
                // Find index in file's page
                uint pageAddress = (uint)((entry.StartCluster - HEAD_SIZE) / 4 * PAGE_SIZE
                    + HEAD_SIZE + FAT_PAGE_SIZE + DIR_PAGE_SIZE);
                indAddress = (uint)(index + pageAddress);
            }

            return indAddress;
        }

        // Get address of file data block, write to it, if data overflows the block
        // write another block
        public void FileWrite(string FilePath, int start, byte[] data)
        {
            if (TryGetFile(FilePath, out Tuple<DirectoryEntry, uint> writeFile))
            {
                if (start > writeFile.Item1.Size || start < 0)
                {
                    throw new ApplicationException("Selected index outside bounds of file.");
                }

                long writeIndex = start + data.Length;
                long fileSize = writeFile.Item1.Size;

                if (writeIndex > fileSize)
                {
                    byte[] addressBuffer = new byte[FAT_ENTRY_SIZE];
                    uint firstAddress = (uint)writeFile.Item1.StartCluster;
                    var addressList = new List<uint>();
                    addressList.Add(firstAddress);

                    // Find clusters the file is already using
                        // First
                    m_file.Seek(firstAddress, SeekOrigin.Begin);
                    m_file.Read(addressBuffer, 0, FAT_ENTRY_SIZE);
                    uint fatEntry = BitConverter.ToUInt32(addressBuffer, 0);
                        // Loop until EOF
                    long sizeAllocated = PAGE_SIZE;
                    while (fatEntry != EOF_SIG)
                    {
                        addressList.Add(fatEntry);
                        sizeAllocated += PAGE_SIZE;
                        m_file.Seek(fatEntry, SeekOrigin.Begin);
                        m_file.Read(addressBuffer, 0, FAT_ENTRY_SIZE);
                        fatEntry = BitConverter.ToUInt32(addressBuffer, 0);
                    }
                    // Allocate more clusters if necessary
                    if (writeIndex > sizeAllocated)
                    {
                        int totalClusters = (int)(writeIndex / PAGE_SIZE);
                        if (writeIndex % PAGE_SIZE != 0)
                        {
                            totalClusters++;
                        }

                        int reqClusters = totalClusters - (int)(sizeAllocated / PAGE_SIZE);

                        uint clusterAddress = addressList[addressList.Count - 1];
                        for (int i = 0; i < reqClusters; i++)
                        {
                            // Allocate a free cluster for file, write it to previous cluster
                            uint freeCluster = GetFreeCluster();
                            m_file.Seek(clusterAddress, SeekOrigin.Begin);
                            m_file.Write(BitConverter.GetBytes(freeCluster), 0, FAT_ENTRY_SIZE);
                            // Data is written to the allocated cluster so it doesn't 
                            // register as free in the GetFreeCluster() call
                            byte[] EOF = BitConverter.GetBytes(EOF_SIG);
                            m_file.Seek(freeCluster, SeekOrigin.Begin);
                            m_file.Write(EOF, 0, FAT_ENTRY_SIZE);

                            // Free cluster address is stored for next loop
                            clusterAddress = freeCluster;
                        }
                    }
                }

                uint tarAddress = GetFileIndexAddress(writeFile.Item1, start);
                m_file.Seek(tarAddress, SeekOrigin.Begin);
                for (int i = 0; i < data.Length; i++)
                {
                    if ((i + start) % PAGE_SIZE == PAGE_SIZE - 1)
                    {
                        tarAddress = GetFileIndexAddress(writeFile.Item1, start + i);
                        m_file.Seek(tarAddress, SeekOrigin.Begin);
                    }

                    m_file.Write(data, i, 1);
                }

                // Directory entry updated and written to disk
                if (writeIndex > fileSize)
                {
                    UpdateDirectoryEntry(writeFile, writeIndex);
                }
                else
                {
                    UpdateDirectoryEntry(writeFile);
                }
            }
            else
            {
                throw new ApplicationException($"File \"{FilePath}\" not found.");
            }
        }

        public byte[] FileRead(string FilePath, int start, int count)
        {
            byte[] readBytes = new byte[count];
            if (TryGetFile(FilePath, out Tuple<DirectoryEntry, uint> readFile))
            {
                if (((start + count) > readFile.Item1.Size) || start < 0 || count < 0)
                {
                    throw new ApplicationException("Selected index outside bounds of file.");
                }

                uint tarAddress = GetFileIndexAddress(readFile.Item1, start);
                m_file.Seek(tarAddress, SeekOrigin.Begin);

                for (int i = 0; i < count; i++)
                {
                    if ((i + start) % PAGE_SIZE == PAGE_SIZE - 1)
                    {
                        tarAddress = GetFileIndexAddress(readFile.Item1, i + start);
                        m_file.Seek(tarAddress, SeekOrigin.Begin);
                    }

                    m_file.Read(readBytes, i, 1);
                }
            }
            else
            {
                throw new ApplicationException($"File \"{FilePath}\" not found.");
            }

            return readBytes;
        }

        private void ZeroFile(uint startAddress)
        {
            // FAT entries follows cluster addresses until eof is read
            // read bytes, move cursor back, write null value to cluster
            byte[] addBuffer = new byte[FAT_ENTRY_SIZE];
            m_file.Seek(startAddress, SeekOrigin.Begin);
            m_file.Read(addBuffer, 0, FAT_ENTRY_SIZE);
            uint addressValue = BitConverter.ToUInt32(addBuffer, 0);
            while (true)
            {
                m_file.Seek(-FAT_ENTRY_SIZE, SeekOrigin.Current);
                for (int i = 0; i < FAT_ENTRY_SIZE; i++)
                {
                    m_file.WriteByte((byte)'\0');
                }

                if (addressValue == EOF_SIG)
                {
                    break;
                }
                else
                {
                    m_file.Seek(addressValue, SeekOrigin.Begin);
                    m_file.Read(addBuffer, 0, FAT_ENTRY_SIZE);
                    addressValue = BitConverter.ToUInt32(addBuffer, 0);
                }
            }
        }

        public void DeleteFile(string FilePath, bool isDir = false, bool recursive = false)
        {
            if (TryGetFile(FilePath, out Tuple<DirectoryEntry, uint> deleteFile))
            {
                if (deleteFile.Item1.isReadOnly)
                {
                    throw new ApplicationException($"Error: File \"{FilePath}\" is read-only.");
                }
                else if (!isDir && deleteFile.Item1.isDirectory)
                {
                    throw new ApplicationException($"Error: \"{FilePath}\" is a directory.");
                }
                else if (isDir && !deleteFile.Item1.isDirectory)
                {
                    throw new ApplicationException($"Error: \"{FilePath}\" is not a directory.");
                }

                if (isDir)
                {
                    if (GetDirContents((uint)deleteFile.Item1.StartCluster).Count > 0 && !recursive)
                    {
                        throw new ApplicationException($"Error: File \"{FilePath}\" is not empty.");
                    }

                    // TODO: recursive delete

                    string volumePath = m_file.Name;
                    Guid tempHash = Guid.NewGuid();
                    string tempCopy = $"Temp{tempHash}";
                    
                    using (FileStream tempStream = File.Create(tempCopy))
                    {
                        m_file.Seek(0, SeekOrigin.Begin);

                        for (int i = 0; i < deleteFile.Item1.StartCluster; i++)
                        {
                            tempStream.WriteByte((byte)m_file.ReadByte());
                        }

                        int postDirIndex = (int)deleteFile.Item1.StartCluster + DIR_PAGE_SIZE;
                        m_file.Seek(postDirIndex, SeekOrigin.Begin);

                        for (int i = postDirIndex; i < m_file.Length; i++)
                        {
                            tempStream.WriteByte((byte)m_file.ReadByte());
                        }
                    }

                    UnMount();

                    File.Delete(volumePath);
                    File.Move(tempCopy, volumePath);

                    Mount(volumePath);
                }
                else
                {
                    ZeroFile((uint)deleteFile.Item1.StartCluster);
                }

                uint entryAddress = deleteFile.Item2;
                m_file.Seek(entryAddress, SeekOrigin.Begin);
                for (int i = 0; i < DIR_ENTRY_SIZE; i++)
                {
                    m_file.WriteByte((byte)'\0');
                }
            }
            else
            {
                throw new ApplicationException($"File \"{FilePath}\" not found.");
            }
        }

        public void TruncateFile(string FilePath)
        {
            if (TryGetFile(FilePath, out Tuple<DirectoryEntry, uint> truncFile))
            {
                // Mark all pages used by file as unused
                ZeroFile((uint)truncFile.Item1.StartCluster);

                // Allocate a free page for file
                uint freeCluster = GetFreeCluster();
                m_file.Seek(freeCluster, SeekOrigin.Begin);
                m_file.Write(BitConverter.GetBytes(EOF_SIG), 0, FAT_ENTRY_SIZE);

                // Update first cluster and size
                truncFile.Item1.StartCluster = freeCluster;
                UpdateDirectoryEntry(truncFile, 0);
            }
            else
            {
                throw new ApplicationException($"File \"{FilePath}\" not found.");
            }
        }
        #endregion
    }
}
