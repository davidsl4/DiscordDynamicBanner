using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DynamicBanner.Utils
{
    internal static class FileExtensions
    {
        public enum FileTypes
        {
            Unknown,
            Png
        }
        
        // ReSharper disable InconsistentNaming
        private static readonly byte[] PNG_HEADER = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static readonly IReadOnlyDictionary<FileTypes, byte[]> TYPES_HEADERS =
            new ReadOnlyDictionary<FileTypes, byte[]>(new Dictionary<FileTypes, byte[]>
            {
                {FileTypes.Png, PNG_HEADER} // 8 bytes
            });
        // ReSharper restore InconsistentNaming

        public static FileTypes GetFileTypeFromHeader(this IEnumerable<byte> bytes)
        {
            using var enumerator = bytes.GetEnumerator();
            foreach (var (type, header) in TYPES_HEADERS)
            {
                enumerator.Reset();
                foreach (var t in header)
                {
                    if (!enumerator.MoveNext()) continue;
                    if (t != enumerator.Current)
                        goto SKIP_FILE_TYPE;
                }

                return type;
                
                SKIP_FILE_TYPE: ;
            }

            return FileTypes.Unknown;
        }
    }
}