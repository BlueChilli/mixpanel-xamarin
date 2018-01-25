using System.Threading;
using System.Text.RegularExpressions;
using System.IO;
//////////////////////////////////////////////////////////////////////
// ADDINS
//////////////////////////////////////////////////////////////////////

#addin "Cake.FileHelpers"
//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "GitReleaseManager"
#tool "GitVersion.CommandLine"
#tool "GitLink"
using Cake.Common.Build.TeamCity;


//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

if (string.IsNullOrWhiteSpace(target))
{
    target = "Default";
}

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Should MSBuild & GitLink treat any errors as warnings?
var treatWarningsAsErrors = false;
Func<string, int> GetEnvironmentInteger = name => {
	
	var data = EnvironmentVariable(name);
	int d = 0;
	if(!String.IsNullOrEmpty(data) && int.TryParse(data, out d)) 
	{
		return d;
	} 
	
	return 0;

};
// Build configuration
var local = BuildSystem.IsLocalBuild;
var isTeamCity = BuildSystem.TeamCity.IsRunningOnTeamCity;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var teamCity = BuildSystem.TeamCity;
var branch = EnvironmentVariable("Git_Branch");
var isPullRequest = !String.IsNullOrEmpty(branch) && branch.ToUpper().Contains("refs/pull"); //teamCity.Environment.PullRequest.IsPullRequest;
var projectName =  EnvironmentVariable("TEAMCITY_PROJECT_NAME"); //  teamCity.Environment.Project.Name;
var isRepository = StringComparer.OrdinalIgnoreCase.Equals("chillisource mobile bindings", projectName);
var isTagged = !String.IsNullOrEmpty(branch) && branch.ToUpper().Contains("refs/tags");
var buildConfName = EnvironmentVariable("TEAMCITY_BUILDCONF_NAME"); //teamCity.Environment.Build.BuildConfName
var buildNumber = GetEnvironmentInteger("BUILD_NUMBER");
var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("master", buildConfName);

var githubOwner = "BlueChilli";
var githubRepository = "mixpanel-xamarin";
var githubUrl = string.Format("https://github.com/{0}/{1}", githubOwner, githubRepository);
var licenceUrl = string.Format("{0}/blob/master/LICENSE", githubUrl);

// Version
string majorMinorPatch;
string semVersion;
string informationalVersion ;
string nugetVersion;
string buildVersion;

Action SetGitVersionData = () => {

	if(!isPullRequest) {
		var gitVersion = GitVersion();
		majorMinorPatch = gitVersion.MajorMinorPatch;
		semVersion = gitVersion.SemVer;
		informationalVersion = gitVersion.InformationalVersion;
		nugetVersion = gitVersion.NuGetVersion;
		buildVersion = gitVersion.FullBuildMetaData;
	}
	else {
		majorMinorPatch = "1.0.0";
		semVersion = "0";
		informationalVersion ="1.0.0";
		nugetVersion = "1.0.0";
		buildVersion = "alpha";
	}
};

SetGitVersionData();

// Artifacts
var artifactDirectory = "./artifacts/";
var packageWhitelist = new[] { "Vapolia.Mixpanel" };

var buildSolution = "./MixpanelBindingsBuild.sln";

// Macros
Func<string> GetMSBuildLoggerArguments = () => {
    return BuildSystem.TeamCity.IsRunningOnTeamCity ? EnvironmentVariable("MsBuildLogger"): null;
};


Action Abort = () => { throw new Exception("A non-recoverable fatal error occurred."); };
Action NonMacOSAbort = () => { throw new Exception("Running on platforms other macOS is not supported."); };


Action<string> RestorePackages = (solution) =>
{
    NuGetRestore(solution);
};


Action<string, string> Package = (nuspec, basePath) =>
{
    CreateDirectory(artifactDirectory);

    Information("Packaging {0} using {1} as the BasePath.", nuspec, basePath);

    NuGetPack(nuspec, new NuGetPackSettings {
        Verbosity                = NuGetVerbosity.Detailed,
        OutputDirectory          = artifactDirectory,
        BasePath                 = basePath,
		Version             = majorMinorPatch
    });
};

Action<string> SourceLink = (solutionFileName) =>
{
    GitLink("./", new GitLinkSettings() {
        RepositoryUrl = githubUrl,
        SolutionFileName = solutionFileName,
        ErrorsAsWarnings = true
    });
};

