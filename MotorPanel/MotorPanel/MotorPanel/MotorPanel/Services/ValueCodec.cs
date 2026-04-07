using Com_ELF.Models;
using System.Text;

namespace Com_ELF.Services
{
    public static class ValueCodec
    {
        public static int GetByteCount(WatchDataType dataType) => dataType switch
        {
            WatchDataType.UInt8 or WatchDataType.Int8 => 1,
            WatchDataType.UInt16 or WatchDataType.Int16 => 2,
            WatchDataType.UInt32 or WatchDataType.Int32 or WatchDataType.Float => 4,
            _ => 4
        };

        public static byte[] ParseValue(string value, WatchDataType dataType)
        {
            try
            {
                return dataType switch
                {
                    WatchDataType.UInt8 => new[] { byte.Parse(value) },
                    WatchDataType.Int8 => new[] { unchecked((byte)sbyte.Parse(value)) },
                    WatchDataType.UInt16 => BitConverter.GetBytes(ushort.Parse(value)),
                    WatchDataType.Int16 => BitConverter.GetBytes(short.Parse(value)),
                    WatchDataType.UInt32 => BitConverter.GetBytes(uint.Parse(value)),
                    WatchDataType.Int32 => BitConverter.GetBytes(int.Parse(value)),
                    WatchDataType.Float => BitConverter.GetBytes(float.Parse(value)),
                    _ => throw new ArgumentException("Unknown data type")
                };
            }
            catch
            {
                throw new InvalidOperationException($"Cannot parse value '{value}' as {dataType}");
            }
        }

        public static string FormatValue(byte[] raw, WatchDataType dataType)
        {
            try
            {
                return dataType switch
                {
                    WatchDataType.UInt8 => raw[0].ToString(),
                    WatchDataType.Int8 => unchecked((sbyte)raw[0]).ToString(),
                    WatchDataType.UInt16 => BitConverter.ToUInt16(raw, 0).ToString(),
                    WatchDataType.Int16 => BitConverter.ToInt16(raw, 0).ToString(),
                    WatchDataType.UInt32 => BitConverter.ToUInt32(raw, 0).ToString(),
                    WatchDataType.Int32 => BitConverter.ToInt32(raw, 0).ToString(),
                    WatchDataType.Float => BitConverter.ToSingle(raw, 0).ToString("F4"),
                    _ => "N/A"
                };
            }
            catch
            {
                return "ERROR";
            }
        }
    }
}