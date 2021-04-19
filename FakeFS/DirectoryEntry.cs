using System;
using System.Text;


namespace FakeFS
{
    public class DirectoryEntry
    {
        static public int EntrySize = 96;
        static public int MaxNameLen = 39;
        private byte _attributes;
        private long _create_date;
        private long _modify_date;

        #region File metadata

        private string _filename;
        public string FileName 
        {
            get => _filename;
            set => _filename = value.PadRight(MaxNameLen - 1);
        }
        public bool isReadOnly 
        { 
            get => GetAttributeValue(0);
            set => SetAttributeValue(0, value);
        }
        public bool isHidden 
        { 
            get => GetAttributeValue(1);
            set => SetAttributeValue(1, value);
        }
        public bool isSystem 
        { 
            get => GetAttributeValue(2);
            set => SetAttributeValue(2, value);
        }
        public bool isLabel 
        { 
            get => GetAttributeValue(3);
            set => SetAttributeValue(3, value);
        }
        public bool isDirectory 
        { 
            get => GetAttributeValue(4);
            set => SetAttributeValue(4, value);
        }
        public bool isArchive 
        { 
            get => GetAttributeValue(5);
            set => SetAttributeValue(5, value);
        }
        public string CreateDate
        {
            get => DateTime.FromBinary(_create_date).ToString("MM/dd/yyyy");
        }
        public string CreateTime
        {
            get => DateTime.FromBinary(_create_date).ToString("HH:mm:ss");
        }
        public string ModifiedDate
        {
            get => DateTime.FromBinary(_modify_date).ToString("MM/dd/yyyy");
        }
        public string ModifiedTime
        {
            get => DateTime.FromBinary(_modify_date).ToString("HH:mm:ss");
        }
        public long StartCluster { get; set; }
        public long Size { get; private set; }

        private bool GetAttributeValue(int bitNumber)
        {
            return (_attributes & (1 << bitNumber)) != 0;
        }

        private void SetAttributeValue(int bitNumber, bool value)
        {
            if (value == true)
                _attributes |= (byte)(1 << bitNumber);
            else
                _attributes &= (byte)~(1 << bitNumber);
        }
        #endregion

        public DirectoryEntry(string filename, uint cluster)
        {
            FileName = filename.PadRight(MaxNameLen - 1);
            _attributes = 0;
            _create_date = DateTime.Now.ToBinary();
            _modify_date = DateTime.Now.ToBinary();
            StartCluster = cluster;
            Size = 0;
        }

        public DirectoryEntry(byte[] bytes)
        {
            string input = Encoding.ASCII.GetString(bytes);

            FileName = input.Substring(0, MaxNameLen - 1);
            _attributes = bytes[MaxNameLen];

            int padding = EntrySize - (4 * sizeof(long));
            byte[] longBytes = new byte[sizeof(long)];

            Array.Copy(bytes, padding, longBytes, 0, sizeof(long));
            _create_date = BitConverter.ToInt64(longBytes, 0);

            Array.Copy(bytes, padding + sizeof(long), longBytes, 0, sizeof(long));
            _modify_date = BitConverter.ToInt64(longBytes, 0);

            Array.Copy(bytes, padding + sizeof(long) * 2, longBytes, 0, sizeof(long));
            StartCluster = BitConverter.ToInt64(longBytes, 0);

            Array.Copy(bytes, padding + sizeof(long) * 3, longBytes, 0, sizeof(long));
            Size = BitConverter.ToInt64(longBytes, 0);
        }

        // Returns byte dump for insertion into file
        public byte[] Serialize()
        {
            byte[] entryDump = new byte[EntrySize];

            for (int i = 0; i < FileName.Length; i++)
            {
                entryDump[i] = (byte)FileName[i];
            }

            entryDump[MaxNameLen] = _attributes;

            int writerIndex = EntrySize - 1 - (4 * sizeof(long));

            foreach (long var in new[] { _create_date, _modify_date, StartCluster, Size })
            {
                byte[] varBytes = BitConverter.GetBytes(var);
                for (int i = 0; i < varBytes.Length; i++)
                {
                    writerIndex++;
                    entryDump[writerIndex] = varBytes[i];
                }
            }

            return entryDump;
        }

        public void Update(long? _size = null)
        {
            _modify_date = DateTime.Now.ToBinary();
            if (_size != null)
            {
                Size = (long)_size;
            }
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (obj == null)
                return false;

            if (GetType() != obj.GetType() )
                return false;

            DirectoryEntry entry = (DirectoryEntry)obj;

            return this.FileName.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase); 
        }

        public override int GetHashCode()
        {
            return FileName.GetHashCode(); 
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
