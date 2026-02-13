using Syncfusion.Licensing;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MotorPanel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            //SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JGaF1cXmhKYVJyWmFZfVhgd19CZFZQR2Y/P1ZhSXxVdkdiWn9bdXFRQWhdUUd9XEA=");    //just 30 days
            SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjGyl/VkR+XU9Ff1RBQmJNYVF2R2VJfFR0cF9HY0wxOX1dQl9lSXxSdUVrXX9fd3RdR2dXUkY=");          //student license
        }
    }

}