Action<string, string, Exception> WriteErrorLog = (message, identity, ex) => 
{
	if(isTeamCity) 
	{
		teamCity.BuildProblem(message, identity);
		teamCity.WriteStatus(String.Format("{0}", identity), "ERROR", ex.ToString());
	}
	else {
		throw new Exception(String.Format("task {0} - {1}", identity, message), ex);
	}
};


Func<string, IDisposable> BuildBlock = message => {

	if(BuildSystem.TeamCity.IsRunningOnTeamCity) 
	{
		return BuildSystem.TeamCity.BuildBlock(message);
	}
	
	return null;
	
};

Func<string, IDisposable> Block = message => {

	if(BuildSystem.TeamCity.IsRunningOnTeamCity) 
	{
		BuildSystem.TeamCity.Block(message);
	}

	return null;
};

Action<string,string> build = (solution, configuration) =>
{
    Information("Building {0}", solution);
	using(BuildBlock("Build")) 
	{			
        	MSBuild(solution, settings => {
			settings
			.SetConfiguration(configuration);
			
			settings
			.WithProperty("PackageOutputPath",  MakeAbsolute(Directory(artifactDirectory)).ToString())
			.WithProperty("NoWarn", "1591") // ignore missing XML doc warnings
			.WithProperty("TreatWarningsAsErrors", treatWarningsAsErrors.ToString())
			.WithProperty("ServerAddress",  "\"" +  EnvironmentVariable("MacServerAddress") + "\"")
		    .WithProperty("ServerUser",  "\"" +   EnvironmentVariable("MacServerUser") + "\"")
		    .WithProperty("ServerPassword",  "\"" +   EnvironmentVariable("MacServerPassword") + "\"")
		
		  	.SetVerbosity(Verbosity.Minimal)
			.SetNodeReuse(false);
		
			var msBuildLogger = GetMSBuildLoggerArguments();
		
			if(!string.IsNullOrEmpty(msBuildLogger)) 
			{
				Information("Using custom MSBuild logger: {0}", msBuildLogger);
				settings.ArgumentCustomization = arguments =>
				arguments.Append(string.Format("/logger:{0}", msBuildLogger));
			}
		});

    };		

};

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup((context) =>
{
    Information("Building version {0} of ChilliSource.Mobile.Bindings. (isTagged: {1})", informationalVersion, isTagged);

	if (isTeamCity)
	{
		Information(
				@"Environment:
				 PullRequest: {0}
				 Build Configuration Name: {1}
				 TeamCity Project Name: {2}
				 Branch: {3}",
				 isPullRequest,
				 buildConfName,
				 projectName,
				 branch
				);
    }
    else
    {
         Information("Not running on TeamCity");
    }


   // CleanDirectories(artifactDirectory);


});

