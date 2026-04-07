namespace Com_ELF.Models
{
    public class ElfSymbol
    {
        public string Name { get; set; } = string.Empty;
        public uint Address { get; set; }
        public uint Size { get; set; }

        public string AddressHex => $"0x{Address:X8}";
    }
}