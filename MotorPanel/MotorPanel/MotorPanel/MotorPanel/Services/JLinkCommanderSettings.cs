namespace Com_ELF.Services
{
    public class JLinkCommanderSettings
    {
        public string JLinkExePath { get; set; } = @"C:\Program Files\SEGGER\JLink_V880\JLink.exe";
        public string DeviceName { get; set; } = "PSC3XXF_TM";
        public string InterfaceName { get; set; } = "SWD";
        public int SpeedKHz { get; set; } = 1000;
        public string JLinkSerialNumber { get; set; } = string.Empty;
        public bool HaltBeforeAccess { get; set; } = true;
        public bool ResumeAfterAccess { get; set; }
    }
}