Teardown((context) =>
{
    // Executed AFTER the last task.
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("RestorePackages")
//    .IsDependentOn("UpdateAssemblyInfo")
    .Does (() =>
{
    build(buildSolution, "Release");
})
.OnError(exception => {
	WriteErrorLog("Build failed", "Build", exception);
});


Task("UpdateAssemblyInfo")
    .Does (() =>
{
    var file = "./CommonAssemblyInfo.cs";

	using(BuildBlock("UpdateAssemblyInfo")) 
	{
		CreateAssemblyInfo(file, new AssemblyInfoSettings {
			Product = "ChilliSource Mobile Bindings",
			Version = majorMinorPatch,
			FileVersion = majorMinorPatch,
			InformationalVersion = informationalVersion,
			Copyright = "Copyright (c) BlueChilli Technology PTY LTD"
		});
	};
   
})
.OnError(exception => {
	WriteErrorLog("updating assembly info failed", "UpdateAssemblyInfo", exception);
});

Task("RestorePackages")
.Does (() =>
{
    Information("Restoring Packages for {0}", buildSolution);
	using(BuildBlock("RestorePackages")) 
	{
	    RestorePackages(buildSolution);
	};
})
.OnError(exception => {
	WriteErrorLog("restoring packages failed", "RestorePackages", exception);
});



Task("Package")
    .IsDependentOn("Build")
    .Does (() =>
{
	using(BuildBlock("Package")) 
	{
		foreach(var package in packageWhitelist)
		{
			// only push the package which was created during this build run.
			var packagePath = string.Format("./nuget/{0}.nuspec", package);

			// Push the package.
			Package(packagePath, "./nuget/");
		}
	};

    
})
.OnError(exception => {
	WriteErrorLog("Generating packages failed", "Package", exception);
});


Task("PublishPackages")
	.IsDependentOn("Build")
    .IsDependentOn("Package")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .Does (() =>
{
	using(BuildBlock("Package"))
	{
		string apiKey;
		string source;

		if (isReleaseBranch && !isTagged)
		{
			// Resolve the API key.
			apiKey = EnvironmentVariable("MYGET_APIKEY");
			if (string.IsNullOrEmpty(apiKey))
			{
				throw new Exception("The MYGET_APIKEY environment variable is not defined.");
			}

			source = EnvironmentVariable("MYGET_SOURCE");
			if (string.IsNullOrEmpty(source))
			{
				throw new Exception("The MYGET_SOURCE environment variable is not defined.");
			}
		}
		else 
		{
			// Resolve the API key.
			apiKey = EnvironmentVariable("NUGET_APIKEY");
			if (string.IsNullOrEmpty(apiKey))
			{
				throw new Exception("The NUGET_APIKEY environment variable is not defined.");
			}

			source = EnvironmentVariable("NUGET_SOURCE");
			if (string.IsNullOrEmpty(source))
			{
				throw new Exception("The NUGET_SOURCE environment variable is not defined.");
			}
		}

		var d = new DirectoryInfo(artifactDirectory);

		foreach(var file in d.GetFiles("*.nupkg")) 
		{			
			// only push the package which was created during this build run.
			var packagePath = artifactDirectory + File(file.Name);

			// Push the package.
			NuGetPush(packagePath, new NuGetPushSettings {
				Source = source,
				ApiKey = apiKey
			});
		}

	};

  
})
.OnError(exception => {
	WriteErrorLog("publishing packages failed", "PublishPackages", exception);
});

Task("CreateRelease")
    .IsDependentOn("Build")
    .IsDependentOn("Package")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .WithCriteria(() => isReleaseBranch)
    .WithCriteria(() => !isTagged)
    .WithCriteria(() => isRunningOnWindows)
    .Does (() =>
{
	using(BuildBlock("CreateRelease"))
	{
		var username = EnvironmentVariable("GITHUB_USERNAME");
		if (string.IsNullOrEmpty(username))
		{
			throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
		}

		var token = EnvironmentVariable("GITHUB_TOKEN");
		if (string.IsNullOrEmpty(token))
		{
			throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
		}

		GitReleaseManagerCreate(username, token, githubOwner, githubRepository, new GitReleaseManagerCreateSettings {
			Milestone         = majorMinorPatch,
			Name              = majorMinorPatch,
			Prerelease        = true,
			TargetCommitish   = "master"
		});
	};

})
.OnError(exception => {
	WriteErrorLog("creating release failed", "CreateRelease", exception);
});

Task("PublishRelease")
   .IsDependentOn("Build")
    .IsDependentOn("Package")
    .WithCriteria(() => !local)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isRepository)
    .WithCriteria(() => isReleaseBranch)
    .WithCriteria(() => isTagged)
    .WithCriteria(() => isRunningOnWindows)
    .Does (() =>
{
	using(BuildBlock("PublishRelease"))
	{
		var username = EnvironmentVariable("GITHUB_USERNAME");
		if (string.IsNullOrEmpty(username))
		{
			throw new Exception("The GITHUB_USERNAME environment variable is not defined.");
		}

		var token = EnvironmentVariable("GITHUB_TOKEN");
		if (string.IsNullOrEmpty(token))
		{
			throw new Exception("The GITHUB_TOKEN environment variable is not defined.");
		}

		// only push whitelisted packages.
		foreach(var package in packageWhitelist)
		{
			// only push the package which was created during this build run.
			var packagePath = artifactDirectory + File(string.Concat(package, ".", nugetVersion, ".nupkg"));

			GitReleaseManagerAddAssets(username, token, githubOwner, githubRepository, majorMinorPatch, packagePath);
		}

		GitReleaseManagerClose(username, token, githubOwner, githubRepository, majorMinorPatch);
	}; 
})
.OnError(exception => {
	WriteErrorLog("updating release assets failed", "PublishRelease", exception);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("CreateRelease")
    .IsDependentOn("PublishPackages")
    .IsDependentOn("PublishRelease")
    .Does (() =>
{

});


//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
