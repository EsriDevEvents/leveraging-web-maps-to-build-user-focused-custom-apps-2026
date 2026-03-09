using System.Configuration;
using System.Data;
using System.Windows;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Http;
using Esri.ArcGISRuntime.Security;

namespace DevSummit2026
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                ArcGISRuntimeEnvironment.Initialize(config => config
                  .ConfigureAuthentication(auth => auth
                     .UseDefaultChallengeHandler()
                   )
                );
                ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ArcGIS Maps SDK runtime initialization failed.");

                this.Shutdown();
            }
        }
    }
}
