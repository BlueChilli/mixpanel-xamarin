using System.Reflection;
using Android.App;
using Android.Content.PM;
using Android.OS;
using MixPanelBinding.TestDroid;
using Xamarin.Android.NUnitLite;

namespace MixpanelBinding.TestDroid
{
    [Activity(Label = "MixpanelBinding.TestDroid", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Portrait )]
    public class MainActivity : TestSuiteActivity
    {
        private MixPanelStat stats;

        protected override void OnCreate(Bundle bundle)
        {
            // tests can be inside the main assembly
            AddTest(Assembly.GetExecutingAssembly());
            // or in any reference assemblies
            // AddTest (typeof (Your.Library.TestClass).Assembly);

            // Once you called base.OnCreate(), you cannot add more assemblies.
            base.OnCreate(bundle);



            //I put that in a iOC container as a singleton
            //This call must be after base.OnCreate
            stats = new MixPanelStat(this);
        }
    }
}
