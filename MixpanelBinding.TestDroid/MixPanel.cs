using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Runtime;
using MixpanelBinding.TestDroid;
using Org.Json;
using Mixpanel.Android.MpMetrics;

//Permissions required by Mixpanel
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
[assembly: UsesPermission(Android.Manifest.Permission.Bluetooth)]

//[assembly: MetaData("com.mixpanel.android.MPConfig.ResourcePackageName", Value = "MixpanelBinding.TestDroid")]
//Also add in AndroidManifest under applications:
//    <activity android:name="com.mixpanel.android.surveys.SurveyActivity" />

namespace MixPanelBinding.TestDroid
{
    //Required by MixPanel, it can not find R$id by reflection
    [Register("R")]
    public partial class Resource
    {
        
    };

    public interface IMixPanel
	{
		void LoginUser(string userId);
		void CreateUser(string userId);
		void LogoutUser();
	}

	public class MixPanelStat : IMixPanel
	{
		//TODO: USE YOUR OWN MIXPANEL ID !!!
        private const string YourMixPanelId = "1d7be370c1639b1df4a694a5912b8538";
	    private readonly MixpanelAPI mixpanel;

	    public MixPanelStat(Context context)
	    {
			try
			{
                mixpanel = MixpanelAPI.GetInstance(context, YourMixPanelId);
				Debug.WriteLine("Mixpanel initialized DistinctId {0}", (object)mixpanel.DistinctId);

                //Ex: send an event with some properties
               CreateUser("user123");
                mixpanel.Track("Coucou Android", new JSONObject(new Dictionary<string,object>{ {"key1","value1"} }));
              

			}
			catch(Exception e)
			{
				Debug.WriteLine ("Error in Mixpanel SDK: {0}", (object)e.ToString());
			}
	    }

		public void LoginUser(string userId)
		{
			mixpanel.Identify(userId);
		}

		public void CreateUser(string userId)
		{
			mixpanel.Alias(userId, mixpanel.DistinctId);
			mixpanel.Identify(mixpanel.DistinctId);
		}

		public void LogoutUser()
		{
			mixpanel.Reset();
		}
	}
}

